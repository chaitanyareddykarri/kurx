namespace Kurx.Application.Abstractions;

public record CollegeAffiliation(string Institute, string? Degree, string? Branch);

public record OrgProfileView(
    Guid OrgId, string OrgName, string OrgSlug, string? LogoKey,
    IReadOnlyList<string> Roles, bool IsVerified,
    DateTime JoinedAt, DateTime? ValidUntil,
    int OrgEventsConducted, int OrgCertificatesCount, int OrgAchievementsCount);

/// <summary>Root profile counts. <b>Every member is nullable and null means hidden from this viewer,
/// never zero</b> (D-229/H2) — a client renders "—". These are projections of the same
/// <c>ProfileCounts</c> definitions the metrics endpoint uses, so the two can never disagree.</summary>
public record ProfileStats(
    int? EventsConducted,
    int? EventsAttended,
    int? CertificatesCount,
    int? Participations,
    int? Achievements,
    int? AllyCount);

/// <summary>Trust indicators, all derived from verified rows (never from the self-declared profile).
/// Platform/staff roles are deliberately excluded — the Trust Center never leaks moderation internals.
///
/// <para><b>Positive signals only</b> (D-221). Risk score, fraud signals, report counts, moderation
/// state and trusted-device details are never exposed here or anywhere on a public profile: they are
/// moderation internals, and a public risk number would also tell a bad actor exactly when they have
/// been flagged. The absence of a badge is the only negative information, and it is deliberately
/// indistinguishable from a privacy choice.</para></summary>
public record VerificationBadges(
    bool IdentityVerified,      // UserIdentity.Status == Approved
    bool VerifiedMember,        // ≥1 Membership.IsVerified
    bool Organizer,             // conducted ≥1 public event
    int? VerifiedCertificates,  // public certificates; null = hidden from this viewer, never 0 (D-229)
    int YearsOnPlatform,
    bool EmailVerified,         // User.EmailVerifiedAt — proven via an emailed OTP (D-182)
    bool SpeakerVerified,       // an organizer linked a Speaker record to this account (D-208)
    bool CommunityVerified,     // verified membership of an org that is itself Verified (IsOrgVerifiedRep)
    /// <summary>True by construction — an account only exists after a successful OTP login — which is
    /// why it is stated here rather than stored as a redundant column.
    ///
    /// <para>It lived as a hardcoded <c>phone_verified = true</c> inside the endpoint's hand-written
    /// mapper, so it existed on the wire and in <c>web/lib/api.ts</c> but in no type. Typing the
    /// response would have dropped the key; putting it where it belongs keeps the wire identical and
    /// makes the contract describe what is actually sent.</para></summary>
    bool PhoneVerified = true);

/// <summary>A slice of the user's "Event DNA" — participation tallied by V3 event Kind (Phase 1 KindSlug).</summary>
public record EventDnaTag(string Kind, int Count);

/// <summary>A recognition — NOT tied to certificate storage; <see cref="Source"/> names where it came
/// from ("certificate" | "badge" today) so a future source can be added without reshaping this record
/// or any consumer (D-203).</summary>
public record AchievementCard(
    string Name, string? Description, string? IconKey, DateTime EarnedAt,
    string Source, string? EventTitle, string? EventSlug, string? OrgName);

