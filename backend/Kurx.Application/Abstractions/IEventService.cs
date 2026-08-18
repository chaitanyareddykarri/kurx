namespace Kurx.Application.Abstractions;

public record EventVenueView(Guid? VenueId, string Name, string Address, string City, double? Lat, double? Lng, string? GoogleMapsUrl);

/// D-265 — the create-event wizard's extra fields, grouped rather than flattened into the input
/// records below. Those are already 25-parameter positional records; adding twenty-five more
/// positions would make every call site unreadable and a mis-ordered argument invisible. Each group
/// is optional: null means "leave alone" on update and "unset" on create.
public record EventContentInput(string? Tagline = null, string? ShortDescription = null,
    string? LogoKey = null, string? ThumbnailKey = null, string? PromoVideoKey = null,
    string? Rules = null, string? FaqJson = null);

public record EventLegalInput(string? TermsUrl = null, string? TermsText = null,
    string? CodeOfConduct = null, string? RefundPolicy = null, string? CancellationPolicy = null,
    bool? RequiresConsent = null, string? ConsentText = null);

public record EventScheduleInput(DateTime? RegistrationOpensAt = null, DateTime? RegistrationClosesAt = null,
    DateTime? CheckinOpensAt = null, DateTime? CheckinClosesAt = null, DateTime? ResultDate = null,
    DateTime? CertificateReleaseAt = null, bool? AutoClose = null);

public record EventLocationInput(string? Building = null, string? Floor = null, string? Room = null,
    string? GoogleMapsUrl = null, string? MeetingPlatform = null, string? MeetingPassword = null);

/// Demographic gates. Separate from AudienceRule (§4.4), which is org membership — both may apply.
public record EventEligibilityInput(int? MinAge = null, int? MaxAge = null,
    string? GenderRestriction = null, int? MaxTeams = null);

public record EventCommerceInput(decimal? PlatformFeePercent = null, long? PlatformFeeFlatPaise = null,
    decimal? TaxPercent = null, bool? TaxInclusive = null, string? PrizePoolJson = null);

public record CreateEventInput(
    string Title, string? Subtitle, string? Description,
    Guid CategoryId, Guid? TypeId, Guid? AudienceLevelId, Guid? TemplateId, Guid? ParentEventId,
    IReadOnlyList<string>? Tags,
    Guid? VenueId, string? VenueName, string? VenueAddress, string? City, double? Lat, double? Lng,
    DateTime StartsAt, DateTime EndsAt, string? Timezone,
    int? Capacity, string? Visibility, string? Language,
    string? ContactEmail, string? ContactPhone, string? Website, string? SocialLinksJson,
    string? EventMode = null, string? OnlineUrl = null,
    EventContentInput? Content = null, EventLegalInput? Legal = null,
    EventScheduleInput? Schedule = null, EventLocationInput? Location = null,
    EventEligibilityInput? Eligibility = null, EventCommerceInput? Commerce = null);

public record UpdateEventInput(
    string? Title, string? Subtitle, string? Description,
    Guid? CategoryId, Guid? TypeId, Guid? AudienceLevelId, Guid? TemplateId,
    IReadOnlyList<string>? Tags,
    Guid? VenueId, string? VenueName, string? VenueAddress, string? City, double? Lat, double? Lng,
    DateTime? StartsAt, DateTime? EndsAt, string? Timezone,
    int? Capacity, string? Visibility, string? Language,
    string? ContactEmail, string? ContactPhone, string? Website, string? SocialLinksJson,
    string? BannerKey, bool? IsFeatured,
    string? EventMode = null, string? OnlineUrl = null,
    bool? ListedStandalone = null,   // V3 §3.4 rule 2
    EventContentInput? Content = null, EventLegalInput? Legal = null,
    EventScheduleInput? Schedule = null, EventLocationInput? Location = null,
    EventEligibilityInput? Eligibility = null, EventCommerceInput? Commerce = null);   // (Phase 12): opt a sub-event into standalone discovery

/// <summary><paramref name="Url"/> is a presigned GET, exactly as <c>PostMediaView</c> carries (D-262).
/// The raw <paramref name="Key"/> is NOT fetchable — <c>PresignGetAsync</c> builds a signed
/// <c>/v1/storage/…</c> URL — so a client handed only the key can render nothing, which is why every
/// event image on every surface was blank (D-302).</summary>
public record EventMediaView(Guid Id, string Kind, string Key, string Caption, int Sort, string? Url = null);

