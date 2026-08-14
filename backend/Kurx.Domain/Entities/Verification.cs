using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

// ── Trust & Verification substrate (M0) ─────────────────────────────────────
// The two shared tables every verification subsystem writes to. Kept deliberately
// polymorphic (SubjectType + SubjectId) so identity KYC, organization verification,
// membership verification, and event approval all reuse one evidence + one review
// table instead of building three bespoke review systems. Profile bio is never an
// input here — evidence is always an explicit document or an explicit review row.

/// <summary>A single piece of evidence backing a verification subject. The file itself lives in
/// private storage (only its key is persisted, presigned per view); this row holds the key, a
/// content hash for forgery/duplicate detection (M13), and any OCR/provider-extracted fields.
/// SubjectId is polymorphic across users/organizations/memberships/events — no hard FK.</summary>
public class VerificationDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public VerificationSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    /// <summary>Open vocabulary (subject-specific), e.g. govt_id, pan, college_letter,
    /// registration_cert, domain_proof. String, not an enum, for extensibility per org type.</summary>
    public string DocType { get; set; } = null!;
    public string StorageKey { get; set; } = null!;         // private-bucket object key
    public string? Sha256 { get; set; }                     // content hash (forgery/dup detection, M13)
    public string? ExtractedJson { get; set; }              // jsonb — OCR/provider-extracted fields
    public VerificationDocumentStatus Status { get; set; } = VerificationDocumentStatus.Pending;
    public Guid UploadedBy { get; set; }                    // FK → users
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Append-only decision record for any verification subject. The audit spine of the admin
/// verification console (M12): every approve/reject/request-changes transition in identity (M3),
/// organization (M5), and membership (M6) verification writes one of these. ReviewerId is null for
/// automated/system decisions.</summary>
public class VerificationReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public VerificationSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public VerificationDecision Decision { get; set; }
    public Guid? ReviewerId { get; set; }                   // FK → users; null = system/automated
    public string? ReasonCode { get; set; }                 // controlled reason vocabulary
    public string? Notes { get; set; }                      // reviewer note (admin-only, never public)
    public int? RiskScore { get; set; }                     // 0..100 snapshot at decision time (M13)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
