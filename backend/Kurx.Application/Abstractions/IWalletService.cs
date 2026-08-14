namespace Kurx.Application.Abstractions;

public record WalletView(
    Guid OrgId,
    long CollectedPaise,
    long AvailablePaise,
    long AdvancedPaise,
    long ReservedPaise,
    long SettledPaise,
    long LifetimeEarnedPaise,
    long LifetimeWithdrawnPaise,
    DateTime UpdatedAt,
    string Currency);

public record LedgerEntryView(
    Guid Id,
    long AmountPaise,
    string State,
    string RefType,
    Guid RefId,
    Guid EventId,
    string Currency,
    DateTime CreatedAt);

// Bank account details are already on file via KYC penny-drop (D-016).
public record WithdrawInput(long AmountPaise);

public interface IWalletService
{
    /// <summary><paramref name="isAdmin"/> (D-186): the one org-scoped read this codebase had never wired
    /// the platform-admin bypass onto — every other org-scoped service already takes this parameter. Reads
    /// only; <see cref="InitiateWithdrawalAsync"/> deliberately does NOT gain an admin bypass — an admin
    /// viewing a wallet is a much smaller decision than one moving money on an org's behalf.</summary>
    Task<ServiceResult<WalletView>> GetWalletAsync(Guid requestingUserId, Guid orgId, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<LedgerEntryView>>> GetLedgerAsync(Guid requestingUserId, Guid orgId, bool isAdmin, int page, int pageSize, CancellationToken ct = default);

    Task<ServiceResult<Guid>> InitiateWithdrawalAsync(Guid requestingUserId, Guid orgId, WithdrawInput input, CancellationToken ct = default);

    /// <summary>Every org whose cached wallet disagrees with the sum of its ledger. Read-only.</summary>
    Task<IReadOnlyList<WalletDrift>> ReconcileAsync(Guid? orgId = null, CancellationToken ct = default);

    /// <summary>Resets each drifting wallet's balances to the ledger, which is the source of truth
    /// (D-103). Idempotent; a re-run over an in-sync wallet changes nothing. Returns the count repaired.</summary>
    Task<int> RepairAsync(Guid? orgId = null, CancellationToken ct = default);
}

/// <summary>A wallet whose cached balance no longer matches its ledger. <paramref name="CachedPaise"/> is
/// Collected+Available (the two states the write paths move between); <paramref name="LedgerPaise"/> is the
/// summed ledger for the same org.</summary>
public record WalletDrift(Guid OrgId, long CachedPaise, long LedgerPaise)
{
    public long DeltaPaise => CachedPaise - LedgerPaise;
}