/// D-265 read-back. The wizard writes these; without them on the way out the fields are write-only —
/// an edit form cannot prefill and a detail page cannot render what was saved.
///
/// <b><see cref="EventLocationDetailView"/> deliberately has no MeetingPassword.</b> `ToEventJson`
/// serves the PUBLIC `GET /v1/events/{slug}` as well as the organiser reads, so a password added
/// here would be published to anonymous visitors. Exposing it to confirmed registrants only is a
/// separate, gated read.
public record EventContentView(string? Tagline, string? ShortDescription, string? LogoKey,
    string? ThumbnailKey, string? PromoVideoKey, string? Rules, string? FaqJson,
    // D-302 — presigned companions to the three keys above, for the same reason EventMediaView needs one.
    string? LogoUrl = null, string? ThumbnailUrl = null, string? PromoVideoUrl = null);

public record EventLegalView(string? TermsUrl, string? TermsText, string? CodeOfConduct,
    string? RefundPolicy, string? CancellationPolicy, bool RequiresConsent, string? ConsentText);

public record EventScheduleView(DateTime? RegistrationOpensAt, DateTime? RegistrationClosesAt,
    DateTime? CheckinOpensAt, DateTime? CheckinClosesAt, DateTime? ResultDate,
    DateTime? CertificateReleaseAt, bool AutoClose);

public record EventLocationDetailView(string? Building, string? Floor, string? Room,
    string? GoogleMapsUrl, string? MeetingPlatform);

/// <param name="MaxTeams">How many teams may enter — <b>derived, not the stored column</b> (D-375).
///
/// <para><c>events.MaxTeams</c> is a D-265 eligibility field that was written, echoed here, and enforced by
/// nothing: an organiser could type 50 while the ticket type sold 20 team slots, and both numbers were
/// shown as fact on different screens. The authoritative team capacity is the registration unit's own
/// inventory — <c>TicketType.Quantity</c> for a <c>PerGroup</c> ticket, which is what the pool draws
/// against (V3 §17.1) — so this projects THAT when the event has one, and falls back to the stored hint
/// only for an event with no team ticket, where there is nothing to contradict.</para>
///
/// <para>Deriving rather than enforcing is deliberate: wiring the stored column into inventory would give
/// the platform a second capacity authority competing with the pool, which is the mistake §17.1 exists to
/// prevent. This leaves exactly one number a client can see, and it is the pool's.</para></param>
public record EventEligibilityView(int? MinAge, int? MaxAge, string GenderRestriction, int? MaxTeams);

/// <summary>The event's full commercial configuration, <b>including the platform's own cut</b>. Internal:
/// it stays on <see cref="EventDetail"/> for the service layer and any future organiser finance surface,
/// and is never the shape put on the wire — see <see cref="PublicEventCommerceView"/> (D-356).</summary>
public record EventCommerceView(decimal? PlatformFeePercent, long? PlatformFeeFlatPaise,
    decimal? TaxPercent, bool TaxInclusive, string? PrizePoolJson);

/// <summary>What an attendee may know about an event's commercials, and nothing more (D-356).
///
/// <para><c>PlatformFeePercent</c> and <c>PlatformFeeFlatPaise</c> are the <b>Kurx↔organiser commercial
/// arrangement</b>. They were reaching anonymous callers because <see cref="EventDetailResponse"/> — the
/// wire record whose own summary says it serves "the public <c>GET /v1/events/{slug}</c> as well as every
/// organiser read" — passed <see cref="EventCommerceView"/> straight through. That is the same seam
/// <c>MeetingPassword</c> was already excluded at; this closes the other half.</para>
///
/// <para>What stays: tax is a property of <b>what the attendee pays</b>, and a prize pool is something a
/// competition advertises. Neither is an internal figure.</para></summary>
public record PublicEventCommerceView(decimal? TaxPercent, bool TaxInclusive, string? PrizePoolJson)
{
    public static PublicEventCommerceView? From(EventCommerceView? c)
        => c is null ? null : new(c.TaxPercent, c.TaxInclusive, c.PrizePoolJson);
}

