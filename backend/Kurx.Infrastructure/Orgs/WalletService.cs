using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrgRole = Kurx.Domain.Enums.OrgRole;

namespace Kurx.Infrastructure.Orgs;

public class WalletService(KurxDbContext db) : IWalletService
{
    public async Task<ServiceResult<WalletView>> GetWalletAsync(Guid requestingUserId, Guid orgId, bool isAdmin, CancellationToken ct = default)
    {
        if (!isAdmin && !await HasFinancialAccess(requestingUserId, orgId, ct))
            return ServiceResult<WalletView>.Fail("forbidden");

        var wallet = await db.OrganizationWallets.FirstOrDefaultAsync(w => w.OrgId == orgId, ct);
        // Lazy-seed for orgs created before Phase 1 seeding was wired.
        if (wallet is null)
            wallet = await LazySeedWalletAsync(orgId, ct);

        return ServiceResult<WalletView>.Success(new WalletView(
            wallet.OrgId, wallet.CollectedPaise, wallet.AvailablePaise,
            wallet.AdvancedPaise, wallet.ReservedPaise, wallet.SettledPaise,
            wallet.LifetimeEarnedPaise, wallet.LifetimeWithdrawnPaise, wallet.UpdatedAt, wallet.Currency));
    }

    public async Task<ServiceResult<IReadOnlyList<LedgerEntryView>>> GetLedgerAsync(
        Guid requestingUserId, Guid orgId, bool isAdmin, int page, int pageSize, CancellationToken ct = default)
    {
        if (!isAdmin && !await HasFinancialAccess(requestingUserId, orgId, ct))
            return ServiceResult<IReadOnlyList<LedgerEntryView>>.Fail("forbidden");

        pageSize = Math.Clamp(pageSize, 1, 100);
        var entries = await db.LedgerEntries
            .Where(l => l.OrgId == orgId)
            // DB-6: ledger entries are written in batches by the settlement job, so same-tick rows are the
            // norm here rather than the exception — and a ledger that shows a row twice is not a ledger.
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(l => new LedgerEntryView(l.Id, l.AmountPaise, l.State.ToString(),
                l.RefType, l.RefId, l.EventId, l.Currency, l.CreatedAt))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<LedgerEntryView>>.Success(entries);
    }

    public async Task<ServiceResult<Guid>> InitiateWithdrawalAsync(
        Guid requestingUserId, Guid orgId, WithdrawInput input, CancellationToken ct = default)
    {
        if (!await HasFinancialAccess(requestingUserId, orgId, ct))
            return ServiceResult<Guid>.Fail("forbidden");

        // Payout account must be Active before any withdrawal can be initiated.
        var payoutStatus = await db.Organizations.AsNoTracking()
            .Where(o => o.Id == orgId)
            .Select(o => (PayoutAccountStatus?)o.PayoutAccountStatus)
            .FirstOrDefaultAsync(ct);
        if (payoutStatus != PayoutAccountStatus.Active)
            return ServiceResult<Guid>.Fail("payout_not_active");

        // Use an explicit transaction with SELECT FOR UPDATE to serialize concurrent withdrawal
        // requests for the same org — prevents both from passing the balance check simultaneously.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // DB-2: bound the wait. Without a lock_timeout this FOR UPDATE waits forever, so one stuck
        // transaction blocks every subsequent withdrawal for this org indefinitely while each blocked
        // caller pins a pooled connection — the failure mode is a slow drain of the pool, not a visible
        // error. SET LOCAL scopes it to this transaction, so no other query's locking behaviour changes.
        //
        // Five seconds: a real contender (another withdrawal for the same org) holds this lock for a
        // handful of queries, so anything past a few seconds means the holder is stuck rather than busy.
        // Losing the race surfaces as 55P03 → `resource_busy` → 503 + Retry-After, and NOTHING is written
        // before the lock is taken, so retrying is safe and cannot double-withdraw.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", ct);

        // Acquire a row-level lock; any concurrent withdrawal for this org blocks here.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM organization_wallet WHERE \"OrgId\" = {orgId} FOR UPDATE", ct);

        var wallet = await db.OrganizationWallets.FirstOrDefaultAsync(w => w.OrgId == orgId, ct);
        if (wallet is null)
            return ServiceResult<Guid>.Fail("wallet_not_found");

        // Deduct in-flight withdrawals so a burst of requests cannot collectively exceed the
        // available balance before any individual request has been processed or paid.
        var pendingPaise = await db.Withdrawals
            .Where(w => w.OrgId == orgId && (w.Status == "requested" || w.Status == "processing"))
            .SumAsync(w => (long?)w.AmountPaise, ct) ?? 0L;

        var effectiveAvailable = wallet.AvailablePaise - pendingPaise;
        if (input.AmountPaise <= 0 || input.AmountPaise > effectiveAvailable)
            return ServiceResult<Guid>.Fail("insufficient_balance");

        var withdrawal = new Withdrawal { OrgId = orgId, AmountPaise = input.AmountPaise };
        db.Withdrawals.Add(withdrawal);
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = requestingUserId,
            Action = "withdrawal.initiate", Entity = "withdrawals", EntityId = withdrawal.Id,
            DetailsJson = $"{{\"org_id\":\"{orgId}\",\"amount_paise\":{input.AmountPaise}}}",
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<Guid>.Success(withdrawal.Id);
    }

