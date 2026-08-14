using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

// ── The Professional Identity fact-set (D-224) ──────────────────────────────
//
// One materialisation of everything Kurx has verified about one person, loaded once and projected by
// every derivation engine. Before this, each section re-queried the same tables independently — which
// was fine at four sections, and is untenable at ten. It is also the only way the Resume, which needs
// every section at once, can be a single load.
//
// Two properties make the design work:
//
//   1. It is DATA WITH NO BEHAVIOUR. Every rule — dedup, ranking, tier mapping, wording — lives in a
//      pure engine over this record. Without that discipline it becomes a god object (the stated R3
//      risk in the Phase 3-5 design), so no method that makes a product decision may be added here.
//
//   2. It is VIEWER-INDEPENDENT. These are the person's facts, not "the facts this viewer may see" —
//      privacy is applied at emission by each engine through SectionAccess, never at load. That is
//      what lets one load serve every viewer, and it keeps the resolver the single privacy boundary
//      rather than splitting the decision across a loader and an engine.

/// <summary>A public event this person is connected to. Private events never enter the fact-set at
/// all, so the "private events never surface" invariant is enforced once, at load, instead of being
/// repeated in every query that touches an event.</summary>
public sealed record ProfileEventFact(
    Guid EventId, string Title, string Slug, string? BannerKey,
    DateTime StartsAt, DateTime EndsAt, string City, string? KindSlug,
    Guid OrgId, string OrgName, EventStatus Status,
    /// <summary>True when this person ran the event — they created it, or they hold a profile-visible
    /// membership at its organization (D-229).
    ///
    /// <para>This is a <b>property of the fact, decided once at load</b>, precisely so no consumer can
    /// re-derive "organized" differently. Three surfaces previously each computed their own version
    /// and displayed three different numbers on one page; the only durable fix is to make the
    /// question unanswerable anywhere but here.</para></summary>
    bool IsOrganizedByUser);

/// <summary>A public participation row. One person may hold several for one event (the unique key is
/// per role), which is why every consumer dedupes by <c>EventId</c> first (D-201).</summary>
public sealed record ProfileParticipationFact(Guid EventId, string RoleSlug, ParticipantState State);

/// <summary>An organizer-assigned duty. <see cref="Role"/> is the 14-value operational vocabulary —
/// a different vocabulary from a participation slug, so the two are kept apart rather than merged
/// into one "role" list that would need re-splitting downstream.</summary>
public sealed record ProfileAssignmentFact(
    Guid EventId, string Role, string? CustomRole, AssignmentStatus Status, DateTime? CompletedAt);

/// <summary>A verified check-in. <see cref="CheckedInAt"/> is null on older rows; consumers fall back
/// to the event's start.</summary>
public sealed record ProfileAttendanceFact(Guid EventId, DateTime? CheckedInAt);

/// <summary>A talk given, via the organizer-made <c>Speaker.UserId</c> link (D-208). Breaks are
/// excluded at load — a break is scheduling furniture, not a talk anyone gave.</summary>
public sealed record ProfileSessionFact(
    Guid EventId, string SessionTitle, DateTime StartsAt, DateTime EndsAt);

/// <summary>A published competition placement. Only <c>Published</c> results are loaded: a
/// provisional result is not yet a fact about the person and a disputed one is contested, so neither
/// belongs in a fact-set at all.</summary>
public sealed record ProfileResultFact(
    Guid EventId, string StageName, int Rank, decimal? FinalScore, DateTime OccurredAt);

/// <summary>An organization membership the user shows on their profile.</summary>
public sealed record ProfileMembershipFact(
    Guid OrgId, string OrgName, string OrgSlug, string? LogoKey,
    OrgRole OrgRole, MembershipClaimRole? ClaimedRole, bool IsVerified, bool OrgIsVerified,
    DateTime JoinedAt, DateTime? VerifiedAt, DateTime? ValidUntil);

/// <summary>Active team membership.</summary>
public sealed record ProfileTeamFact(Guid TeamId, string TeamName, TeamRole Role, DateTime JoinedAt);

/// <summary>A public, non-revoked certificate. Added in D-225 for the metrics engine — the loader
/// grows by one method and one member per fact family, which is the extensibility D-224 exists for.</summary>
public sealed record ProfileCertificateFact(
    Guid CertificateId, Guid EventId, CertificateKind Kind, DateTime IssuedAt, string VerifyCode);

/// <summary>A platform gamification badge (D-229). Kept as its own lane in Achievements rather than
/// merged with certificates — a badge proves engagement, a certificate proves recognition.</summary>
public sealed record ProfileBadgeFact(string Name, string? Description, string? IconKey, DateTime EarnedAt);

/// <summary>Account-level trust facts (D-229) — the inputs to the public trust signals, folded into
/// the fact-set so the root profile no longer needs a parallel cached summary to compute them.</summary>
public sealed record ProfileTrustFacts(
    bool IdentityVerified,
    bool EmailVerified,
    /// <summary>An organizer linked a <c>Speaker</c> record to this account (D-208) — evidence because
    /// someone else performed the act, not because the person claimed it.</summary>
    bool SpeakerLinked,
    /// <summary>Accepted ally connections. Held here so the count has one definition; whether it is
    /// <i>shown</i> is the Network section's decision, made at emission.</summary>
    int AllyCount);

/// <summary>Everything Kurx has verified about one person, loaded once per request.
///
/// <para><see cref="Events"/> is keyed by event id and is the only place event detail lives — every
/// other fact references an event by id, which is what removes the repeated joins each section used
/// to perform. A fact is only present if its event is in <see cref="Events"/>.</para>
///
/// <para>Adding a new fact family is one loader method and one member here; every engine then sees
/// it. That is the extensibility this record exists to buy.</para></summary>
public sealed record ProfileFactSet(
    Guid UserId,
    DateTime AccountCreatedAt,
    IReadOnlyDictionary<Guid, ProfileEventFact> Events,
    IReadOnlyList<ProfileParticipationFact> Participations,
    IReadOnlyList<ProfileAssignmentFact> Assignments,
    IReadOnlyList<ProfileAttendanceFact> Attendance,
    IReadOnlyList<ProfileSessionFact> Sessions,
    IReadOnlyList<ProfileResultFact> Results,
    IReadOnlyList<ProfileMembershipFact> Memberships,
    IReadOnlyList<ProfileTeamFact> Teams,
    IReadOnlyList<ProfileCertificateFact> Certificates,
    IReadOnlyList<ProfileBadgeFact> Badges,
    ProfileTrustFacts Trust);

/// <summary>Loads a <see cref="ProfileFactSet"/>. Registered <b>scoped</b> and memoised per user for
/// the lifetime of one request: a single request that projects several sections (the Journey today,
/// the Resume later) pays for one load. Cross-request reuse is deliberately left to the existing
/// summary cache — caching the whole fact-set would trade a large serialisation cost for a benefit
/// the per-request sharing already provides.</summary>
public interface IProfileFactSetLoader
{
    Task<ProfileFactSet> LoadAsync(Guid userId, CancellationToken ct = default);
}