public record EventDetail(
    Guid Id, Guid RepresentingOrgId, Guid? ParentEventId, string Title, string Slug, string ShortCode, string Subtitle, string Description,
    Guid CategoryId, Guid? TypeId, Guid? AudienceLevelId, Guid? TemplateId,
    IReadOnlyList<string> Tags, EventVenueView Venue,
    DateTime StartsAt, DateTime EndsAt, string Timezone,
    int? Capacity, string Visibility, string Status, string Language,
    string ContactEmail, string ContactPhone, string Website, string? SocialLinksJson,
    string? BannerKey, bool IsFeatured, int ViewCount,
    IReadOnlyList<EventMediaView> Media,
    DateTime CreatedAt, DateTime? PublishedAt, DateTime UpdatedAt,
    string EventMode, string? OnlineUrl, string SettlementCurrency,
    // D-265 — optional so every existing construction site still compiles unchanged.
    EventContentView? Content = null, EventLegalView? Legal = null, EventScheduleView? Schedule = null,
    EventLocationDetailView? LocationDetail = null, EventEligibilityView? Eligibility = null,
    EventCommerceView? Commerce = null,
    // D-302 — who the host is representing, resolved server-side. RepresentingOrgId above is a bare Guid,
    // so every client that wanted to name the organization had to fetch it separately, and none did.
    EventRepresentationView? Representing = null,
    string? BannerUrl = null)
{
    /// <summary><b>Deprecated (D-273a) — use <c>representing_org_id</c>.</b> Emitted only so clients
    /// deployed before the rename keep working; it always equals <see cref="RepresentingOrgId"/> and never
    /// meant ownership (the owner is the user in <c>created_by</c>). Remove in the contract phase once no
    /// client reads it.</summary>
    [Obsolete("Use RepresentingOrgId. Emitted for pre-D-273a clients; removed in the contract phase.")]
    public Guid OrgId => RepresentingOrgId;
}

/// <summary>The organization an event represents, named rather than referenced (D-302). An event is owned
/// by the user in <c>created_by</c> (D-268); this is the institution it is run on behalf of, which is what
/// an attendee reads to decide whether to trust it. <see cref="IsVerified"/> is the only trust signal
/// carried — a client must never infer verification from the presence of a name.</summary>
public record EventRepresentationView(Guid OrgId, string Name, string Slug, string? LogoKey, bool IsVerified);

public record EventSummary(Guid Id, Guid RepresentingOrgId, Guid? ParentEventId, string Title, string Slug, string ShortCode, string Subtitle,
    string? BannerKey, DateTime StartsAt, DateTime EndsAt, string Status, string Visibility, string VenueName, string City,
    // D-302 — optional and trailing so every existing construction site compiles unchanged. These are the
    // facts a card needs and could not previously get: two of them (mode, price) were already FILTERS, so
    // discovery let you narrow by a value it then refused to show you.
    string EventMode = "Offline", string? CategoryName = null,
    long? PriceFromPaise = null, string Currency = "INR", bool IsFeatured = false,
    /// <summary>The unit <see cref="PriceFromPaise"/> is charged in — `PerTicket` or `PerGroup` (D-376).
    ///
    /// <para>Without it a discovery card can only say "From ₹2,000", which on a team event reads as a
    /// per-person minimum when it is the price of the whole team. The card stays condensed by design; it
    /// just stops being ambiguous. Taken from the CHEAPEST ticket, so it pairs with the price beside it.</para></summary>
    string? PriceFromUnit = null,
    /// <summary>Presigned banner. Every card on every surface rendered imageless because the summary
    /// carried only <c>BannerKey</c>, which is not fetchable (D-302).</summary>
    string? BannerUrl = null)
{
    /// <summary><b>Deprecated (D-273a) — use <c>representing_org_id</c>.</b> See <see cref="EventDetail"/>.</summary>
    [Obsolete("Use RepresentingOrgId. Emitted for pre-D-273a clients; removed in the contract phase.")]
    public Guid OrgId => RepresentingOrgId;
}

/// <summary>Enriched org-scoped row for the organizer events-management table. Kept distinct from the
/// public <see cref="EventSummary"/> so per-event stats (sold / checked-in / revenue) never leak onto the
/// public listing endpoints. RevenuePaise is captured-order gross — a read aggregate, not a payout figure.</summary>
public record OrgEventRow(Guid Id, string Title, string Slug, string Status, string Visibility,
    string? CategoryName, string VenueName, string City, int? Capacity, bool IsPaid,
    int TicketsSold, int CheckedIn, long RevenuePaise, DateTime StartsAt, DateTime UpdatedAt, string Currency);

/// <summary>Who an event represents (D-268). <c>Personal</c> means the owner represents themselves and
/// carries no organization at all — not an organization named "personal". <c>Organization</c> carries the
/// institution's identity. Representation affects branding, verification, trust, permissions and payout
/// destination; it never denotes ownership, which is always the user.</summary>
public record RepresentationView(string Kind, Guid? OrganizationId, string? OrganizationName, bool Verified)
{
    public const string Personal = "personal";
    public const string Organization = "organization";

    public static RepresentationView ForPersonal() => new(Personal, null, null, false);
    public static RepresentationView ForOrganization(Guid id, string name, bool verified)
        => new(Organization, id, name, verified);
}

/// <summary>One of the caller's own events, for their Workspace. Same enriched shape as
/// <see cref="OrgEventRow"/> plus who the event represents — carried so the row can render "representing X"
/// without a second call, NOT so the client can group by it.</summary>
public record MyEventRow(Guid Id, string Title, string Slug, string Status, string Visibility,
    string? CategoryName, string VenueName, string City, int? Capacity, bool IsPaid,
    int TicketsSold, int CheckedIn, long RevenuePaise, DateTime StartsAt, DateTime UpdatedAt, string Currency,
    RepresentationView Representation);

