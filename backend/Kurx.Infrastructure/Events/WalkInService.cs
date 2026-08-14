using System.Security.Cryptography;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Walk-in registration (V3 §7.6, Phase 13). A staff participant registers an attendee at the gate against the
/// <c>WalkIn</c>-segment pool; the attendee is a guest (identity NONE/CONTACT). It creates an authoritative Order +
/// Ticket, then reuses <see cref="IEventRegistrationService.ProjectOrderInTransactionAsync"/> — the sole minting path —
/// to produce Registration+Admission+Credential, correcting the admission's pool to the WalkIn pool it consumed.
/// Offline-safe: a staff-scoped idempotency key makes a replayed queued walk-in return the original, never a duplicate.</summary>
public class WalkInService(KurxDbContext db, TokenService tokens, IInventoryService inventory,
    IEventRegistrationService registration, IEventPermissionService permissions) : IWalkInService
{
    private static readonly ParticipantState[] ActiveStates = [ParticipantState.Accepted, ParticipantState.Active];

    public async Task<ServiceResult<WalkInView>> CreateAsync(Guid staffUserId, Guid eventId, bool isAdmin, WalkInInput input, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<WalkInView>.Fail("not_found");
        if (!await IsStaffAsync(staffUserId, eventId, isAdmin, ct)) return ServiceResult<WalkInView>.Fail("forbidden");
        var tt = await db.TicketTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == input.TicketTypeId && t.EventId == eventId, ct);
        if (tt is null) return ServiceResult<WalkInView>.Fail("invalid_ticket_type");
        var walkInPool = await db.InventoryPools.FirstOrDefaultAsync(p => p.TicketTypeId == tt.Id && p.Segment == InventorySegment.WalkIn, ct);
        if (walkInPool is null) return ServiceResult<WalkInView>.Fail("no_walkin_pool");   // §7.6 draws from segment=walk_in

        // Offline reconcile (§7.6): a staff-scoped idempotency key — a replayed queued walk-in returns the original.
        var key = string.IsNullOrWhiteSpace(input.IdempotencyKey) ? null : $"walkin:{staffUserId}:{input.IdempotencyKey.Trim()}";
        if (key is not null)
        {
            var prior = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.EventId == eventId && o.IdempotencyKey == key, ct);
            if (prior is not null) return await BuildViewAsync(prior.Id, ct);
        }

        var amount = input.Free || tt.PricePaise <= 0 ? 0 : tt.PricePaise;   // free or at-gate cash (no gateway)
        var guestPhone = string.IsNullOrWhiteSpace(input.GuestPhone) ? null : AuthService.NormalizePhone(input.GuestPhone!);
        var order = new Order
        {
            UserId = null, EventId = eventId, TicketTypeId = tt.Id, Status = OrderStatus.Paid,
            AmountPaise = amount, Currency = ev.SettlementCurrency,
            GuestName = string.IsNullOrWhiteSpace(input.GuestName) ? null : input.GuestName!.Trim(),
            GuestPhone = guestPhone,
            GuestEmail = string.IsNullOrWhiteSpace(input.GuestEmail) ? null : input.GuestEmail!.Trim(),
            GuestAccessToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
            IdempotencyKey = key,
        };
        var orderItem = new OrderItem { OrderId = order.Id, TicketTypeId = tt.Id, Qty = 1, UnitPricePaise = amount, Currency = ev.SettlementCurrency };
        var code = Guid.NewGuid();
        var ticket = new Ticket { OrderItemId = orderItem.Id, EventId = eventId, UserId = null, Code = code, HmacSig = tokens.SignTicketCode(code) };

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Conditional CONSUME (§17.1) from the WalkIn pool — the same authority the money path uses.
            if (!await inventory.TryConsumeManyAsync([new PoolDraw(walkInPool.Id, 1)], ct)) return ServiceResult<WalkInView>.Fail("sold_out");
            db.Orders.Add(order);
            db.OrderItems.Add(orderItem);
            db.Tickets.Add(ticket);
            db.AuditLogs.Add(new AuditLog { ActorType = isAdmin ? "admin" : "user", ActorId = staffUserId, Action = "walkin.create", Entity = "orders", EntityId = order.Id, DetailsJson = JsonSerializer.Serialize(new { eventId, ticketTypeId = tt.Id }) });
            await db.SaveChangesAsync(ct);
            await registration.ProjectOrderInTransactionAsync(order.Id, ct);   // reg + admission + credential + VAR (authoritative)
            // The projection defaults the admission's pool to General; correct it to the WalkIn pool actually consumed
            // so §17.1 reconciliation (Consumed == count(active admissions on the pool)) stays exact.
            var adm = await db.Admissions.FirstAsync(a => a.TicketId == ticket.Id, ct);
            adm.PoolId = walkInPool.Id;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost the idempotency race (guest-phone unique index) — return the order the winner wrote.
            if (key is not null)
            {
                var winner = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.EventId == eventId && o.IdempotencyKey == key, ct);
                if (winner is not null) return await BuildViewAsync(winner.Id, ct);
            }
            throw;
        }
        return await BuildViewAsync(order.Id, ct);
    }

    private async Task<ServiceResult<WalkInView>> BuildViewAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId, ct);
        var reg = await db.Registrations.AsNoTracking().FirstAsync(r => r.OrderId == orderId, ct);
        var adm = await db.Admissions.AsNoTracking().FirstAsync(a => a.RegistrationId == reg.Id, ct);
        var cred = adm.CredentialId is { } cid ? await db.Credentials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, ct) : null;
        return ServiceResult<WalkInView>.Success(new WalkInView(orderId, reg.Id, adm.Id, adm.CredentialId, cred?.Code ?? Guid.Empty, order.GuestAccessToken));
    }

    private async Task<bool> IsStaffAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct)
    {
        if (isAdmin) return true;
        if (await permissions.HasAsync(userId, eventId, "event:manage", ct)) return true;
        var opsSlugs = await db.ParticipantRoles.AsNoTracking().Where(r => r.Class == ParticipantClass.Operations).Select(r => r.Slug).ToListAsync(ct);
        return await db.EventParticipants.AsNoTracking().AnyAsync(ep => ep.EventId == eventId
            && ep.SubjectType == ParticipantSubjectType.Person && ep.SubjectId == userId
            && ActiveStates.Contains(ep.State) && opsSlugs.Contains(ep.RoleSlug), ct);
    }
}
