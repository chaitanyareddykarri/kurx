namespace Kurx.Domain.Entities;

/// <summary>An account. Identity/handle fields (Phone/Email/Username) are authentication + public
/// handle. Everything from <see cref="Headline"/> down is a SELF-DECLARED display profile with
/// <b>authority zero</b> (M1, D-041): it is never read by any authorization or verification decision.
/// Trust facts live in separate aggregates — platform authority in <c>platform_roles</c> (M2), person
/// identity in <c>user_identity_verifications</c> (M3), affiliation in <c>membership_claims</c> (M6).
/// "Bio is never proof."</summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>LEGACY phone storage: bare digits, country code included but no '+' (e.g. "919876543210"),
    /// historically produced by <c>AuthService.NormalizePhone</c>'s "10 digits ⇒ India" assumption.
    /// Being replaced by <see cref="PhoneE164"/> (D-089); kept and still written during the staged
    /// migration so every legacy client and query keeps working. Do not add new readers.</summary>
    public string Phone { get; set; } = null!;

    // ── Canonical phone identity (D-089, ADR-A6) — additive, nullable until cut-over ──────────
    // Populated for new registrations and backfilled for legacy rows. Null means "not yet migrated",
    // never "no phone", so a null here is a signal to fall back to Phone, not an error.

    /// <summary>Canonical E.164, '+' included (e.g. "+919876543210"). The identifier auth resolves on
    /// once cut-over completes. Unique when set.</summary>
    public string? PhoneE164 { get; set; }

    /// <summary>ISO 3166-1 alpha-2 region the number belongs to ("IN", "US", …). Region is captured at
    /// registration rather than inferred, because E.164 alone cannot always recover it unambiguously.</summary>
    public string? CountryCode { get; set; }

    /// <summary>National (in-country) format for display, e.g. "98765 43210". Presentation only —
    /// never an identifier, never used for lookup.</summary>
    public string? PhoneNational { get; set; }

    public string Name { get; set; } = null!;
    public string? Email { get; set; }

    /// <summary>When the email was proven via an emailed OTP (Phase 2D). Null = unverified/none. A verified
    /// email is a recovery-and-notification channel, never on its own an authentication factor.</summary>
    public DateTime? EmailVerifiedAt { get; set; }

    public string? Username { get; set; }               // lowercase, unique — public handle

    /// <summary>Date of birth, collected during first-time onboarding and required to complete it.
    /// <para><see cref="DateOnly"/>, not <see cref="DateTime"/>: a birth date has no time and no zone, and
    /// storing one as <c>timestamptz</c> shifts the calendar day for anyone east or west of UTC — an Indian
    /// user born on the 1st would read back as the 31st.</para>
    /// <para>Unlike the display fields below this is <b>not</b> authority-zero decoration: age-restricted
    /// events already carry <c>MinAge</c>/<c>MaxAge</c> eligibility, so this is the only self-reported fact
    /// on the account a booking rule can consult. It is still self-declared, never proof of age — that
    /// remains identity verification's job (M3).</para></summary>
    public DateOnly? DateOfBirth { get; set; }

    // ── Display profile — AUTHORITY ZERO (self-declared, never proof) ─────────
    public string? Headline { get; set; }
    public string? Bio { get; set; }
    // Self-declared education. Shown on the public profile as an UNVERIFIED affiliation only.
    // Migrated to evidence-backed membership_claims and dropped in M6 (D-044).
    public string? EducationJson { get; set; }          // jsonb [{institute,degree,branch,start_year,end_year}]
    public string[]? Skills { get; set; }

    /// <summary>Spoken/written languages, self-declared. A real column rather than a key inside
    /// <c>LinksJson</c> or a metadata blob, per the platform convention that every field already
    /// asked for gets a column. Null means "not stated" and never "speaks nothing" — the profile
    /// omits the section rather than asserting an absence.</summary>
    public string[]? Languages { get; set; }

    /// <summary>Self-declared interests. Deliberately distinct from the derived <b>Event DNA</b>,
    /// which is this person's participation tallied by event kind: one is what they say they care
    /// about, the other is what they provably did. Collapsing them would let a claim borrow the
    /// authority of a proof, the same separation <see cref="Headline"/> keeps from the derived
    /// headline (D-225).</summary>
    public string[]? Interests { get; set; }
    public string? LinksJson { get; set; }              // jsonb {github,linkedin,website,instagram}
    public string? AvatarKey { get; set; }
    public string? CoverKey { get; set; }
    // The four original visibility booleans. Still the source of truth for the sections they name
    // until the D-221 dual-write release completes; SectionVisibilityJson overrides them per section.
    public bool ProfilePublic { get; set; } = true;
    public bool ShowAttended { get; set; } = false;
    public bool ShowCertificates { get; set; } = true;
    public bool ShowAllies { get; set; } = true;

    /// <summary>Per-section visibility overrides (D-221) — jsonb <c>{"attended":"only_me", …}</c>, keys
    /// are <see cref="Kurx.Domain.Enums.ProfileSection"/> in snake_case, values
    /// <see cref="Kurx.Domain.Enums.SectionVisibility"/>. Null or an absent key means "fall back to the
    /// boolean above, else the section's documented default" — which is what makes this column
    /// additive and the migration reversible.</summary>
    public string? SectionVisibilityJson { get; set; }
    public DateTime? UsernameChangedAt { get; set; }
    // NOTE: IsKurxAdmin was removed in M1 (D-041). Platform authority is now platform_roles (M2, D-040).

    // ── Account moderation (D-060) — set by platform staff. Both null = active. A set SuspendedAt
    // (reversible hold) or BannedAt (hard block) blocks OTP login and refresh (enforced in AuthService). ──
    public DateTime? SuspendedAt { get; set; }
    public DateTime? BannedAt { get; set; }
    public string? ModerationReason { get; set; }       // reason for the current suspend/ban, shown to staff

    // ── Account settings (D-263) ─────────────────────────────────────────────

    /// <summary>UI language, <c>en</c> or <c>hi</c>. Null means "not chosen" and falls back to the
    /// request's Accept-Language, so an existing user is never silently switched.</summary>
    public string? Language { get; set; }

    // ── Scheduled deletion (D-263, India DPDP) ───────────────────────────────
    // Soft, scheduled and reversible for 30 days. Cancelling is a column write, so nothing has to be
    // undone; a user in grace can still sign in, because otherwise "cancel" is unreachable by the only
    // person entitled to press it.

    public DateTime? DeletionRequestedAt { get; set; }

    /// <summary>When the anonymisation job may run. Non-null = deletion pending.</summary>
    public DateTime? DeletionScheduledFor { get; set; }
    public string? DeletionReason { get; set; }

    /// <summary>Set once the row has actually been anonymised. The row itself is never removed:
    /// orders, ledger entries, certificates and audit logs keep pointing at this id, and their
    /// retention is a legal obligation that erasure does not override (D-263).</summary>
    public DateTime? AnonymizedAt { get; set; }

    /// <summary>When this account was last observed connected (D-295). Written when a realtime
    /// connection drops, so it answers "last seen" for someone who is not online right now — presence
    /// answers "online now" and says nothing once the socket closes.
    ///
    /// <para>Coarse by design: a courtesy signal, not attendance, and never a source for any
    /// authorization or verification decision. Exposure is governed by the same profile visibility
    /// rules as everything else on the person (D-221).</para></summary>
    public DateTime? LastSeenAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Trusted-device auth (AM0, additive/nullable — unused until AM5) ──────
    // Parent device-bound session (ADR-A8); FamilyId on the session is the revoke unit.
    public Guid? SessionId { get; set; }
    // Thumbprint of the device public key that must sign to rotate this token (sender-constrained
    // refresh / proof-of-possession, AM5) — a stolen bearer refresh string is then useless alone.
    public string? PoPKeyThumbprint { get; set; }
}