public record EventListFilter(string? Q, Guid? CategoryId, Guid? OrgId, string? City, string? Status,
    string? Visibility, DateTime? DateFrom, DateTime? DateTo, string? Sort, int Page, int PageSize,
    string? Price = null,    // Price: "free" | "paid" (D-064 A7).
    string? Mode = null,     // Mode: "offline" | "online" | "hybrid" (D-064 A7).
    // V3 §15 (Phase 16) — additive optional discovery filters; all null = unchanged behaviour for existing clients.
    string? Kind = null,     // event_kinds slug (§2).
    string? Language = null, // ISO language code.
    double? Lat = null,      // caller location → proximity ranking + optional radius filter; no location ⇒ no proximity influence.
    double? Lng = null,
    double? RadiusKm = null);

/// <summary>An event awaiting platform review (M8, D-057) — surfaced in the admin approval queue.</summary>
/// <summary>D-266 M4 — one recorded reviewer decision. <c>Notes</c> is admin-only and never surfaces to
/// the organiser verbatim; the reason code is what a client renders.</summary>
public record EventReviewEntry(Guid Id, string Decision, Guid? ReviewerId, string? ReviewerName,
    string? ReasonCode, string? Notes, DateTime CreatedAt);

/// <summary>D-266 M4 — queue tab counts. <c>Legacy</c> is the pre-M4 <c>InReview</c> bucket, retired in
/// Stage 4 and now always 0; the field stays on the contract because removing one is a client break.</summary>
public record EventReviewCounts(int PendingReview, int UnderReview, int ChangesRequested,
    int Approved, int Rejected, int Legacy);

/// <summary>D-266 M8 — one caller's autosaved wizard state. <c>PayloadJson</c> is returned exactly as it
/// was stored; the server never interprets it.</summary>
public record EventDraftView(Guid EventId, string PayloadJson, string? StepKey, DateTime SavedAt);

/// <param name="Status">D-266 M7 — `PendingReview` or `UnderReview`. The queue spans both, and a reviewer
/// deciding what to claim needs to know which without a second call.</param>
/// <param name="Product">Public or Private. Drives which review sections apply at all.</param>
/// <param name="ArchetypeSlug">The behaviour axis — what kind of event a reviewer is about to read.</param>
/// <param name="IsPaid">Whether money is involved, which is what makes financial review apply (A11).</param>
/// <param name="AuthorizationStatus">The institutional authorization's state, or null when none is filed.
/// A reviewer's most common question about a represented event, answered in the list rather than after a
/// click.</param>
/// <param name="City">Where it claims to happen. **`MeetingPassword` is deliberately absent from this
/// record entirely** — it is excluded by construction rather than filtered per call site, because the next
/// call site is the one that forgets (D-266 §5).</param>
public record PendingEventView(Guid EventId, Guid RepresentingOrgId, string OrgName, string Title, string Slug,
    DateTime StartsAt, DateTime CreatedAt,
    string Status = "PendingReview", string Product = "Public", string? ArchetypeSlug = null,
    bool IsPaid = false, string? AuthorizationStatus = null, string? City = null,
    // D-266 M4 — who holds this item. Resolved to a name server-side so the queue needs no call per row;
    // null when unclaimed, or when the holder's account no longer exists.
    Guid? ReviewClaimedBy = null, string? ReviewClaimedByName = null, DateTime? ReviewClaimedAt = null)
{
    /// <summary><b>Deprecated (D-273a) — use <c>representing_org_id</c>.</b> See <see cref="EventDetail"/>.</summary>
    [Obsolete("Use RepresentingOrgId. Emitted for pre-D-273a clients; removed in the contract phase.")]
    public Guid OrgId => RepresentingOrgId;
}

