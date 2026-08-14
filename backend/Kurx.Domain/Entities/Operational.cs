using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

public class WhatsAppMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ToPhone { get; set; } = null!;
    public string Template { get; set; } = null!;
    public WhatsAppMessageKind Kind { get; set; }
    public string? Wamid { get; set; }                    // unique when present; null until sent
    public WhatsAppMessageStatus Status { get; set; } = WhatsAppMessageStatus.Queued;
    public string? Error { get; set; }
    public string RelatedType { get; set; } = null!;      // order | ticket | event | group | certificate
    public Guid? RelatedId { get; set; }
    public string? PayloadJson { get; set; }              // jsonb — raw send request + last webhook body
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SeatHold
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketTypeId { get; set; }
    /// <summary>The authoritative pool this hold reserves against (V3 §17.1, Phase 9). The hold increments
    /// <c>InventoryPool.Held</c>; capture converts held→consumed, expiry releases it. Null on legacy rows.</summary>
    public Guid? PoolId { get; set; }
    public Guid OrderId { get; set; }
    public int Qty { get; set; }
    public SeatHoldStatus Status { get; set; } = SeatHoldStatus.Active;
    public DateTime ExpiresAt { get; set; }               // created_at + 10 min
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TicketTransfer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Guid FromUserId { get; set; }
    public string ToPhone { get; set; } = null!;
    public Guid? ToUserId { get; set; }
    public string TransferCode { get; set; } = null!;     // unique 8-char
    public TicketTransferStatus Status { get; set; } = TicketTransferStatus.Pending;
    public DateTime ExpiresAt { get; set; }               // created_at + 72 hours
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAt { get; set; }
}

public class GateEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Guid EventId { get; set; }
    public Guid ScannedBy { get; set; }
    public string? DeviceInfo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // scan time
}
