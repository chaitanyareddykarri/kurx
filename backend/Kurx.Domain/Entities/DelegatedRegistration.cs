using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>A SeatBlock (V3 §7.5, Phase 13) — delegated registration: an org unit reserves <c>Quantity</c> seats
/// against a Pass before the people are known. Seats are bought via the <b>authoritative Order/Ticket money path</b>
/// (<see cref="OrderId"/>), which mints <see cref="Quantity"/> <b>unassigned</b> Admissions (PersonId null). The
/// <see cref="DelegateUserId"/> persona then binds people through the delegate console, governed by
/// <see cref="AssignmentDeadline"/> + <see cref="ReassignLimit"/> + a per-assignment audit. Payment is data/authz only
/// (§9.7): FREE or DEFERRED (invoice to the org via <see cref="PayerId"/>); no live collection this phase.</summary>
public class SeatBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }             // the Pass analog
    public Guid RegistrantOrgUnitId { get; set; }      // who registers — the college / company (§7.5)
    public Guid? PayerId { get; set; }                 // may differ from the registrant (a user); org is invoiced (§9.7)
    public Guid DelegateUserId { get; set; }           // the third persona: runs the delegate console
    public DelegatedPaymentMode PaymentMode { get; set; } = DelegatedPaymentMode.Free;
    public int Quantity { get; set; }
    public DateTime? AssignmentDeadline { get; set; }
    public int ReassignLimit { get; set; }             // per-seat reassignment cap (§7.5 rule 3)
    public Guid OrderId { get; set; }                  // the authoritative funding order (reused money path)
    public SeatBlockState State { get; set; } = SeatBlockState.Open;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One seat within a <see cref="SeatBlock"/> (V3 §7.5 <c>seats[]</c>). Governs a single (initially unassigned)
/// <see cref="Admission"/>; the person it is assigned to is the admission's <c>PersonId</c> (null = UNASSIGNED).
/// Reassignment is governed by <see cref="ReassignableUntil"/> and <see cref="ReassignCount"/> vs the block's limit,
/// with every (re)assignment written to the audit spine.</summary>
public class SeatBlockSeat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeatBlockId { get; set; }
    public Guid AdmissionId { get; set; }              // the admission this seat governs (unique)
    public int ReassignCount { get; set; }
    public DateTime? ReassignableUntil { get; set; }
    public DateTime? AssignedAt { get; set; }          // null while UNASSIGNED
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
