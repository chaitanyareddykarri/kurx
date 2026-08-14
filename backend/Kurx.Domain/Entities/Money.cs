using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

public class Transfer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public Guid OrgId { get; set; }
    public string? RazorpayTransferId { get; set; }     // unique when present
    public long AmountPaise { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;       // ISO-4217 (V3 §9.1)
    public TransferStatus Status { get; set; } = TransferStatus.OnHold;
    public DateTime? HoldUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Append-only. States: collected → available → advanced → reserved → settled.</summary>
public class LedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public Guid EventId { get; set; }
    public long AmountPaise { get; set; }               // signed; a state's balance = sum of its entries
    public string Currency { get; set; } = Money.DefaultCurrency;       // ISO-4217 (V3 §9.1)
    public LedgerState State { get; set; }
    public string RefType { get; set; } = null!;        // payment | refund | advance | reserve | settlement | chargeback
    public Guid RefId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Withdrawal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public long AmountPaise { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;       // ISO-4217 (V3 §9.1)
    public string Status { get; set; } = "requested";   // requested | processing | paid | rejected
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