/// <summary>An event row in the global admin event-management list (D-061/D-186) — any status, with its org
/// and the same per-event stats the organizer's own list already computes (<see cref="OrgEventRow"/>), plus
/// moderation state. RegistrationsCount mirrors TicketsSold today (an issued, non-void ticket is the
/// authoritative registration record) — kept as a separately-named field so a future move onto the V3
/// Registration substrate is a data-source swap, not an API contract change. Reports/fraud counts are
/// deliberately NOT here: they come from IReportService/IFraudService, composed at the endpoint layer so
/// EventService never reaches into another service's table.</summary>
public record AdminEventView(Guid EventId, Guid RepresentingOrgId, string OrgName, string Title, string Slug,
    string Status, bool IsFeatured, DateTime StartsAt, DateTime CreatedAt,
    string? CategoryName, string? SubcategoryName, string Visibility, string City, string VenueName, int? Capacity,
    DateTime EndsAt, bool IsPaid, string OrgVerificationStatus, DateTime UpdatedAt,
    bool IsSuspended, string? SuspendedReason, bool IsHidden, string? HiddenReason, string? BannerKey,
    int TicketsSold, int CheckedIn, int RegistrationsCount, long RevenuePaise, string Currency,
    /// <summary>D-381 — the USER who owns the event (<c>Event.CreatedBy</c>), which is not the
    /// organization it represents. The console showed only the organization, so a legacy
    /// self-representation row rendered a person's name under "Representing organization".</summary>
    Guid CreatorId = default, string? CreatorName = null,
    /// <summary>The represented organization is a legacy self-representation row (D-268), retired for new
    /// events by D-379. Sent so the console can name it as legacy rather than fabricate an
    /// organization — no new event can produce this.</summary>
    bool OrgIsPersonal = false)
{
    /// <summary><b>Deprecated (D-273a) — use <c>representing_org_id</c>.</b> See <see cref="EventDetail"/>.</summary>
    [Obsolete("Use RepresentingOrgId. Emitted for pre-D-273a clients; removed in the contract phase.")]
    public Guid OrgId => RepresentingOrgId;
}

/// <summary>Filter for the global admin event list (D-186/D-187) — the same shape as the public
/// <see cref="EventListFilter"/>, extended with the admin-only axes (verification, revenue/registration
/// range, moderation flags) a platform operator needs that a public visitor never would.</summary>
public record AdminEventListFilter(string? Q, string? Status, Guid? CategoryId, string? City,
    string? Visibility, bool? IsPaid, bool? VerifiedOrgOnly, DateTime? DateFrom, DateTime? DateTo,
    long? RevenueMinPaise, long? RevenueMaxPaise, int? RegistrationsMin, int? RegistrationsMax,
    string? Sort, int Limit, int Page = 1);

public record PaymentReadiness(bool IsPaid, bool PaymentsEnabled, IReadOnlyList<string> BlockingReasons);

/// <summary>One organiser-workspace tab (V3 §20) — a <c>workspace_tab</c> group with the event's non-Off
/// capabilities under it. Generated from the capability registry, never hand-written per Kind.</summary>
public record WorkspaceTabView(string Tab, IReadOnlyList<WorkspaceCapabilityView> Capabilities);
public record WorkspaceCapabilityView(string Slug, string Name, string State);

/// <summary>One continuously-computed publish-checklist row (V3 §14.2/§20): a forward lifecycle action, its target
/// state, whether its validation gate currently passes, and (if not) the blocking code. Projects the Phase-14 gate
/// logic — it is never a second source of truth.</summary>
public record ChecklistItemView(string Action, string Target, bool Ok, string? Blocker);

/// <summary>The generated organiser workspace for an event (V3 §20): capability tabs + the live publish checklist.</summary>
public record EventWorkspaceView(Guid EventId, string Status, IReadOnlyList<WorkspaceTabView> Tabs, IReadOnlyList<ChecklistItemView> Checklist);

public record EventAnalytics(
    Guid EventId, int ViewCount, int TicketTypes, int TicketsIssued, int CheckedIn, int OrdersPaid, long GrossPaise, string Currency);

/// <summary>
/// Event content management: CRUD, status workflow, and the taxonomy/venue/tag associations around it.
/// Ticketing (TicketType/FormField) is a separate, later-phase concern and is untouched here (D-018).
/// **Authorization is <see cref="IEventAuthority"/>'s job, not this interface's** (D-269): the event's
/// creator owns it (D-268); a Representative or an Owner/Manager seat in the organization it represents
/// is an additional grant; a platform admin (KurxAdmin claim) outranks both.
/// </summary>
/// <summary>D-388 — one changed field, as a reviewer reads it. <paramref name="Current"/> comes from the
/// LIVE row at render time, not from the snapshot taken when the request was made: a reviewer must decide
/// against what the event is now, and the two differing is exactly the version conflict that refuses the
/// approval. Both sides are pre-formatted strings — a reviewer compares a date, not an ISO timestamp, and
/// the server owns that rendering so three clients cannot format it three ways.</summary>
public record EventChangeField(string Field, string Label, string? Current, string? Proposed);

/// <summary>D-388 — a proposed edit to a live event, as host and reviewer both see it.</summary>
/// <param name="BaseVersion">The <c>Event.Version</c> this was authored against.</param>
/// <param name="CurrentVersion">What the event is on now. Unequal means the proposal is stale and
/// approving it is refused (<c>version_conflict</c>) rather than allowed to overwrite newer approved
/// values.</param>
public record EventChangeRequestView(
    Guid Id, Guid EventId, string EventTitle, Guid RequestedBy, string? RequestedByName,
    int BaseVersion, int CurrentVersion, bool Stale,
    IReadOnlyList<EventChangeField> Changes, string? Reason, string Status,
    Guid? ReviewedBy, string? ReviewedByName, DateTime? ReviewedAt,
    string? ReviewReasonCode, string? ReviewNotes,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? AppliedAt);

