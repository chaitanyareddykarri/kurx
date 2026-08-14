using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Messaging;

public class WhatsAppLogService(KurxDbContext db, IWhatsAppSender sender, ILogger<WhatsAppLogService> log)
    : IWhatsAppLogService
{
    public async Task<Guid> SendAndLogAsync(string toPhone, string message, WhatsAppMessageKind kind,
        string relatedType, Guid? relatedId, object? payload = null, CancellationToken ct = default)
    {
        // Converge on E.164 at the boundary instead of in each caller, so the log row and the wire carry
        // one format whichever column the caller happened to read.
        toPhone = PhoneCanonicalizer.ToE164OrUnchanged(toPhone);

        var msg = new WhatsAppMessage
        {
            ToPhone = toPhone,
            Template = kind.ToString(),
            Kind = kind,
            RelatedType = relatedType,
            RelatedId = relatedId,
            Status = WhatsAppMessageStatus.Queued,
            PayloadJson = payload is not null ? JsonSerializer.Serialize(payload) : null,
        };
        db.WhatsAppMessages.Add(msg);
        await db.SaveChangesAsync(ct);

        try
        {
            await sender.SendTextAsync(toPhone, message, ct);
            msg.Status = WhatsAppMessageStatus.Sent;
            msg.UpdatedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            msg.Status = WhatsAppMessageStatus.Failed;
            msg.Error = ex.Message;
            msg.UpdatedAt = DateTime.UtcNow;
            log.LogWarning(ex, "WhatsApp send failed for {MessageId} to {Phone}", msg.Id, PhoneCanonicalizer.Mask(toPhone));
        }
        await db.SaveChangesAsync(ct);
        return msg.Id;
    }

    public async Task HandleWebhookAsync(string wamid, string newStatus, string? rawPayloadJson, CancellationToken ct = default)
    {
        var msg = await db.WhatsAppMessages.FirstOrDefaultAsync(m => m.Wamid == wamid, ct);
        if (msg is null) return;

        if (Enum.TryParse<WhatsAppMessageStatus>(newStatus, true, out var status))
            msg.Status = status;
        if (rawPayloadJson is not null) msg.PayloadJson = rawPayloadJson;
        msg.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