    // Financial access is restricted to Owner and Finance roles (D-015).
    private async Task<bool> HasFinancialAccess(Guid userId, Guid orgId, CancellationToken ct)
        => await db.Memberships.AnyAsync(
            m => m.OrgId == orgId && m.UserId == userId
                 && (m.Role == OrgRole.Owner || m.Role == OrgRole.Finance), ct);

    /// <summary>The wallet is a projection of the append-only ledger (D-103), so the ledger sum is ground
    /// truth and any disagreement is cache drift. This is the money-side sibling of
    /// <c>IInventoryService.ReconcileAsync</c> — inventory and registrations both had continuous proof of
    /// their invariant and money did not, which is exactly why the lost-update bug behind D-240 could have
    /// run in production unnoticed: the ledger stayed correct while the balance quietly diverged.
    ///
    /// <para>Compares against Collected+Available only. Advanced/Reserved/Settled are moved by payout
    /// flows that do not yet write ledger rows, so folding them in would report permanent false drift.</para></summary>
    public async Task<IReadOnlyList<WalletDrift>> ReconcileAsync(Guid? orgId = null, CancellationToken ct = default)
    {
        var ledgerByOrg = await db.LedgerEntries.AsNoTracking()
            .Where(l => orgId == null || l.OrgId == orgId)
            .GroupBy(l => l.OrgId)
            .Select(g => new { OrgId = g.Key, Sum = g.Sum(l => l.AmountPaise) })
            .ToDictionaryAsync(x => x.OrgId, x => x.Sum, ct);

        var wallets = await db.OrganizationWallets.AsNoTracking()
            .Where(w => orgId == null || w.OrgId == orgId)
            .Select(w => new { w.OrgId, w.CollectedPaise, w.AvailablePaise })
            .ToListAsync(ct);

        // A wallet with no ledger rows sums to 0 — correct, and it must still be checked: a balance
        // sitting on a wallet that has no entries backing it is drift, not an absence of evidence.
        return wallets
            .Select(w => new WalletDrift(w.OrgId, w.CollectedPaise + w.AvailablePaise, ledgerByOrg.GetValueOrDefault(w.OrgId)))
            .Where(d => d.DeltaPaise != 0)
            .ToList();
    }

    public async Task<int> RepairAsync(Guid? orgId = null, CancellationToken ct = default)
    {
        var drift = await ReconcileAsync(orgId, ct);
        if (drift.Count == 0) return 0;

        var driftOrgIds = drift.Select(d => d.OrgId).ToList();
        var availableByOrg = await db.OrganizationWallets.AsNoTracking()
            .Where(w => driftOrgIds.Contains(w.OrgId))
            .ToDictionaryAsync(w => w.OrgId, w => w.AvailablePaise, ct);

        foreach (var d in drift)
        {
            // The correction lands on Collected, never Available. Available is what
            // InitiateWithdrawalAsync pays out against, so inflating it during a repair could hand an org
            // money that has not settled — a repair must not be able to create a withdrawal.
            //
            // Per-bucket reconstruction from the ledger is not possible: a Refunded entry is a single
            // negative row that does not record which bucket it debited. So the ledger fixes the TOTAL and
            // Available is preserved as-is, with Collected absorbing the difference.
            var available = availableByOrg.GetValueOrDefault(d.OrgId);
            var collected = d.LedgerPaise - available;
            // Ledger below the matured balance means Available itself is overstated; clamp rather than
            // write a negative, which the ck_org_wallet_* CHECKs would reject and roll the repair back.
            if (collected < 0) { collected = 0; available = Math.Max(0, d.LedgerPaise); }

            await db.OrganizationWallets.Where(w => w.OrgId == d.OrgId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(w => w.CollectedPaise, collected)
                    .SetProperty(w => w.AvailablePaise, available)
                    .SetProperty(w => w.UpdatedAt, DateTime.UtcNow), ct);
        }
        return drift.Count;
    }

    // Seeds a zero-balance wallet for orgs that predate automatic seeding.
    // The unique constraint on OrgId is the race guard — only one INSERT wins;
    // the loser reads the winner's row.
    private async Task<OrganizationWallet> LazySeedWalletAsync(Guid orgId, CancellationToken ct)
    {
        var wallet = new OrganizationWallet { OrgId = orgId };
        try
        {
            db.OrganizationWallets.Add(wallet);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true)
        {
            db.ChangeTracker.Clear();
            wallet = await db.OrganizationWallets.FirstAsync(w => w.OrgId == orgId, ct);
        }
        return wallet;
    }
}
