using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>A person's identity-verification aggregate (1:1 with <see cref="User"/>, M3, D-042). Distinct
/// from organization bank verification (M9, <c>org_bank_verifications</c>) — this is KYC of a *person*.
///
/// <para>Only masked last-4 values are ever persisted (mirrors D-016) — the full government-ID / PAN /
/// account number is passed to the provider and never stored. Component presence (the *Last4 fields)
/// is what capability gates read (M7); <see cref="Level"/> is a coarse display summary.</para>
///
/// <para>Provider calls go through the existing <c>IKycProvider</c> (mock in dev, like org KYC); the
/// production DigiLocker/PAN/penny-drop adapter is a gated integration task (see D-042).</para></summary>
public class UserIdentity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                        // unique — one row per user

    public IdentityLevel Level { get; set; } = IdentityLevel.Phone;   // highest rung achieved (display)
    public IdentityStatus Status { get; set; } = IdentityStatus.NotStarted;

    // Masked evidence — presence means that component is verified. Never the full value.
    public string? GovtIdKind { get; set; }                 // digilocker | aadhaar_offline | passport | dl (open)
    public string? GovtIdLast4 { get; set; }
    public string? PanLast4 { get; set; }
    public string? BankLast4 { get; set; }                  // person's own account (individual payouts)

    // ── Per-component status ──────────────────────────────────────────────────
    // <see cref="Status"/> is the aggregate and was the ONLY status, which made it wrong: every
    // submission wrote it, so one failed bank check flipped a person with an approved government ID
    // and PAN to `Rejected` overall, and the UI could only guess a component's state from whether
    // its masked value happened to be present. A component's state is a property of that component.
    public IdentityStatus GovtIdStatus { get; set; } = IdentityStatus.NotStarted;
    public IdentityStatus PanStatus { get; set; } = IdentityStatus.NotStarted;
    public IdentityStatus BankStatus { get; set; } = IdentityStatus.NotStarted;

    // ── Bank proof detail ─────────────────────────────────────────────────────
    /// <summary>Outcome of the penny-drop test. The provider call has always been made
    /// (<c>IKycProvider.PennyDropAsync</c>); its result was collapsed into the aggregate status and
    /// the distinction lost, so "we credited ₹1 and it landed" was indistinguishable from "someone
    /// typed an account number".</summary>
    public PennyDropStatus PennyDropStatus { get; set; } = PennyDropStatus.NotStarted;

    /// <summary>Did the bank's registered account-holder name match? Previously the holder name was
    /// sent to the provider and the answer thrown away, so a PAN and a bank account belonging to two
    /// different people passed silently.</summary>
    public NameMatchStatus BankNameMatch { get; set; } = NameMatchStatus.NotChecked;

    /// <summary>When the bank account last passed a penny drop. Null while unproven.</summary>
    public DateTime? BankVerifiedAt { get; set; }

    public string? ProviderRefsJson { get; set; }           // jsonb — provider request/response refs (no PII)
    public int RiskScore { get; set; }                      // snapshot; fed by M13
    public Guid? ReviewedBy { get; set; }                   // null = automated/provider decision
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }                // e.g. government-ID expiry → renewal
    public int SubmitCount { get; set; }                    // retry counter (rate-limits resubmission)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