public interface IEventService
{
    /// <summary>Creates an event owned by <paramref name="userId"/>.
    ///
    /// <para><paramref name="representingOrgId"/> is the organization the event <b>represents</b> — null
    /// means the user represents themselves (<c>Representing = Personal</c>). Representation is an
    /// attribute of the event affecting branding, verification, trust, permissions and payout
    /// destination; it is never the owner. The owner is always the user, recorded in
    /// <see cref="Kurx.Domain.Entities.Event.CreatedBy"/> and enforced from there (D-268).</para>
    ///
    /// <para>Authorization here is *representation* authority, not ownership: representing an
    /// organization requires a manage-capable seat in it; representing yourself requires nothing.</para></summary>
    Task<ServiceResult<EventDetail>> CreateAsync(Guid userId, Guid? representingOrgId, bool isAdmin, CreateEventInput input, CancellationToken ct = default);

    /// <summary>D-101 (M7): duplicates an event as a fresh <c>Draft</c> — copies content (ticket types with
    /// <c>Sold</c> reset, form fields, media, speakers, sponsors, sessions, tags) but never runtime state
    /// (tickets, orders, attendees, analytics, published/featured flags). Owner/Manager/Representative.</summary>
    Task<ServiceResult<EventDetail>> CloneAsync(Guid userId, Guid eventId, bool isAdmin, string? newTitle,
        CancellationToken ct = default);

    Task<ServiceResult<EventDetail>> UpdateAsync(Guid userId, Guid eventId, bool isAdmin, UpdateEventInput input, CancellationToken ct = default);

    // ── D-388 · change requests on a LIVE event ──────────────────────────────────────────────────
    // These live on IEventService rather than in a service of their own because approving a change
    // request IS an event update: it must run through the same private apply path `UpdateAsync` uses
    // (D-191's "one and only event-update implementation"), reuse the same authorization helper, and
    // produce the same reindex, refund window and capability rematerialization. A separate service could
    // reach none of that without duplicating it, which is the drift D-191 exists to prevent.

    /// <summary>Proposes an edit to a live Public event. Refused with <c>not_live_protected</c> if the
    /// event is not one — a draft is edited directly and a change request there would be a second way to
    /// do the same thing. Replaces the event's existing pending request rather than creating a second:
    /// one pending proposal per event, so "what is waiting for approval" has one answer.</summary>
    Task<ServiceResult<EventChangeRequestView>> CreateChangeRequestAsync(Guid userId, Guid eventId,
        UpdateEventInput input, string? reason, CancellationToken ct = default);

    /// <summary>This event's change requests, newest first. Readable by anyone who may manage the event
    /// and by reviewers/admins — the same audience as the event's review history.</summary>
    Task<ServiceResult<IReadOnlyList<EventChangeRequestView>>> ListChangeRequestsAsync(Guid userId,
        Guid eventId, bool isAdmin, bool isReviewer, CancellationToken ct = default);

    /// <summary>The host takes their own pending proposal back. Only from <c>Pending</c>: a decided
    /// request is a record, not a draft.</summary>
    Task<ServiceResult<EventChangeRequestView>> WithdrawChangeRequestAsync(Guid userId, Guid eventId,
        Guid changeRequestId, CancellationToken ct = default);

    /// <summary>A reviewer's verdict. Approving applies every proposed value in ONE transaction — all or
    /// nothing, never a live event left with a new title and its old date — and only if the event is still
    /// on the version the request was authored against.</summary>
    /// <param name="approve">true = apply; false = reject, which requires <paramref name="reasonCode"/>.</param>
    Task<ServiceResult<EventChangeRequestView>> DecideChangeRequestAsync(Guid reviewerId, Guid eventId,
        Guid changeRequestId, bool approve, string? reasonCode, string? notes, bool isAdmin, bool isReviewer,
        CancellationToken ct = default);

    /// <summary>The platform-wide queue of pending change requests, oldest first — the admin console's
    /// counterpart to <c>GET /v1/admin/events/pending</c>.</summary>
    Task<ServiceResult<IReadOnlyList<EventChangeRequestView>>> ListPendingChangeRequestsAsync(int limit,
        CancellationToken ct = default);

