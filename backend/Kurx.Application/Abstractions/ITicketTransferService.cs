namespace Kurx.Application.Abstractions;

public record TicketTransferView(
    Guid Id, Guid TicketId, string ToPhone, string TransferCode,
    string Status, DateTime ExpiresAt, DateTime CreatedAt);

public interface ITicketTransferService
{
    Task<ServiceResult<TicketTransferView>> InitiateAsync(Guid userId, Guid ticketId, string toPhone, CancellationToken ct = default);
    Task<ServiceResult<TicketTransferView>> ClaimAsync(Guid claimantUserId, string claimantPhone, string transferCode, CancellationToken ct = default);
    Task<ServiceResult<bool>> CancelAsync(Guid userId, Guid transferId, CancellationToken ct = default);
}