public record PublicProfileView(
    Guid Id, string Name, string Username,
    string? Headline, string? Bio,
    string? AvatarKey, string? CoverKey,
    CollegeAffiliation? College,
    string? LinksJson, string[]? Skills,
    ProfileStats Stats,
    IReadOnlyList<OrgProfileView> Organizations,
    string Summary,
    VerificationBadges Verification,
    IReadOnlyList<EventDnaTag> EventDna,
    IReadOnlyList<AchievementCard> Achievements,
    IReadOnlyList<string> IdentityLabels,
    /// <summary>The derived headline (D-225) — the short form of <see cref="IdentityLabels"/>. Kept
    /// separate from <see cref="Headline"/>, which is the user's own self-declared line: one is proof,
    /// the other is a claim, and collapsing them would lose that distinction.</summary>
    string DerivedHeadline = "",
    /// <summary>Self-declared, Phase 2 (About). Null means "not stated" and never "none" — the client
    /// omits the section rather than asserting an absence.</summary>
    string[]? Languages = null,
    /// <summary>Self-declared interests. Deliberately NOT the derived <see cref="EventDna"/>, which is
    /// what this person provably did: one is a claim, the other is proof, and the profile keeps the two
    /// visibly apart for the same reason it does with the two headlines (D-225).</summary>
    string[]? Interests = null,
    /// <summary>When the account was created, to <b>month precision only</b> — ISO-8601 year-month
    /// (<c>"2026-08"</c>), rendered by clients as "Joined Kurx · August 2026".
    ///
    /// <para>Deliberately coarser than the <c>created_at</c> that <c>GET /v1/me</c> serves its owner.
    /// The exact instant an account was created is a behavioural fact about a person — it pins when
    /// they were at a keyboard, and correlates across accounts registered in the same minute — and
    /// nothing on a public profile needs it to say how long someone has been here. Truncating in the
    /// projection rather than formatting in the client means the precision is never on the wire to
    /// leak.</para>
    ///
    /// <para>Nullable only for the empty-profile cases this record is also constructed for; a real
    /// account always has one, because the column is non-null and set on insert.</para></summary>
    string? JoinedAt = null,
    /// <summary>Provenance (D-221): which category each field belongs to, so a client never has to
    /// guess whether a value is proof or a claim. <c>provenance-badge.tsx</c> renders directly from
    /// this map, so it is load-bearing rather than decorative.
    ///
    /// <para><c>[JsonPropertyName]</c> because the key carries a leading underscore that no naming
    /// convention would produce: the response converter renames keys but leaves <c>_meta</c> alone
    /// (it contains no uppercase), so serialized name and schema name agree.</para></summary>
    [property: System.Text.Json.Serialization.JsonPropertyName("_meta")]
    IReadOnlyDictionary<string, string>? Meta = null,
    /// <summary>Presigned companions to <see cref="AvatarKey"/>/<see cref="CoverKey"/> — D-302's rule
    /// ("a storage key is not a URL") reaching the profile, which that sweep fixed for events and missed
    /// here. The keys are kept beside them because <c>_meta</c> carries provenance keyed by
    /// <c>avatar_key</c>/<c>cover_key</c>, so removing them would break that map; these are what a client
    /// renders. Null when the key is unset — never a URL to nothing, or the client's "has a picture?"
    /// test turns true and shows a broken image where initials belong.</summary>
    string? AvatarUrl = null,
    string? CoverUrl = null);

public record PublicEventCard(
    Guid Id, string Title, string Slug, string? BannerKey,
    DateTime StartsAt, string City, string OrgName,
    IReadOnlyList<string> Roles, string Visibility,
    string? CertificateVerifyCode, bool IsAchievement);

public record PublicCertificateCard(
    Guid Id,
    string EventTitle,
    /// <summary>Emitted as <c>issued_at</c>, not <c>created_at</c>. The hand-written mapper this record
    /// replaced renamed the key on the way out, and both clients bind the renamed one
    /// (<c>web/lib/api.ts</c> <c>issued_at</c>, <c>@JsonKey(name: 'issued_at')</c> on Flutter). The
    /// attribute keeps the wire byte-identical; dropping it would be a silent breaking change that no
    /// compiler and — until the spec described this endpoint — no gate would have caught.</summary>
    [property: System.Text.Json.Serialization.JsonPropertyName("issued_at")]
    DateTime CreatedAt,
    string VerifyCode,
    bool IsAchievement);

/// <summary>One professional milestone. <see cref="Kind"/> is the source lane (org_joined |
/// org_verified | participation | certificate | achievement | organized | attended); <see cref="Roles"/>
/// carries every role held for a <c>participation</c> entry (an event with 2 roles is one entry, not
/// two — D-201/dedup fix). <see cref="IsFirstEvent"/> flags the single earliest entry across all lanes.</summary>
public record TimelineEntry(
    string Kind, string Title, string? Slug, string? BannerKey,
    IReadOnlyList<string> Roles, string? OrgName, string? City,
    DateTime OccurredAt, string? VerifyCode, bool IsFirstEvent);

public record PublicUserSearchResult(Guid Id, string Name, string Username, string? AvatarKey, string? Headline,
    /// <summary>Presigned companion to <c>AvatarKey</c> (D-302). Null when there is no key.</summary>
    string? AvatarUrl = null);

// ── Verified sources wired in D-222 ─────────────────────────────────────────
// All three already existed with a real person FK and simply never reached the profile. Each is a
// projection, exactly like the lanes above — no new table, no new write path.

// `OrgName` is null on all three when the event is self-represented (D-268). The self-representation
// row is named after the person, so emitting it would put someone's own name on their public profile
// as the institution that ran the event — the leak D-319 avoided in `AssignmentView` and these three
// siblings still carried. Null, never a fallback label: the clients render nothing.

