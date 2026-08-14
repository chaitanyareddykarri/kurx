using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

public interface IWhatsAppLogService
{
    Task<Guid> SendAndLogAsync(string toPhone, string message, WhatsAppMessageKind kind,
        string relatedType, Guid? relatedId, object? payload = null, CancellationToken ct = default);

    Task HandleWebhookAsync(string wamid, string newStatus, string? rawPayloadJson, CancellationToken ct = default);
}