    /// <summary>D-191: Super Admin only, exceptional path over the same update core <see cref="UpdateAsync"/>
    /// uses — never a second update implementation. <paramref name="reason"/> is mandatory; the call is
    /// refused (<c>reason_required</c>) if it's empty/whitespace. Writes a full before/after audit snapshot.</summary>
    Task<ServiceResult<EventDetail>> EmergencyUpdateAsync(Guid actorId, Guid eventId, string reason, UpdateEventInput input, CancellationToken ct = default);

    /// <summary>Applies a status transition (see EventStatusWorkflow). Free events publish directly; a
    /// PAID event (any priced ticket type) may not self-publish — the org must <c>submit_review</c>
    /// (gated on the organizer's paid-organizing capability + org verification, M7/M8) and a platform
    /// reviewer <c>publish</c>es it. <paramref name="isReviewer"/> = caller holds VerificationReviewer/SuperAdmin.</summary>
    /// <summary>D-266 M4 — <paramref name="reasonCode"/> and <paramref name="notes"/> carry a reviewer's
    /// decision. Optional and trailing so every existing caller binds unchanged; the workflow decides which
    /// actions require them (<c>EventStatusWorkflow.ActionsRequiringReason</c> / <c>...RequiringNotes</c>),
    /// so the rule lives in one place rather than at each call site.</summary>
    Task<ServiceResult<EventDetail>> TransitionAsync(Guid userId, Guid eventId, bool isAdmin, bool isReviewer,
        string action, string? reasonCode = null, string? notes = null, CancellationToken ct = default);