/// <summary>A published competition placement (<c>StageResult</c>). Only <c>Published</c> results
/// surface: a Provisional or Disputed one is not yet a fact about the person. <see cref="Rank"/> is
/// the real stored rank, never re-derived here.</summary>
public record CompetitionResultCard(
    Guid EventId, string EventTitle, string EventSlug, string? BannerKey,
    string StageName, int Rank, decimal? Score, DateTime OccurredAt, string? OrgName);

/// <summary>One organizer-assigned duty (<c>EventAssignment</c>) — the 14-role operational vocabulary,
/// which the participation lane flattens to a handful of slugs and so loses. <see cref="CompletedAt"/>
/// is what makes this evidence of service delivered rather than merely invited.</summary>
public record AssignmentCard(
    Guid EventId, string EventTitle, string EventSlug, string? BannerKey,
    string Role, string Status, DateTime? CompletedAt, DateTime StartsAt, string? OrgName);

/// <summary>A talk actually given: <c>EventSessionSpeaker</c> → <c>Speaker.UserId</c>, a link an
/// organizer made (D-208), which is what makes it verified rather than self-claimed.</summary>
public record SpeakerSessionCard(
    Guid EventId, string EventTitle, string EventSlug, string? BannerKey,
    string SessionTitle, DateTime StartsAt, DateTime EndsAt, string? OrgName);

/// <summary>Experience (D-225) — a band plus the counts that produced it. The two always travel
/// together: a band alone is an unfalsifiable judgement, and the counts make it checkable.</summary>
public record ExperienceSummary(
    string Band,
    int DistinctEvents, int EventsOrganized, int EventsParticipated, int EventsAttended,
    int LeadershipEvents, int Organizations, int VerifiedOrganizations,
    int AssignmentsCompleted, int SpeakerSessions, int CompetitionsWon,
    int YearsActive, DateTime? FirstActivityAt);

/// <summary>Profile metrics (D-225; every member nullable as of D-229). A null member is
/// <b>hidden from this viewer</b>, never zero — a client must render "—" for null and never "0", or a
/// privacy choice would read as a statement about the person. Every count is defined once in
/// <c>ProfileCounts</c>; this record only carries the visible subset.</summary>
public record ProfileMetrics(
    int? EventsOrganized, int? EventsParticipated, int? EventsAttended,
    double? CompletionRate,
    int? AssignmentsAccepted, int? AssignmentsCompleted,
    int? CompetitionsEntered, int? CompetitionsWon,
    int? SpeakerSessions,
    int? Certificates, int? AchievementCertificates,
    int? Organizations, int? VerifiedOrganizations,
    int? AllyCount,
    IReadOnlyList<string> Cities,
    IReadOnlyList<EventDnaTag> EventDna);

/// <summary>One day of the contributions heatmap (D-228). <see cref="Level"/> is a 0–4 band, not a
/// raw count, so the client renders density without re-deciding the scale.</summary>
public record ContributionDay(DateOnly Date, int Count, int Level);

/// <summary>The contributions heatmap. Only days with activity are returned — a year of explicit
/// zeroes would be 365 rows of nothing, and the client fills gaps itself.</summary>
public record ContributionsSummary(
    DateOnly From, DateOnly To, int Total, IReadOnlyList<ContributionDay> Days);

/// <summary>One rung of the Professional Journey (D-223) — the first time a tier was provably reached.
/// <see cref="Occurrences"/> is how many times it has happened since; <see cref="EvidenceKind"/> names
/// the table that proved it, and the remaining fields point at the earliest instance. A node with no
/// resolvable evidence is never emitted.</summary>
public record JourneyNode(
    string Tier, DateTime FirstAttainedAt, int Occurrences,
    string EvidenceKind, string? EventTitle, string? EventSlug, string? Detail, string? OrgName);

/// <summary>The evidence behind one <see cref="JourneyNode"/>, as its own object on the wire. The node
/// is only meaningful because a row proves it, and the client renders this as the "show me the proof"
/// link — so the four pointers travel together rather than as loose siblings of the tier.</summary>
public record JourneyEvidenceView(
    string Kind, string? EventTitle, string? EventSlug, string? Detail, string? OrgName);

