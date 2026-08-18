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

/// <summary>A staff member's arrival, recorded when their badge's signed pass is scanned (D-385).
///
/// <para><b>Its own table, not a nullable column on <see cref="GateEntry"/>.</b> That row's
/// <c>TicketId</c> is non-nullable and every attendance figure on the platform counts it, so admitting
/// staff through it would inflate attendee check-in counts with people who never bought anything. Staff
/// arrivals and attendee admissions are two different measurements; D-362 named this table as the
/// remaining step and deferred it only because another session held an uncommitted migration.</para>
///
/// <para>Keyed on the <see cref="EventAssignment"/> rather than the user: the assignment is what the badge
/// encodes, what carries the role the badge prints, and what revocation acts on.</para></summary>
public class StaffGateEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The accepted <c>EventAssignment</c> the scanned pass resolved to.</summary>
    public Guid AssignmentId { get; set; }

    /// <summary>The event the scan happened at. Equal to the assignment's own event by the time a row is
    /// written — the scan is refused otherwise — but stored so the arrivals list for an event is one index
    /// away rather than a join.</summary>
    public Guid EventId { get; set; }

    public Guid ScannedBy { get; set; }
    public string? DeviceInfo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
