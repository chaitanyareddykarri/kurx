using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>An identity card issued BY a verified organization TO one of its verified members (D-331).
///
/// <para><b>Why the issuer is on the row.</b> The card's whole value is that a third party can trust a
/// claim its holder makes about themselves. That is only true if the claim came from somewhere the
/// platform already trusts, so <see cref="OrgId"/> is not decoration — it is the authority the card
/// leans on, and issuance checks the org is <c>Verified</c> and the holder's membership
/// <c>IsVerified</c> at the moment of issue. A card whose identifiers the holder typed would be a
/// forgery template; see D-331 for why the self-declared alternative was rejected.</para>
///
/// <para><b>Asserted vs. supplied fields.</b> <see cref="StudentId"/>, <see cref="Department"/>,
/// <see cref="Course"/>, <see cref="Year"/> and the validity window are ASSERTED by the issuer and are
/// not holder-editable — that is what makes "protected identifier" mean something. Blood group, address
/// and emergency contact are SUPPLIED by the holder, optional, and never required to issue a card:
/// blood group is health data and an emergency contact is a third party's personal data, so neither is
/// inferred, and neither is ever exposed by the public verification page.</para>
///
/// <para><b>Event badges reuse this row.</b> A badge is the same document with <see cref="EventId"/>
/// set and the institutional fields null — issued against a ticket by the event's organizer, which is
/// an authority the platform holds, and asserting no college affiliation.</para></summary>
public class IdCard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The issuing organization. Never null: an unissued card is not a card (D-331).</summary>
    public Guid OrgId { get; set; }

    /// <summary>The holder.</summary>
    public Guid UserId { get; set; }

    /// <summary>Set for an event badge, null for a college ID. The two differ in what they assert, not
    /// in how they are rendered or verified.</summary>
    public Guid? EventId { get; set; }

    /// <summary>Unique code printed on the card and encoded in the QR. Same 10-char base32 shape as
    /// <see cref="Certificate.VerifyCode"/> so one verification convention covers both.</summary>
    public string VerifyCode { get; set; } = null!;

    /// <summary>Human-facing card number, unique per issuing org. Distinct from <see cref="StudentId"/>:
    /// a reissue after a loss gets a new card number while the student ID is unchanged, which is what
    /// lets a revoked card be told apart from its replacement.</summary>
    public string CardNumber { get; set; } = null!;

    // ── Asserted by the issuer. Not holder-editable (D-331). ────────────────────────────────────
    public string? StudentId { get; set; }
    public string? Department { get; set; }
    public string? Course { get; set; }
    public string? Year { get; set; }

    /// <summary>Validity window. <see cref="ValidUntil"/> drives <see cref="IdCardStatus.Expired"/>,
    /// which is derived at read time rather than stored — a stored status would need a sweeper and would
    /// be wrong between runs.</summary>
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }

    // ── Supplied by the holder. Optional, never inferred, never publicly exposed (D-331). ───────
    public string? BloodGroup { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    // ── Presentation. Holder-editable; affects the rendered card only. ──────────────────────────
    public IdCardTemplate Template { get; set; } = IdCardTemplate.StandardCollege;

    /// <summary>Storage key of the photo printed on the card. Defaults to the holder's
    /// <c>User.AvatarKey</c> at issue time but is snapshotted, so changing an avatar later does not
    /// silently reprint every card.</summary>
    public string? PhotoKey { get; set; }

    /// <summary><b>Per-card logo and signature are NOT IMPLEMENTED (verified 2026-08-18, D-386).</b>
    /// Nothing reads or writes either; both are null on every row.
    ///
    /// <para>The logo that prints comes from the <i>event's</i> design — <c>IdCardTemplateSpec.LogoKey</c>
    /// (D-362) — because the mark on a badge is the organiser's, not the holder's, so one key per event is
    /// the right shape and a key per card would be two hundred copies of it. A signature has no field key
    /// in <c>IdCardField.Keys</c> and therefore nowhere on the card to go.</para>
    ///
    /// <para>Kept as columns only because dropping them is a destructive migration needing its own
    /// decision.</para></summary>
    public string? LogoKey { get; set; }
    public string? SignatureKey { get; set; }

    /// <summary><b>NOT IMPLEMENTED — nothing reads or writes this (verified 2026-08-18, D-386).</b>
    ///
    /// <para>Intended as per-card element overrides from the editor. D-362 then made the design
    /// <i>event-level</i> — one <c>DesignTemplate</c> with <c>Kind = IdCard</c>, which is what
    /// <c>IdCardService.SpecAsync</c> reads and the only thing that shapes a rendered badge. Per-card
    /// overrides were never built and are not currently wanted: an organiser designs the card once and
    /// prints many, and a per-card layout is how two people at the same door end up holding visibly
    /// different credentials.</para>
    ///
    /// <para>Kept as a column only because dropping it is a destructive migration that needs its own
    /// decision. Do not read it — it is null on every row.</para></summary>
    public string? LayoutJson { get; set; }

    /// <summary><b>NOT IMPLEMENTED — nothing reads or writes this (verified 2026-08-18, D-386).</b>
    ///
    /// <para>Intended (D-334 §8) to print the holder's meal entitlement on the card, read live from their
    /// entitlement grants at generation time. Neither half exists: there is no meal key in
    /// <c>IdCardField.Keys</c>, so the layout has nowhere to put it, and no path sets the flag. It is
    /// <c>false</c> on every row and reads as "no meal info" — which happens to be correct, but by
    /// accident rather than by design.</para>
    ///
    /// <para>Kept as a column only because dropping it is a destructive migration that needs its own
    /// decision. Building it means a field key, a layout slot and a grant lookup — a feature, not a
    /// wiring job.</para></summary>
    public bool ShowMealInfo { get; set; }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────────
    public IdCardStatus Status { get; set; } = IdCardStatus.Draft;

    /// <summary>Whether the verification page resolves this card. False hides it from lookup entirely;
    /// revocation does NOT — a revoked card stays visible so the revocation can be confirmed (D-036).</summary>
    public bool IsPublic { get; set; } = true;

    public bool IsRevoked { get; set; }
    public string? RevokedReason { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }

    /// <summary>Storage keys of the last generated artefacts. Null until generation runs; regenerating
    /// overwrites them, and <see cref="GeneratedAt"/> is the timestamp the card prints.</summary>
    public string? PdfKey { get; set; }
    public string? PngKey { get; set; }
    public DateTime? GeneratedAt { get; set; }

    /// <summary>Who issued it, and who last changed it. The fuller who-changed-what trail lives in
    /// <c>audit_logs</c>; these two are on the row because every read of a card wants them.</summary>
    public Guid IssuedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Status as a verifier should see it: a card past <see cref="ValidUntil"/> reads as
    /// expired regardless of the stored status, so an un-swept row never verifies as current.</summary>
    public IdCardStatus EffectiveStatus(DateOnly today) =>
        IsRevoked ? IdCardStatus.Revoked
        : ValidUntil is { } until && until < today ? IdCardStatus.Expired
        : Status;
}