/// <summary>The wire shape of one Journey rung — <see cref="JourneyNode"/> is the service's flat
/// projection, this is what <c>GET /v1/public/users/{username}/journey</c> has always emitted.
///
/// <para><b>Why a second record rather than reshaping <see cref="JourneyNode"/>.</b> The flat node is
/// consumed internally by <c>ResumeEngine</c>, which wants the fields loose; the wire nests them under
/// <c>evidence</c> and adds a constant <c>source</c>. Reshaping the node to match would push an
/// API-surface concern down into a type the PDF composer also reads. Both clients already bind the
/// nested form (<c>evidence: z.object({...})</c> on web, <c>JourneyEvidenceDto</c> on Flutter), so this
/// is the existing contract given a name — not a new one.</para>
///
/// <para><see cref="Source"/> is a constant rather than data: every rung of the Journey is derived from
/// a verified row by construction, and a node with no resolvable evidence is never emitted at all.</para></summary>
public record JourneyNodeView(
    string Tier, DateTime FirstAttainedAt, int Occurrences, string Source, JourneyEvidenceView Evidence)
{
    /// <summary>Declared here, beside the contract it produces, so the projection cannot drift from the
    /// shape — which is exactly what happened while it lived as an anonymous object in the endpoint.</summary>
    public static JourneyNodeView From(JourneyNode n) => new(
        n.Tier, n.FirstAttainedAt, n.Occurrences, "verified",
        new JourneyEvidenceView(n.EvidenceKind, n.EventTitle, n.EventSlug, n.Detail, n.OrgName));
}

/// <summary>Reads of the public profile. Every method takes an optional <c>viewerId</c> (D-221): the
/// endpoints are anonymous-allowed, but a signed-in reader may be entitled to more than an anonymous
/// one under the Connections / EventParticipants visibility tiers. Null means anonymous. The parameter
/// is optional so every existing caller compiles unchanged and keeps its previous, anonymous behaviour.</summary>
public interface IPublicProfileService
{
    Task<ServiceResult<PublicProfileView>> GetProfileAsync(
        string username, Guid? viewerId = null, CancellationToken ct = default);

    /// <summary>Foundation for people-search across the app (D-20x) — public profiles only
    /// (<c>ProfilePublic</c> + a claimed <c>Username</c>), name/username substring match.</summary>
    Task<IReadOnlyList<PublicUserSearchResult>> SearchUsersAsync(string query, int page, int pageSize, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<PublicEventCard>>> GetEventsAsync(
        string username, string type, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<PublicCertificateCard>>> GetCertificatesAsync(
        string username, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<TimelineEntry>>> GetTimelineAsync(
        string username, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default);

    /// <summary>Published competition placements (D-222). Gated by the Achievements section.</summary>
    Task<ServiceResult<IReadOnlyList<CompetitionResultCard>>> GetCompetitionResultsAsync(
        string username, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default);

    /// <summary>Organizer-assigned duties with their completion state (D-222). Gated by Events.</summary>
    Task<ServiceResult<IReadOnlyList<AssignmentCard>>> GetAssignmentsAsync(
        string username, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default);

    /// <summary>Talks given, from the organizer-made speaker link (D-222). Gated by Events.</summary>
    Task<ServiceResult<IReadOnlyList<SpeakerSessionCard>>> GetSpeakerSessionsAsync(
        string username, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default);

    /// <summary>The Professional Journey (D-223) — first attainment of each professional tier, in
    /// chronological order. Each tier is gated by the section that owns its evidence, so a hidden
    /// section cannot leak through here.</summary>
    Task<ServiceResult<IReadOnlyList<JourneyNode>>> GetJourneyAsync(
        string username, Guid? viewerId = null, CancellationToken ct = default);

    /// <summary>Counts, rates and the Event DNA distribution (D-225), computed from the
    /// viewer-visible fact subset. Gated by the Metrics section.</summary>
    Task<ServiceResult<ProfileMetrics>> GetMetricsAsync(
        string username, Guid? viewerId = null, CancellationToken ct = default);

    /// <summary>Experience band + the counts behind it (D-225). Gated by the Events section.</summary>
    Task<ServiceResult<ExperienceSummary>> GetExperienceAsync(
        string username, Guid? viewerId = null, CancellationToken ct = default);

    /// <summary>Daily contribution density (D-228). Each contributing fact is gated <b>before</b> it
    /// is bucketed, so a hidden section lowers the day's intensity rather than leaking through it.</summary>
    Task<ServiceResult<ContributionsSummary>> GetContributionsAsync(
        string username, int months, Guid? viewerId = null, CancellationToken ct = default);

    /// <summary>The Professional Resume as a PDF (D-228), composed for <b>this viewer</b> — a section
    /// they may not see is absent from their copy, so the document can never be a privacy bypass.</summary>
    Task<ServiceResult<byte[]>> GetResumePdfAsync(
        string username, string profileUrl, Guid? viewerId = null, CancellationToken ct = default);
}