    /// <summary>Live payment-readiness for an event (M8): whether it is paid, whether payments would be
    /// enabled right now (Published + organizer paid-verified + org verified), and any blocking reasons.
    /// Computed fresh — never a stored flag — so a suspension takes effect immediately (M10 reuses this).</summary>
    Task<ServiceResult<PaymentReadiness>> GetPaymentReadinessAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>The generated organiser workspace (V3 §20): the event's capabilities grouped by <c>workspace_tab</c>
    /// plus the live publish checklist (a projection of the Phase-14 §14.2 gates). Owner/Manager/Representative or
    /// admin; a non-member gets <c>not_found</c> (drafts never leak their existence). Read-only, computed fresh.</summary>
    Task<ServiceResult<EventWorkspaceView>> GetWorkspaceAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Owner/Manager/Admin aggregate summary for one event (D-053): counts (views, ticket types,
    /// tickets issued, checked-in, paid orders) + gross revenue, derived live from orders/tickets/ticket
    /// types. Read-only; returns aggregate counts only — no per-attendee PII. Same 404-not-403 hiding.</summary>
    Task<ServiceResult<EventAnalytics>> GetAnalyticsAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Owner/Manager/Admin only, and only while the event is still Draft.</summary>
    Task<ServiceResult<bool>> DeleteDraftAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <param name="viewerUserId">Null for an anonymous/public caller. A non-Published event is only
    /// visible to a member of its org (any role) or a platform admin — anonymous or non-member callers
    /// get "not_found", never a 403, so drafts never leak their existence.</param>
    Task<ServiceResult<EventDetail>> GetAsync(Guid eventId, Guid? viewerUserId, bool isAdmin, CancellationToken ct = default);

    /// <param name="viewVisitorKey">Non-null only from the public event-detail endpoint: appends a row to
    /// the event-view stream (D-130) that the nightly rollup turns into views / unique visitors. Pass null
    /// from organizer and admin reads. This replaced a synchronous <c>ViewCount++</c> UPDATE on the events
    /// row — see <see cref="Kurx.Domain.Entities.EventView"/> for why the key is date-salted.</param>
    Task<ServiceResult<EventDetail>> GetBySlugAsync(string slug, Guid? viewerUserId, bool isAdmin, string? viewVisitorKey = null, CancellationToken ct = default);

    /// <summary>Public search/list — only Published events are visible here regardless of filter.</summary>
    Task<(IReadOnlyList<EventSummary> Items, int Total)> SearchAsync(EventListFilter filter, CancellationToken ct = default);

    /// <summary>Admin approval queue (M8, D-057): every event awaiting review across all orgs — D-266 M4's
    /// PendingReview and UnderReview — oldest first, with its org name. Reviewer-gated at the endpoint;
    /// approve/reject reuse the transition path.</summary>
    Task<IReadOnlyList<PendingEventView>> ListInReviewAsync(int limit, CancellationToken ct = default);

    /// <summary>D-266 M4 — one event's review history, newest first. Reads <c>VerificationReview</c>, the
    /// existing decision store; there is deliberately no second review table, so "what did reviewers
    /// decide" is one query rather than a join across parallel histories.</summary>
    Task<ServiceResult<IReadOnlyList<EventReviewEntry>>> GetReviewHistoryAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>D-266 M4 — how many events sit in each review state, for the admin queue's tab counts.
    /// Derived from event status rather than stored, so it can never drift from the queue it describes.</summary>
    Task<EventReviewCounts> GetReviewCountsAsync(CancellationToken ct = default);

    /// <summary>D-266 M7 — FinanceOps clears (or refuses) the money path on an event whose archetype
    /// requires it (D12 §6, A11 Fundraising). Authorization is the endpoint's <c>FinanceOps</c> policy;
    /// this performs none of its own, matching the rest of the admin review surface.
    /// <para>Returns <c>notes_required</c> when refusing without a reason the organiser can act on.</para></summary>
    Task<ServiceResult<EventDetail>> RecordFinancialReviewAsync(Guid reviewerId, Guid eventId, bool passed,
        string? notes, CancellationToken ct = default);

    /// <summary>D-266 M8 — save the wizard's in-progress form for this caller. Upsert per (event, user).</summary>
    Task<ServiceResult<EventDraftView>> SaveDraftAsync(Guid userId, Guid eventId, bool isAdmin,
        string payloadJson, string? stepKey, CancellationToken ct = default);

    /// <summary>The caller's autosaved draft, or a null Value when none exists.</summary>
    Task<ServiceResult<EventDraftView?>> GetDraftAsync(Guid userId, Guid eventId, bool isAdmin,
        CancellationToken ct = default);

    /// <summary>Global admin event management (D-061/D-186/D-187): search every non-deleted event across all
    /// orgs and statuses with the full admin filter set, newest-first (or per <see cref="AdminEventListFilter.Sort"/>),
    /// real-paginated with an accurate <c>Total</c> — so a platform with more events than one page's <c>Limit</c>
    /// never silently truncates older rows (D-187: the previous shape had no <c>Page</c> and hard-capped at 100
    /// with no way to reach row 101).</summary>
    Task<(IReadOnlyList<AdminEventView> Items, int Total)> ListForAdminAsync(AdminEventListFilter filter, CancellationToken ct = default);

    /// <summary>Feature / unfeature an event for the public discovery surfaces (D-061). Admin curation.</summary>
    Task<ServiceResult<AdminEventView>> SetFeaturedAsync(Guid eventId, bool featured, CancellationToken ct = default);

    /// <summary>Admin moderation overrides (D-186) — orthogonal to <see cref="Event.Status"/>, same shape as
    /// <see cref="SetFeaturedAsync"/>. A suspended/hidden event keeps its real status and stays visible to its
    /// own org members; only public discovery/detail is gated (<c>CanViewAsync</c>) and the search index drops it.</summary>
    Task<ServiceResult<AdminEventView>> SuspendAsync(Guid eventId, string? reason, CancellationToken ct = default);
    Task<ServiceResult<AdminEventView>> UnsuspendAsync(Guid eventId, CancellationToken ct = default);
    Task<ServiceResult<AdminEventView>> HideAsync(Guid eventId, string? reason, CancellationToken ct = default);
    Task<ServiceResult<AdminEventView>> UnhideAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Admin action (D-186): message or warn the event's creator via the existing notification
    /// pipeline (<c>kind</c> is the notification kind, e.g. <c>admin.message</c>/<c>admin.warning</c>), and
    /// write a typed audit event. Composition only — resolves <c>Event.CreatedBy</c> (not exposed on the
    /// public <see cref="EventDetail"/> DTO) and calls straight through to <c>INotificationService</c>.</summary>
    Task<ServiceResult<bool>> NotifyOrganizerAsync(Guid eventId, Guid actorId, string kind, string title, string message, CancellationToken ct = default);

    Task<ServiceResult<(IReadOnlyList<OrgEventRow> Items, int Total)>> ListForOrgAsync(Guid userId, Guid orgId, bool isAdmin, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Every event the caller hosts, across every organization they hold a seat in, newest first.
    /// The user-first counterpart to <see cref="ListForOrgAsync"/> (which stays for the admin console's
    /// per-org view): a person's Workspace is a list of THEIR events, so the client never has to enumerate
    /// organizations to find them. The representing organization travels on the row as metadata rather than
    /// as the container the caller had to open first.</summary>
    Task<(IReadOnlyList<MyEventRow> Items, int Total)> ListMineAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<EventSummary>> UpcomingAsync(int limit, CancellationToken ct = default);

    Task<IReadOnlyList<EventSummary>> TrendingAsync(int limit, CancellationToken ct = default);

    Task<IReadOnlyList<EventSummary>> FeaturedAsync(int limit, CancellationToken ct = default);

    Task<IReadOnlyList<EventSummary>> LatestAsync(int limit, CancellationToken ct = default);

    Task<IReadOnlyList<EventSummary>> RelatedAsync(Guid eventId, int limit, CancellationToken ct = default);
}
