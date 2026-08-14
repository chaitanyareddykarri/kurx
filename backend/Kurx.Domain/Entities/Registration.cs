using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>The five-axis registration policy (V3 §7.1, Phase 8) — a composable policy per Pass; in Phase 8 a
/// Pass is a <see cref="TicketType"/>, so exactly one policy shadows each ticket type, derived from its current
/// config and settable by the organiser. Enforcement stays on the authoritative Order/Ticket path this phase;
/// gate/allocation values that need later subsystems (LOTTERY draw, PREREQUISITE eval, TEAM, DELEGATED) are
/// stored-but-not-enforced until the Phase 9 cut-over.</summary>
public class RegistrationPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }              // the Pass analog (Phase 8); unique

    public RegistrationSubject Subject { get; set; } = RegistrationSubject.Person;
    public string? GatesJson { get; set; }              // jsonb RegistrationGate[] — composable, declared order; default [Open]
    public IdentityRequirement IdentityRequirement { get; set; } = IdentityRequirement.Account;
    public Guid? FormId { get; set; }                   // intake — the form (FormFields live on the ticket type today)
    public string? DocumentsRequiredJson { get; set; }  // jsonb string[]
    public Guid? ApplicationId { get; set; }
    public AllocationPolicy Allocation { get; set; } = AllocationPolicy.Fcfs;
    public PaymentPolicy Payment { get; set; } = PaymentPolicy.Free;

    // Windows (§7.1). Opens/Closes mirror the ticket type's sale window; the rest are organiser-set.
    public DateTime? OpensAt { get; set; }
    public DateTime? ClosesAt { get; set; }
    public int? LateWindowMinutes { get; set; }
    public DateTime? EditUntil { get; set; }
    public DateTime? CancelUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>The ACT (V3 §7.2, Phase 8): a subject secured participation — holds state, form answers, payment.
/// One Registration shadows one <see cref="Order"/>. A party booking is one Registration producing N Admissions,
/// never a group entity (§6.1). Subject is Person in Phase 8 (Team is Phase 10). Order remains authoritative;
/// this is the dual-write shadow projected post-commit and reconciled.</summary>
public class Registration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public RegistrationSubject SubjectType { get; set; } = RegistrationSubject.Person;
    public Guid? SubjectId { get; set; }                // the person's user id; null for a guest (CONTACT identity)
    public Guid OrderId { get; set; }                   // shadow link to the authoritative order; unique
    public RegistrationState State { get; set; } = RegistrationState.Pending;
    public string? AnswersJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>The RIGHT (V3 §7.2, Phase 8): one person's right of entry, consuming person-inventory. One Admission
/// shadows one <see cref="Ticket"/>; it references the inventory pool the ticket consumed (Phase 7) and the
/// person's <see cref="CredentialId"/>.</summary>
public class Admission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RegistrationId { get; set; }
    public Guid EventId { get; set; }
    public Guid? PersonId { get; set; }                 // the ticket holder; null for guest/unassigned
    public Guid TicketId { get; set; }                  // shadow link to the authoritative ticket; unique
    public Guid? PoolId { get; set; }                   // the inventory pool consumed (Phase 7)
    public Guid? CredentialId { get; set; }
    public AdmissionState State { get; set; } = AdmissionState.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>The ARTIFACT (V3 §7.2, Phase 8): the scannable identity — ONE per person per event tree. Multiple
/// admissions for the same person in one event share one credential. Guests (no account) get one credential per
/// admission. The scalar Ticket.Code remains the authoritative scannable this phase; this shadows it.</summary>
public class Credential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid? PersonId { get; set; }                 // null for a guest credential (per-admission, no dedup)
    public Guid Code { get; set; }                      // mirrors the ticket's scannable code
    public CredentialState State { get; set; } = CredentialState.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
