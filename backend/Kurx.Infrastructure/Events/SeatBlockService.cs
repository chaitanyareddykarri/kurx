using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Delegated registration / SeatBlock (V3 §7.5, Phase 13). Creation buys <c>Quantity</c> seats through the
/// <b>authoritative Order/Ticket money path</b> against the General pool, minting <b>unassigned</b> Admissions
/// (PersonId null) via the existing projection. The delegate console binds a person by setting the seat's ticket
/// <c>UserId</c> and <b>re-running the same projection</b> — which re-derives the admission's person and its one-per-tree
/// Credential — governed by the block's assignment deadline + reassign limit, every (re)assignment audited. Payment is
/// data/authz only (FREE | DEFERRED, §9.7): no live collection.</summary>
public class SeatBlockService(KurxDbContext db, TokenService tokens, IInventoryService inventory,
    IEventRegistrationService registration, IEventPermissionService permissions) : ISeatBlockService
{
    public async Task<ServiceResult<SeatBlockView>> CreateAsync(Guid actorId, Guid eventId, bool isAdmin, SeatBlockInput input, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<SeatBlockView>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, eventId, isAdmin, ct)) return ServiceResult<SeatBlockView>.Fail("forbidden");
        if (input.Quantity < 1) return ServiceResult<SeatBlockView>.Fail("invalid_quantity");
        var reassignLimit = input.ReassignLimit ?? 0;
        if (reassignLimit < 0) return ServiceResult<SeatBlockView>.Fail("invalid_reassign_limit");
        if (!TryEnum(input.PaymentMode, out DelegatedPaymentMode payment, DelegatedPaymentMode.Free)) return ServiceResult<SeatBlockView>.Fail("invalid_payment_mode");
        var tt = await db.TicketTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == input.TicketTypeId && t.EventId == eventId, ct);
        if (tt is null) return ServiceResult<SeatBlockView>.Fail("invalid_ticket_type");
        var unit = await db.OrgUnits.AsNoTracking().FirstOrDefaultAsync(u => u.Id == input.RegistrantOrgUnitId && u.OrgId == ev.RepresentingOrgId, ct);
        if (unit is null) return ServiceResult<SeatBlockView>.Fail("invalid_org_unit");
        if (!await db.Users.AnyAsync(u => u.Id == input.DelegateUserId, ct)) return ServiceResult<SeatBlockView>.Fail("invalid_delegate");
        if (input.PayerId is { } pid && !await db.Users.AnyAsync(u => u.Id == pid, ct)) return ServiceResult<SeatBlockView>.Fail("invalid_payer");

        var poolId = await inventory.EnsureGeneralPoolAsync(tt.Id, tt.EventId, tt.Quantity, tt.Sold, ct);
        var unitPaise = payment == DelegatedPaymentMode.Free ? 0 : tt.PricePaise;   // DEFERRED records the owed amount (invoiced to the org); no collection this phase

        var order = new Order { UserId = null, EventId = eventId, TicketTypeId = tt.Id, Status = OrderStatus.Paid, AmountPaise = unitPaise * input.Quantity, Currency = ev.SettlementCurrency };
        var orderItem = new OrderItem { OrderId = order.Id, TicketTypeId = tt.Id, Qty = input.Quantity, UnitPricePaise = unitPaise, Currency = ev.SettlementCurrency };
        var tickets = Enumerable.Range(0, input.Quantity).Select(_ =>
        {
            var code = Guid.NewGuid();
            return new Ticket { OrderItemId = orderItem.Id, EventId = eventId, UserId = null, Code = code, HmacSig = tokens.SignTicketCode(code) };
        }).ToList();

        var block = new SeatBlock
        {
            EventId = eventId, TicketTypeId = tt.Id, RegistrantOrgUnitId = input.RegistrantOrgUnitId, PayerId = input.PayerId,
            DelegateUserId = input.DelegateUserId, PaymentMode = payment, Quantity = input.Quantity,
            AssignmentDeadline = input.AssignmentDeadline, ReassignLimit = reassignLimit, OrderId = order.Id, CreatedBy = actorId,
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Conditional CONSUME of N (§17.1) — all-or-nothing. No capacity ⇒ roll back; nothing written.
        if (!await inventory.TryConsumeManyAsync([new PoolDraw(poolId, input.Quantity)], ct)) return ServiceResult<SeatBlockView>.Fail("sold_out");
        db.Orders.Add(order);
        db.OrderItems.Add(orderItem);
        db.Tickets.AddRange(tickets);
        tt2Sold(tt.Id, input.Quantity);   // legacy mirror bump
        await db.SaveChangesAsync(ct);
        await registration.ProjectOrderInTransactionAsync(order.Id, ct);   // N unassigned admissions (PersonId null) + VAR

        var admissions = await db.Admissions.Where(a => tickets.Select(t => t.Id).Contains(a.TicketId)).ToListAsync(ct);
        db.SeatBlocks.Add(block);
        foreach (var adm in admissions)
            db.SeatBlockSeats.Add(new SeatBlockSeat { SeatBlockId = block.Id, AdmissionId = adm.Id, ReassignableUntil = input.AssignmentDeadline });
        db.AuditLogs.Add(Audit(actorId, isAdmin, "seatblock.create", block.Id, JsonSerializer.Serialize(new { eventId, qty = input.Quantity, unit = input.RegistrantOrgUnitId })));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<SeatBlockView>.Success(await ToViewAsync(block, ct));
    }

    public async Task<ServiceResult<SeatBlockView>> GetAsync(Guid actorId, Guid seatBlockId, bool isAdmin, CancellationToken ct = default)
    {
        var block = await db.SeatBlocks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == seatBlockId, ct);
        if (block is null) return ServiceResult<SeatBlockView>.Fail("not_found");
        if (!await CanManageBlockAsync(actorId, block, isAdmin, ct)) return ServiceResult<SeatBlockView>.Fail("forbidden");
        return ServiceResult<SeatBlockView>.Success(await ToViewAsync(block, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<SeatBlockView>>> ListForEventAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        if (!await IsOrganiserAsync(actorId, eventId, isAdmin, ct)) return ServiceResult<IReadOnlyList<SeatBlockView>>.Fail("forbidden");
        var blocks = await db.SeatBlocks.AsNoTracking().Where(b => b.EventId == eventId).OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
        // M4: one grouped assigned-seat count for all blocks, not a query per block.
        var ids = blocks.Select(b => b.Id).ToList();
        var assigned = (await db.SeatBlockSeats.AsNoTracking().Where(s => ids.Contains(s.SeatBlockId) && s.AssignedAt != null)
            .GroupBy(s => s.SeatBlockId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.N);
        return ServiceResult<IReadOnlyList<SeatBlockView>>.Success(blocks.Select(b => ToView(b, assigned.GetValueOrDefault(b.Id))).ToList());
    }

    public async Task<ServiceResult<IReadOnlyList<SeatView>>> ListSeatsAsync(Guid actorId, Guid seatBlockId, bool isAdmin, CancellationToken ct = default)
    {
        var block = await db.SeatBlocks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == seatBlockId, ct);
        if (block is null) return ServiceResult<IReadOnlyList<SeatView>>.Fail("not_found");
        if (!await CanManageBlockAsync(actorId, block, isAdmin, ct)) return ServiceResult<IReadOnlyList<SeatView>>.Fail("forbidden");
        return ServiceResult<IReadOnlyList<SeatView>>.Success(await SeatViewsAsync(seatBlockId, ct));
    }

    public async Task<ServiceResult<SeatView>> AssignAsync(Guid actorId, Guid seatId, bool isAdmin, SeatAssignInput input, CancellationToken ct = default)
    {
        var seat0 = await db.SeatBlockSeats.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seatId, ct);
        if (seat0 is null) return ServiceResult<SeatView>.Fail("not_found");
        var block = await db.SeatBlocks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == seat0.SeatBlockId, ct);
        if (block is null || !await CanManageBlockAsync(actorId, block, isAdmin, ct)) return ServiceResult<SeatView>.Fail("forbidden");
        if (block.State != SeatBlockState.Open) return ServiceResult<SeatView>.Fail("block_closed");
        if (block.AssignmentDeadline is { } dl && DateTime.UtcNow > dl) return ServiceResult<SeatView>.Fail("assignment_deadline_passed");
        if (!await db.Users.AnyAsync(u => u.Id == input.PersonId, ct)) return ServiceResult<SeatView>.Fail("invalid_person");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // M3: serialize concurrent (re)assignments of THIS seat so the reassign-limit check + increment are atomic —
        // a transaction-scoped advisory lock; the second caller reads the incremented count and the limit holds.
        var (k1, k2) = SeatLockKey(seatId);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({k1}, {k2})", ct);
        var seat = await db.SeatBlockSeats.FirstAsync(s => s.Id == seatId, ct);   // fresh under the lock
        var adm = await db.Admissions.FirstAsync(a => a.Id == seat.AdmissionId, ct);
        var previousPersonId = adm.PersonId;
        var wasAssigned = seat.AssignedAt is not null && previousPersonId is not null;
        if (wasAssigned && previousPersonId == input.PersonId) return await SeatViewResultAsync(seat, ct);   // no-op
        if (wasAssigned)   // §7.5 rule 3: reassignment is governed
        {
            if (seat.ReassignableUntil is { } ru && DateTime.UtcNow > ru) return ServiceResult<SeatView>.Fail("reassign_window_closed");
            if (seat.ReassignCount >= block.ReassignLimit) return ServiceResult<SeatView>.Fail("reassign_limit_reached");
        }

        var ticket = await db.Tickets.FirstAsync(t => t.Id == adm.TicketId, ct);
        ticket.UserId = input.PersonId;                       // bind the person on the authoritative artifact
        if (input.AnswersJson is not null) ticket.AnswersJson = input.AnswersJson;
        // The projection binds PersonId/credential only at admission CREATION; on an existing admission it re-binds the
        // credential (from ticket.UserId) but not PersonId — so the delegate-assign act sets PersonId explicitly here.
        adm.PersonId = input.PersonId;
        adm.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await registration.ProjectOrderInTransactionAsync(block.OrderId, ct);   // binds the one-per-tree credential + credential-state
        // M1: the prior assignee's credential is no longer referenced by this admission — revoke it if now orphaned.
        if (previousPersonId is { } prev && prev != input.PersonId) await RevokeCredentialIfOrphanedAsync(block.EventId, prev, ct);
        seat.AssignedAt ??= DateTime.UtcNow;
        if (wasAssigned) seat.ReassignCount++;
        seat.UpdatedAt = DateTime.UtcNow;

        // D-292 — a delegate-assigned seat is a real attendee, so it joins the event chat like any other
        // (§17.1: through the outbox, in this transaction, never inline). This path was the one way to
        // hold a ticket without ever reaching the room: a college books fifty seats and assigns them, and
        // all fifty students were missing from the conversation about the event they were attending.
        // On a reassignment the previous holder leaves — the handler re-checks tickets, so it never
        // evicts someone who still holds one of their own.
        EnqueueChatJoin(block.EventId, input.PersonId);
        if (previousPersonId is { } left && left != input.PersonId) EnqueueChatLeave(block.EventId, left);

        db.AuditLogs.Add(Audit(actorId, isAdmin, wasAssigned ? "seatblock.reassign" : "seatblock.assign", seat.Id, JsonSerializer.Serialize(new { personId = input.PersonId, reassignCount = seat.ReassignCount })));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await SeatViewResultAsync(seat, ct);
    }

    public async Task<ServiceResult<SeatView>> UnassignAsync(Guid actorId, Guid seatId, bool isAdmin, CancellationToken ct = default)
    {
        var seat = await db.SeatBlockSeats.FirstOrDefaultAsync(s => s.Id == seatId, ct);
        if (seat is null) return ServiceResult<SeatView>.Fail("not_found");
        var block = await db.SeatBlocks.FirstOrDefaultAsync(b => b.Id == seat.SeatBlockId, ct);
        if (block is null || !await CanManageBlockAsync(actorId, block, isAdmin, ct)) return ServiceResult<SeatView>.Fail("forbidden");
        if (block.State != SeatBlockState.Open) return ServiceResult<SeatView>.Fail("block_closed");
        var adm = await db.Admissions.FirstAsync(a => a.Id == seat.AdmissionId, ct);
        if (adm.PersonId is null) return await SeatViewResultAsync(seat, ct);   // already unassigned
        var previousPersonId = adm.PersonId.Value;

        var ticket = await db.Tickets.FirstAsync(t => t.Id == adm.TicketId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        ticket.UserId = null;                                 // release the person on the authoritative artifact
        adm.PersonId = null;                                  // clear the binding (the projection won't on an existing admission)
        adm.CredentialId = null;                              // drop the person credential link so the projection re-mints a guest placeholder
        adm.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await registration.ProjectOrderInTransactionAsync(block.OrderId, ct);
        await RevokeCredentialIfOrphanedAsync(block.EventId, previousPersonId, ct);   // M1: no orphaned Active credential
        seat.AssignedAt = null;
        seat.UpdatedAt = DateTime.UtcNow;
        // D-292 — the mirror of the join above; the handler leaves them in place if they hold another ticket.
        EnqueueChatLeave(block.EventId, previousPersonId);
        db.AuditLogs.Add(Audit(actorId, isAdmin, "seatblock.unassign", seat.Id, null));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await SeatViewResultAsync(seat, ct);
    }

    public async Task<ServiceResult<SeatBlockStatusView>> StatusAsync(Guid actorId, Guid seatBlockId, bool isAdmin, CancellationToken ct = default)
    {
        var block = await db.SeatBlocks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == seatBlockId, ct);
        if (block is null) return ServiceResult<SeatBlockStatusView>.Fail("not_found");
        if (!await CanManageBlockAsync(actorId, block, isAdmin, ct)) return ServiceResult<SeatBlockStatusView>.Fail("forbidden");
        var seats = await SeatViewsAsync(seatBlockId, ct);
        var incomplete = seats.Where(s => s.PersonId is null).Select(s => s.Id).ToList();
        return ServiceResult<SeatBlockStatusView>.Success(new SeatBlockStatusView(seats.Count, seats.Count - incomplete.Count, incomplete.Count, incomplete));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private async Task<bool> IsOrganiserAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct)
        => isAdmin || await permissions.HasAsync(actorId, eventId, "event:manage", ct);

    private async Task<bool> CanManageBlockAsync(Guid actorId, SeatBlock block, bool isAdmin, CancellationToken ct)
        => isAdmin || block.DelegateUserId == actorId || await permissions.HasAsync(actorId, block.EventId, "event:manage", ct);

    private void tt2Sold(Guid ticketTypeId, int by)
    {
        var tracked = db.ChangeTracker.Entries<TicketType>().FirstOrDefault(e => e.Entity.Id == ticketTypeId)?.Entity;
        if (tracked is not null) { tracked.Sold += by; return; }
        var tt = db.TicketTypes.First(t => t.Id == ticketTypeId);   // attach + bump the legacy mirror
        tt.Sold += by;
    }

    private async Task<IReadOnlyList<SeatView>> SeatViewsAsync(Guid seatBlockId, CancellationToken ct)
    {
        var seats = await db.SeatBlockSeats.AsNoTracking().Where(s => s.SeatBlockId == seatBlockId).OrderBy(s => s.CreatedAt).ToListAsync(ct);
        var admIds = seats.Select(s => s.AdmissionId).ToList();
        var adms = await db.Admissions.AsNoTracking().Where(a => admIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        return seats.Select(s =>
        {
            var a = adms[s.AdmissionId];
            return new SeatView(s.Id, s.SeatBlockId, s.AdmissionId, a.PersonId, s.ReassignCount, s.ReassignableUntil, s.AssignedAt, a.State.ToString());
        }).ToList();
    }

    private async Task<ServiceResult<SeatView>> SeatViewResultAsync(SeatBlockSeat seat, CancellationToken ct)
    {
        var a = await db.Admissions.AsNoTracking().FirstAsync(x => x.Id == seat.AdmissionId, ct);
        return ServiceResult<SeatView>.Success(new SeatView(seat.Id, seat.SeatBlockId, seat.AdmissionId, a.PersonId, seat.ReassignCount, seat.ReassignableUntil, seat.AssignedAt, a.State.ToString()));
    }

    private async Task<SeatBlockView> ToViewAsync(SeatBlock b, CancellationToken ct)
        => ToView(b, await db.SeatBlockSeats.CountAsync(s => s.SeatBlockId == b.Id && s.AssignedAt != null, ct));

    private static SeatBlockView ToView(SeatBlock b, int assignedCount) => new(b.Id, b.EventId, b.TicketTypeId,
        b.RegistrantOrgUnitId, b.PayerId, b.DelegateUserId, b.PaymentMode.ToString(), b.Quantity, b.AssignmentDeadline,
        b.ReassignLimit, b.OrderId, b.State.ToString(), assignedCount);

    // M3: two 32-bit keys for a transaction-scoped Postgres advisory lock, derived from the seat id — serialises
    // concurrent (re)assignments of the same seat so the reassign-limit check and increment are atomic.
    private static (int, int) SeatLockKey(Guid seatId)
        => (BitConverter.ToInt32(seatId.ToByteArray(), 0), BitConverter.ToInt32(seatId.ToByteArray(), 4));

    // D-292: the same two outbox messages OrderService writes for an ordinary registration, for the same
    // reason (§17.1) — a chat join is a non-money side effect and must not run inline inside the seat
    // transaction. Both handlers are idempotent, so the random key suffix never needs to dedup.
    private void EnqueueChatJoin(Guid eventId, Guid userId) => db.OutboxMessages.Add(new OutboxMessage
    {
        Type = "event.chat_join",
        PayloadJson = JsonSerializer.Serialize(new { eventId, userId }),
        IdempotencyKey = $"chat_join:{eventId}:{userId}:{Guid.NewGuid():N}",
    });

    private void EnqueueChatLeave(Guid eventId, Guid userId) => db.OutboxMessages.Add(new OutboxMessage
    {
        Type = "event.chat_leave_if_no_tickets",
        PayloadJson = JsonSerializer.Serialize(new { eventId, userId }),
        IdempotencyKey = $"chat_leave:{eventId}:{userId}:{Guid.NewGuid():N}",
    });

    /// <summary>M1: after a person is unbound from a seat (reassign/unassign), revoke their event-tree credential if no
    /// non-void admission still references it — never leaving an orphaned Active credential, while preserving the row as
    /// history. A credential still used by another admission (e.g. a normal registration) is left Active.</summary>
    private async Task RevokeCredentialIfOrphanedAsync(Guid eventId, Guid personId, CancellationToken ct)
    {
        var parentId = await db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.ParentEventId).FirstOrDefaultAsync(ct);
        var rootEventId = parentId ?? eventId;
        var cred = await db.Credentials.FirstOrDefaultAsync(c => c.EventId == rootEventId && c.PersonId == personId, ct);
        if (cred is null || cred.State == CredentialState.Revoked) return;
        if (!await db.Admissions.AnyAsync(a => a.CredentialId == cred.Id && a.State != AdmissionState.Void, ct))
        {
            cred.State = CredentialState.Revoked;
            cred.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static AuditLog Audit(Guid actorId, bool isAdmin, string action, Guid entityId, string? detailsJson)
        => new() { ActorType = isAdmin ? "admin" : "user", ActorId = actorId, Action = action, Entity = "seat_blocks", EntityId = entityId, DetailsJson = detailsJson };

    private static bool TryEnum(string? s, out DelegatedPaymentMode value, DelegatedPaymentMode current)
    {
        if (string.IsNullOrWhiteSpace(s)) { value = current; return true; }
        return Enum.TryParse(s, true, out value);
    }
}
