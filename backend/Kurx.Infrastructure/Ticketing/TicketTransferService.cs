using System.Security.Cryptography;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Ticketing;

public class TicketTransferService(KurxDbContext db, IWhatsAppLogService waLog, TokenService tokens,
    IChatService chat, IAudienceService audience)
    : ITicketTransferService
{
    public async Task<ServiceResult<TicketTransferView>> InitiateAsync(Guid userId, Guid ticketId, string toPhone, CancellationToken ct = default)
    {
        toPhone = AuthService.NormalizePhone(toPhone);
        if (toPhone.Length is < 8 or > 16) return ServiceResult<TicketTransferView>.Fail("invalid_phone");

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || ticket.UserId != userId) return ServiceResult<TicketTransferView>.Fail("not_found");
        if (ticket.State != TicketState.Issued) return ServiceResult<TicketTransferView>.Fail("ticket_not_transferable");

        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == ticket.EventId, ct);
        if (ev is null || !ev.TransfersEnabled) return ServiceResult<TicketTransferView>.Fail("transfers_disabled");

        // Cancel any existing pending transfer for this ticket before creating a new one
        await db.TicketTransfers
            .Where(t => t.TicketId == ticketId && t.Status == TicketTransferStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketTransferStatus.Cancelled), ct);

        var code = GenerateTransferCode();
        var transfer = new TicketTransfer
        {
            TicketId = ticketId,
            FromUserId = userId,
            ToPhone = toPhone,
            TransferCode = code,
            Status = TicketTransferStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddHours(72),
        };
        db.TicketTransfers.Add(transfer);
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "transfer.create",
            Entity = "ticket_transfer", EntityId = transfer.Id,
        });
        await db.SaveChangesAsync(ct);

        await waLog.SendAndLogAsync(
            toPhone,
            $"You have received a ticket transfer on Kurx. Your claim code is: {code}. It expires in 72 hours.",
            WhatsAppMessageKind.TicketDelivery, "ticket_transfer", transfer.Id, ct: ct);

        return ServiceResult<TicketTransferView>.Success(ToView(transfer));
    }

    public async Task<ServiceResult<TicketTransferView>> ClaimAsync(Guid claimantUserId, string claimantPhone, string transferCode, CancellationToken ct = default)
    {
        claimantPhone = AuthService.NormalizePhone(claimantPhone);
        var now = DateTime.UtcNow;

        var transfer = await db.TicketTransfers.FirstOrDefaultAsync(t => t.TransferCode == transferCode, ct);
        if (transfer is null) return ServiceResult<TicketTransferView>.Fail("not_found");
        if (transfer.Status != TicketTransferStatus.Pending) return ServiceResult<TicketTransferView>.Fail("transfer_not_pending");
        if (transfer.ExpiresAt <= now) return ServiceResult<TicketTransferView>.Fail("transfer_expired");
        // Exact match on the canonical number. This was a "last 10 digits" suffix check, which tolerated the
        // inconsistent formats that existed before normalization was canonical — and quietly matched ACROSS
        // COUNTRIES: a transfer addressed to +91 98765 43210 (stored 919876543210) was claimable by
        // +1 987 654 3210 (stored 19876543210), because the final ten digits are identical. Both sides now
        // come from the same canonical normalizer, so equality is both correct and stricter.
        if (!string.Equals(transfer.ToPhone, claimantPhone, StringComparison.Ordinal))
            return ServiceResult<TicketTransferView>.Fail("phone_mismatch");

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == transfer.TicketId, ct);
        if (ticket is null) return ServiceResult<TicketTransferView>.Fail("not_found");

        // A claim reassigns the ticket to a new person — the same audience gate as any other registration
        // path (V3 §4.4). The claimant must satisfy the event's rule; no rule ⇒ allowed. Uses the shared
        // evaluator so the decision is identical to order/group registration.
        var eligibility = await audience.EvaluateAsync(claimantUserId, ticket.EventId, ct);
        if (!eligibility.Allowed)
        {
            db.AuditLogs.Add(new AuditLog
            {
                ActorType = "user", ActorId = claimantUserId, Action = "audience.register_denied",
                Entity = "events", EntityId = ticket.EventId,
                DetailsJson = $"{{\"reason\":\"{eligibility.Reason}\",\"via\":\"transfer\"}}",
            });
            await db.SaveChangesAsync(ct);
            return ServiceResult<TicketTransferView>.Fail("not_eligible");
        }

        // Rotate QR — old screenshot is dead
        ticket.Code = Guid.NewGuid();
        ticket.HmacSig = tokens.SignTicketCode(ticket.Code);
        ticket.UserId = claimantUserId;

        transfer.Status = TicketTransferStatus.Claimed;
        transfer.ToUserId = claimantUserId;
        transfer.ClaimedAt = now;

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = claimantUserId, Action = "transfer.claim",
            Entity = "ticket_transfer", EntityId = transfer.Id,
        });
        await db.SaveChangesAsync(ct);

        // Chat access follows the ticket. Order matters only for clarity — both calls are idempotent,
        // and the sender is dropped only if this was their last ticket for the event.
        await chat.AddMemberByEventAsync(ticket.EventId, claimantUserId, "Member", ct);
        await chat.RemoveMemberIfNoTicketsAsync(ticket.EventId, transfer.FromUserId, ct);

        return ServiceResult<TicketTransferView>.Success(ToView(transfer));
    }

    public async Task<ServiceResult<bool>> CancelAsync(Guid userId, Guid transferId, CancellationToken ct = default)
    {
        var transfer = await db.TicketTransfers.FirstOrDefaultAsync(t => t.Id == transferId, ct);
        if (transfer is null) return ServiceResult<bool>.Fail("not_found");
        if (transfer.FromUserId != userId) return ServiceResult<bool>.Fail("forbidden");
        if (transfer.Status != TicketTransferStatus.Pending) return ServiceResult<bool>.Fail("transfer_not_pending");

        transfer.Status = TicketTransferStatus.Cancelled;
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "transfer.cancel",
            Entity = "ticket_transfer", EntityId = transfer.Id,
        });
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private static string GenerateTransferCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(8);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private static TicketTransferView ToView(TicketTransfer t) => new(
        t.Id, t.TicketId, t.ToPhone, t.TransferCode, t.Status.ToString(), t.ExpiresAt, t.CreatedAt);
}
