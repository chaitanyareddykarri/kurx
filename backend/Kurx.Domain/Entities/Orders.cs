using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }                   // null = guest order (no Kurx account, D-036)
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public long AmountPaise { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;       // ISO-4217; the event's settlement currency (V3 §9.1)
    public string? RazorpayOrderId { get; set; }
    public string? AnswersJson { get; set; }            // per_registration answers
    // Guest checkout (UserId null): captured directly, no User row.
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestAccessToken { get; set; }       // unique 12-char, only set for guest orders
    /// <summary>Client-supplied idempotency key (V3 §17.1, Phase 9). Unique per (buyer, event) — a retried
    /// create returns the original order instead of consuming inventory twice. Null = no key supplied.</summary>
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid TicketTypeId { get; set; }
    public int Qty { get; set; }
    public long UnitPricePaise { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;       // ISO-4217 (V3 §9.1)
}

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }      // unique when present
    public string Method { get; set; } = "";
    public string Status { get; set; } = "created";
    public DateTime? CapturedAt { get; set; }
    public string? WebhookPayloadJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Refund
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public long AmountPaise { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;       // ISO-4217 (V3 §9.1)
    public string Reason { get; set; } = "";
    public RefundStatus Status { get; set; } = RefundStatus.Initiated;
    public string? RazorpayRefundId { get; set; }       // unique when present
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public Guid OrderId { get; set; }
    public int GroupNumber { get; set; }                // auto per event: 1, 2, 3…
    public string? DisplayName { get; set; }
    public string JoinCode { get; set; } = null!;       // unique 6-char
    public Guid LeaderUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GroupMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid? UserId { get; set; }                   // set when they join
    public string Name { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public Guid? TicketId { get; set; }
    public string? AnswersJson { get; set; }            // per_participant answers
    public DateTime? JoinedAt { get; set; }
}

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderItemId { get; set; }
    public Guid EventId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? GroupMemberId { get; set; }
    public Guid Code { get; set; } = Guid.NewGuid();    // unique QR payload
    public string HmacSig { get; set; } = null!;
    public TicketState State { get; set; } = TicketState.Issued;
    public string? AnswersJson { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public Guid? CheckedInBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
