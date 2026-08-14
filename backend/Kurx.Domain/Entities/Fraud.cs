using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>A hard block on a known-bad identifier (M13, D-052). Value is stored normalized (phone
/// E.164, email lowercased, org name normalized) so lookups are exact. Enforced at the trust boundary
/// (a blacklisted user's phone/email fails the fraud-clear check) and at org creation (blacklisted name).</summary>
public class BlacklistEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public BlacklistKind Kind { get; set; }
    public string Value { get; set; } = null!;          // normalized
    public string? Reason { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A fraud signal contributing to a subject's risk score (M13, D-052). Polymorphic over the
/// same subjects as the verification substrate (UserIdentity/Organization/Membership/Event). The sum of
/// a subject's signal scores is its risk; above a threshold the subject is not fraud-clear.</summary>
public class FraudSignal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public VerificationSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public FraudSignalKind Kind { get; set; }
    public string? Value { get; set; }
    public int Score { get; set; }                      // contribution to the subject's risk
    public string? DetailsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