public class UsernameHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = null!;
    public Guid UserId { get; set; }
    public DateTime ReleasedAt { get; set; }            // reclaimable 30 days after this
}

/// <summary>OTP requests — hashed code + counters powering rate limits and attempt caps.</summary>
public class OtpRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Phone { get; set; } = null!;
    public string CodeHash { get; set; } = null!;
    public string? RequestIp { get; set; }
    public int VerifyAttempts { get; set; }
    public bool Consumed { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string FcmToken { get; set; } = null!;
    public string Platform { get; set; } = null!;       // android | ios
    public string? DeviceName { get; set; }
    public string? AppVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Kind { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string? DataJson { get; set; }               // includes deep_link

    /// <summary>Idempotency identity for the notifications that must not repeat (DB-3).
    ///
    /// <para><b>Nullable on purpose, and null for almost everything.</b> Only two kinds dedup:
    /// <c>event_announcement</c> (one per recipient per announcement) and <c>EventReminder</c> (one per
    /// recipient per event). Every other kind legitimately repeats — an event can change materially many
    /// times, an invitation can be re-sent after a decline, an ally request may be re-made after D-230's
    /// 30-day cooldown, an authorization can be resubmitted and re-reviewed. A blanket uniqueness rule
    /// over (UserId, Kind, subject) would silently swallow all four.</para>
    ///
    /// <para>The uniqueness is a PARTIAL unique index — <c>(UserId, DedupKey) WHERE DedupKey IS NOT
    /// NULL</c> — so rows leaving this null are unconstrained, now and for every kind added later.</para>
    ///
    /// <para>Format is <c>{kind}:{subjectId}</c>: deterministic, readable in psql, and derivable from the
    /// business facts rather than an opaque hash whose meaning is lost the moment it needs debugging.
    /// Built by <c>NotificationDedup</c>.</para></summary>
    public string? DedupKey { get; set; }

    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ActorType { get; set; } = null!;      // user | admin | system
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = null!;
    public string Entity { get; set; } = null!;
    public Guid? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Append-only audit trail of username changes. Separate from username_history which
/// tracks the 30-day availability cooldown for released usernames.</summary>
public class UsernameChangeLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string? OldUsername { get; set; }            // null on first username claim
    public string NewUsername { get; set; } = null!;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Polymorphic content moderation reports submitted by users.</summary>
public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterId { get; set; }
    public string EntityType { get; set; } = null!;    // event | review | chat_message | user | org | post | post_comment
    public Guid EntityId { get; set; }
    public string Reason { get; set; } = null!;
    public string? Details { get; set; }
    public string Status { get; set; } = "open";       // open | reviewed | resolved | dismissed
    public Guid? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
