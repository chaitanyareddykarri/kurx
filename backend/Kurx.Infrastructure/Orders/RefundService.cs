using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Orders;

/// <summary>
/// D-103 (M4): the reverse-ledger refund path. One transaction that appends to the append-only ledger and updates
/// the projected wallet cache together, so the two can never disagree. Phase 9 (§9.5/§9.6/§17.1): the refund amount
/// is derived from the immutable <b>VAR</b> (never recomputed), the seats return to the authoritative pool via a
/// conditional release, the admissions/credentials are voided in the same transaction, and the Paid→Refunded
/// transition is <b>atomically claimed</b> — which is what makes concurrent/duplicate full refunds idempotent (a
/// second one sees Refunded and returns already_refunded). No UNIQUE(order_id) is needed, and its absence keeps V3
/// §9.6 partial refunds open. The chat cleanup is a non-money side effect and goes through the outbox.
/// </summary>
public class RefundService(KurxDbContext db, IAuditWriter audit, ILogger<RefundService> log,
    IInventoryService inventory, IEventRegistrationService registration) : IRefundService
{
    public async Task<ServiceResult<RefundResult>> RefundOrderAsync(Guid orderId, string reason, Guid? actorId,
        CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Fail("not_found");

        // Replay-safe fast path: a retried cancellation or re-delivered webhook must not reverse twice.
        if (order.Status is OrderStatus.Refunded)
        {
            var prior = await db.Refunds.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == order.Id, ct);
            EnqueueChatLeave(order.EventId, order.UserId);   // re-run cleanup defensively; idempotent
            await db.SaveChangesAsync(ct);
            return ServiceResult<RefundResult>.Success(
                new RefundResult(order.Id, prior?.Id ?? Guid.Empty, prior?.AmountPaise ?? order.AmountPaise, "already_refunded"));
        }
        if (order.Status != OrderStatus.Paid) return Fail("invalid_order_state");

        var ev = await db.Events.AsNoTracking().FirstAsync(e => e.Id == order.EventId, ct);
        // Existence only — the balances are never read into memory and decided on here (see the debit below).
        if (!await db.OrganizationWallets.AnyAsync(w => w.OrgId == ev.RepresentingOrgId, ct)) return Fail("wallet_not_found");

        // The refund amount is the VAR (§9.5) — the same immutable snapshot revenue reads, so the books balance.
        // Fallback to the order amount only if VAR is absent (defensive; backfill guarantees it exists).
        var varSum = await db.ValueAllocationRecords.Where(v => v.OrderId == order.Id).SumAsync(v => (long?)v.AllocatedPaise, ct) ?? 0;
        var amount = varSum > 0 ? varSum : order.AmountPaise;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Duplicate/concurrent refund safety (§17.1): atomically claim Paid→Refunded. 0 rows ⇒ someone else won.
        var claimed = await db.Orders.Where(o => o.Id == order.Id && o.Status == OrderStatus.Paid)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Refunded), ct);
        if (claimed == 0)
        {
            await tx.RollbackAsync(ct);
            var prior = await db.Refunds.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == order.Id, ct);
            return ServiceResult<RefundResult>.Success(
                new RefundResult(order.Id, prior?.Id ?? Guid.Empty, prior?.AmountPaise ?? amount, "already_refunded"));
        }
        await db.Entry(order).ReloadAsync(ct);

        // Debit where the money actually sits: CollectedToAvailableLedgerJob matures Collected -> Available,
        // so a refund after settlement must come out of Available. The wallet's CHECK constraints forbid a
        // negative balance, so an under-funded wallet is refused here rather than throwing at the database.
        //
        // Both attempts carry the sufficiency test in the WHERE, so Postgres evaluates it under the row lock
        // and the decrement is conditional (same contract as InventoryService.TryConsumeManyAsync). Reading
        // the balances into memory and assigning back made EF emit `SET "CollectedPaise" = <literal>`: two
        // refunds for the SAME org on DIFFERENT orders both passed a stale check and the second overwrote
        // the first, so the money left the ledger but stayed in the cached balance — phantom funds the org
        // could still withdraw.
        var debited = await db.OrganizationWallets
            .Where(w => w.OrgId == ev.RepresentingOrgId && w.CollectedPaise >= amount)
            .ExecuteUpdateAsync(s => s
                .SetProperty(w => w.CollectedPaise, w => w.CollectedPaise - amount)
                .SetProperty(w => w.UpdatedAt, DateTime.UtcNow), ct);
        if (debited == 0)
            debited = await db.OrganizationWallets
                .Where(w => w.OrgId == ev.RepresentingOrgId && w.AvailablePaise >= amount)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(w => w.AvailablePaise, w => w.AvailablePaise - amount)
                    .SetProperty(w => w.UpdatedAt, DateTime.UtcNow), ct);
        if (debited == 0) { await tx.RollbackAsync(ct); return Fail("insufficient_wallet_balance"); }

        // Reverse-ledger: a prior entry is NEVER mutated — the negation is appended (D-103).
        var reversal = new LedgerEntry
        {
            OrgId = ev.RepresentingOrgId,
            EventId = ev.Id,
            Currency = ev.SettlementCurrency,   // V3 §9.1 — same currency as the event's collected funds
            AmountPaise = -amount,
            State = LedgerState.Refunded,
            RefType = "refund",
            RefId = order.Id,
        };
        db.LedgerEntries.Add(reversal);

        // Initiated, not Processed: the platform's books are reversed now, but the customer has not been
        // paid back until a real gateway disburses it. That flip belongs to the payments integration.
        var refund = new Refund
        {
            OrderId = order.Id,
            Currency = ev.SettlementCurrency,   // V3 §9.1
            AmountPaise = amount,
            Reason = reason,
            Status = RefundStatus.Initiated,
        };
        db.Refunds.Add(refund);

        // Tickets from this order stop being valid for entry, and their seats return to the authoritative pool.
        var orderItemIds = await db.OrderItems.Where(oi => oi.OrderId == order.Id).Select(oi => oi.Id).ToListAsync(ct);
        var tickets = await db.Tickets
            .Where(t => orderItemIds.Contains(t.OrderItemId) && t.State != TicketState.Void)
            .ToListAsync(ct);
        foreach (var ticket in tickets) ticket.State = TicketState.Void;

        var ticketType = await db.TicketTypes.FirstOrDefaultAsync(t => t.Id == order.TicketTypeId, ct);
        if (ticketType is not null && tickets.Count > 0)
        {
            ticketType.Sold = Math.Max(0, ticketType.Sold - tickets.Count);   // legacy mirror
            var poolId = await inventory.GeneralPoolIdAsync(ticketType.Id, ct);
            if (poolId is { } pid) await inventory.ReleaseConsumedAsync(pid, tickets.Count, ct);   // §17.1 — inventory returns on refund
        }

        audit.Write(new AuditEvent("order.refunded", "orders", order.Id,
            ActorType: actorId is null ? "system" : "user",
            ActorId: actorId,
            Before: new { status = nameof(OrderStatus.Paid) },
            After: new
            {
                status = nameof(OrderStatus.Refunded),
                amount_paise = amount,
                reason,
                tickets_voided = tickets.Count,
            }));

        await db.SaveChangesAsync(ct);

        // Stamp the audit link only now: LastLedgerEntryId is a real FK to the reversal row the SaveChanges
        // above just inserted, so it cannot be set in the same statement as the debit.
        await db.OrganizationWallets.Where(w => w.OrgId == ev.RepresentingOrgId)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.LastLedgerEntryId, reversal.Id), ct);

        // Authoritative void (§17.1, Phase 9): re-project in the SAME transaction so the voided tickets flip their
        // admissions to Void and revoke credentials with no remaining active admission — never eventually.
        await registration.ProjectOrderInTransactionAsync(order.Id, ct);
        EnqueueChatLeave(order.EventId, order.UserId);   // non-money side effect via outbox
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        log.LogInformation("Order {OrderId} refunded {AmountPaise} paise ({Reason}); {Count} ticket(s) voided",
            order.Id, amount, reason, tickets.Count);
        return ServiceResult<RefundResult>.Success(new RefundResult(order.Id, refund.Id, amount, "refunded"));
    }

    // D-199: the HTTP-facing entry point. Authorizes, then delegates to the RefundOrderAsync above unchanged.
    public async Task<ServiceResult<RefundResult>> RequestRefundAsync(Guid orderId, string reason, Guid actorId,
        bool isFinanceStaff, CancellationToken ct = default)
    {
        if (!isFinanceStaff && !await HasFinancialAccessForOrderAsync(orderId, actorId, ct))
            return Fail("forbidden");
        return await RefundOrderAsync(orderId, reason, actorId, ct);
    }

    public async Task<ServiceResult<RefundView>> GetForOrderAsync(Guid orderId, Guid requestingUserId,
        bool isFinanceStaff, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return ServiceResult<RefundView>.Fail("not_found");

        // D-018: a non-existent order and one that exists but the caller can't see must be indistinguishable
        // — both "not_found", never "forbidden" — so an authenticated caller can't confirm someone else's
        // order GUID is real. (The mutating POST /refund path uses "forbidden" instead: it can't be reached
        // by GUID-probing alone, since the same rejection already covers "doesn't exist" for non-staff callers.)
        var isOwnOrder = order.UserId == requestingUserId;
        if (!isFinanceStaff && !isOwnOrder && !await HasFinancialAccessForOrderAsync(orderId, requestingUserId, ct))
            return ServiceResult<RefundView>.Fail("not_found");

        var refund = await db.Refunds.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == orderId, ct);
        if (refund is null) return ServiceResult<RefundView>.Fail("not_found");

        var ev = await db.Events.AsNoTracking().FirstAsync(e => e.Id == order.EventId, ct);
        return ServiceResult<RefundView>.Success(ToView(refund, order.EventId, ev.RepresentingOrgId));
    }

    public async Task<IReadOnlyList<RefundView>> MyRefundsAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await (from r in db.Refunds.AsNoTracking()
                           join o in db.Orders.AsNoTracking() on r.OrderId equals o.Id
                           where o.UserId == userId
                           orderby r.CreatedAt descending
                           select new { Refund = r, o.EventId }).ToListAsync(ct);
        if (rows.Count == 0) return [];

        var eventIds = rows.Select(x => x.EventId).Distinct().ToList();
        var orgByEvent = await db.Events.AsNoTracking().Where(e => eventIds.Contains(e.Id))
            .Select(e => new { e.Id, e.RepresentingOrgId }).ToDictionaryAsync(e => e.Id, e => e.RepresentingOrgId, ct);
        return rows.Select(x => ToView(x.Refund, x.EventId, orgByEvent[x.EventId])).ToList();
    }

    public async Task<(IReadOnlyList<RefundView> Items, int Total)> ListForAdminAsync(string? status, int limit,
        int page, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        page = Math.Max(page, 1);

        var q = db.Refunds.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RefundStatus>(status, true, out var st))
            q = q.Where(r => r.Status == st);

        var total = await q.CountAsync(ct);
        // DB-6: refunds are a money ledger; an unstable page boundary here misstates what was returned.
        var pageRows = await q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * limit).Take(limit)
            .Join(db.Orders.AsNoTracking(), r => r.OrderId, o => o.Id, (r, o) => new { Refund = r, o.EventId })
            .ToListAsync(ct);

        var eventIds = pageRows.Select(x => x.EventId).Distinct().ToList();
        var orgByEvent = await db.Events.AsNoTracking().Where(e => eventIds.Contains(e.Id))
            .Select(e => new { e.Id, e.RepresentingOrgId }).ToDictionaryAsync(e => e.Id, e => e.RepresentingOrgId, ct);
        var items = pageRows.Select(x => ToView(x.Refund, x.EventId, orgByEvent[x.EventId])).ToList();
        return (items, total);
    }

    private static RefundView ToView(Refund r, Guid eventId, Guid orgId) =>
        new(r.Id, r.OrderId, eventId, orgId, r.AmountPaise, r.Currency, r.Reason, r.Status.ToString(),
            r.RazorpayRefundId, r.CreatedAt);

    // The same financial-access bar WalletService.HasFinancialAccess uses (Owner/Finance), resolved from
    // the order's event's org rather than a direct orgId — refunds are addressed by order, not by org.
    private async Task<bool> HasFinancialAccessForOrderAsync(Guid orderId, Guid userId, CancellationToken ct)
    {
        var orgId = await (from o in db.Orders.AsNoTracking()
                            join e in db.Events.AsNoTracking() on o.EventId equals e.Id
                            where o.Id == orderId
                            select (Guid?)e.RepresentingOrgId).FirstOrDefaultAsync(ct);
        if (orgId is null) return false;
        return await db.Memberships.AnyAsync(m => m.OrgId == orgId && m.UserId == userId
            && (m.Role == OrgRole.Owner || m.Role == OrgRole.Finance), ct);
    }

    // The chat membership cleanup is a non-money side effect (§17.1): enqueued to the transactional outbox and
    // delivered at-least-once. The handler re-checks ticket state, so it never evicts a still-valid member.
    private void EnqueueChatLeave(Guid eventId, Guid? userId)
    {
        if (userId is null) return;
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "event.chat_leave_if_no_tickets",
            PayloadJson = JsonSerializer.Serialize(new { eventId, userId = userId.Value }),
            IdempotencyKey = $"chat_leave:{eventId}:{userId}:{Guid.NewGuid():N}",
        });
    }

    private static ServiceResult<RefundResult> Fail(string error) => ServiceResult<RefundResult>.Fail(error);
}
