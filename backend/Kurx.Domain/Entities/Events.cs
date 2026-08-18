using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>D-188 (Platform Taxonomy Management): a node in the Audience → Category → Type tree. Was
/// developer-seeded-only (<see cref="Kurx.Infrastructure.Events.EventTaxonomySeeder"/>); now fully
/// admin-manageable via <see cref="Kurx.Application.Abstractions.ICategoryService"/>. <see cref="Status"/>
/// (lifecycle) and <see cref="IsVisible"/> (display) are deliberately separate axes — a node can be
/// Active-but-hidden (usable by historical events, absent from new-event pickers). <see cref="Slug"/> is
/// permanently immutable after creation (never changes on rename) — historical <c>Event</c> rows reference
/// this row by <see cref="Id"/>, never by slug, so no edit here can ever orphan a past event's displayed
/// taxonomy. <see cref="MetadataJson"/> is a deliberate escape hatch for genuinely unanticipated future
/// fields only — every field already asked for gets a real column, never JSON.</summary>
public class EventCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }
    public CategoryLevel Level { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public int Sort { get; set; }
    /// <summary>Display-only — whether this node appears in the Create Event picker. Independent of
    /// <see cref="Status"/>; see the type doc comment.</summary>
    public bool IsVisible { get; set; } = true;
    public CategoryStatus Status { get; set; } = CategoryStatus.Active;
    public string? Description { get; set; }
    /// <summary>A storage key (not a URL) — same pattern as Speaker.PhotoKey/Sponsor.LogoKey. No admin
    /// upload widget exists yet; this is populated by pasting an existing key.</summary>
    public string? IconKey { get; set; }
    public string? Color { get; set; }
    public string? Badge { get; set; }
    /// <summary>Comma-separated search aliases (e.g. "hack,codefest" for "Hackathon").</summary>
    public string? SearchKeywords { get; set; }
    /// <summary>Escape hatch only — see the type doc comment. Never a field already named elsewhere in
    /// this entity.</summary>
    public string? MetadataJson { get; set; }

    // ── D-266 (M1) — set on Type-level nodes; the admin console owns these after first boot ──
    /// <summary>Which archetype an event of this Type behaves as. The single join between the
    /// human-facing taxonomy (104 types) and the behaviour model (14 archetypes).</summary>
    public string? ArchetypeSlug { get; set; }
    /// <summary>Public or Private product for this Type. A Wedding can never be Public; a Fundraiser
    /// can never be Private — the constraint lives here as data, not as code.</summary>
    public EventProduct? ProductClass { get; set; }
    /// <summary>jsonb string[] of <see cref="Kurx.Domain.Enums.EventRegistrationPolicy"/> names this Type
    /// permits. Null = inherit the archetype default. No type supports every policy: a Blood Drive is
    /// Open-only, a Campus Recruitment is CollegeRestricted/InviteOnly.</summary>
    public string? AllowedRegistrationPoliciesJson { get; set; }

    /// <summary>Optimistic-concurrency / point-in-time marker, incremented on every mutation. Combined with
    /// the full-row audit snapshot every mutation writes, this is what makes "what did this node look like
    /// at version N" answerable later without a dedicated version-history table today (D-188).</summary>
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Null for bootstrap-seeded rows (no actor) or rows created before this column existed.</summary>
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>D-188 — "this Event Type supports X" (Team Registration, Certificates, Judging, ...), reusing
/// the existing V3 <see cref="Capability"/> catalog rather than inventing new capability names. Deliberately
/// keyed by <see cref="CategoryNodeId"/> (a real FK to <see cref="EventCategory.Id"/>, not DB-constrained to
/// any one <see cref="CategoryLevel"/>) rather than by <c>TypeId</c> specifically — this pass's service layer
/// only writes/reads Type-level rows, but a future capability-inheritance pass (Audience → Category → Type
/// cascading defaults) can add Audience-/Category-level rows on this SAME table with no migration. This is
/// also the intended long-term replacement for the fragile name-matched <c>Event.KindSlug</c> →
/// <c>KindAlias</c> → <c>KindCapabilityDefault</c> chain (see <see cref="KindCapabilityDefault"/>) — not
/// wired into live event-capability resolution yet; that is a separate, future, riskier change.</summary>
public class CategoryCapabilityDefault
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryNodeId { get; set; }              // → EventCategory.Id
    public string CapabilitySlug { get; set; } = null!;   // → Capability.Slug
    public CapabilityState State { get; set; } = CapabilityState.On;
}

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The organization this event <b>REPRESENTS — not its owner</b> (D-268/D-273a). The owner is
    /// always <see cref="CreatedBy"/>. Representation affects branding, verification, trust, permissions and
    /// settlement; it never confers ownership.
    ///
    /// <para>Non-null, so a self-represented ("Personal") event points at an internal <c>IsPersonal</c> row
    /// that is never surfaced. Nullability was weighed again in D-273a and deferred to D-273b: the money
    /// spine (<c>LedgerEntry</c>, <c>OrganizationWallet</c>, <c>PayoutSchedule</c>) is keyed non-nullably on
    /// an org, so a null representation would leave a captured payment with nowhere to settle.</para>
    ///
    /// <para><b>Storage note (D-273a):</b> the physical column is still <c>"OrgId"</c>. The property was
    /// renamed without a schema change so the application can be rolled back independently of the database;
    /// the column name is now an internal storage detail and appears nowhere on the wire.</para>
    ///
    /// <para>Never authorize on this directly — use <c>IEventAuthority</c> (D-269).</para></summary>
    public Guid RepresentingOrgId { get; set; }

    public Guid? ParentEventId { get; set; }            // COMPOSITION (§3): fest → sub-events; depth ≤ 3 (§3.4, enforced in the API)

    /// <summary><b>The event's OWNER</b> (D-268). Kurx is user-first: a user owns an event, an
    /// organization never does. Every event authorization checks this first and it stands alone — the
    /// creator needs no membership anywhere, which is what makes a self-represented event manageable.
    /// Resolved through <c>IEventAuthority</c> (D-269), never re-implemented per service.</summary>
    public Guid CreatedBy { get; set; }

    // V3 §3/§13.2 (Phase 12) — LINEAGE + structural discovery. An event belongs to at most one EventSeries (§3.4
    // rule 5; series never nest). EDITIONS members carry an ordinal/label; RECURRING occurrences reuse StartsAt +
    // Timezone (per-occurrence timezone). ListedStandalone drives §3.4 rule 2 — roots/editions are discoverable by
    // default, a sub-event surfaces inside its parent and appears in standalone discovery only on opt-in.
    public Guid? SeriesId { get; set; }
    public int? EditionOrdinal { get; set; }
    public string? EditionLabel { get; set; }
    public bool ListedStandalone { get; set; } = true;

    // Content
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string ShortCode { get; set; } = null!;      // unique 6-char, voice/SMS-friendly (D-036)
    public string Subtitle { get; set; } = "";
    public string Description { get; set; } = "";
    public string Language { get; set; } = "en";

    // ── Presentation content (D-265) ─────────────────────────────────────────
    // Distinct from Subtitle, which predates these and is the card's one-liner. Tagline is the
    // marketing hook shown under the title on the detail page; ShortDescription is the ≤300-char
    // blurb used in listings and share previews where Description is far too long.
    public string? Tagline { get; set; }                // ≤160
    public string? ShortDescription { get; set; }       // ≤300
    /// <summary>Storage keys, not URLs — presigned on read, same as <see cref="BannerKey"/>.</summary>
    public string? LogoKey { get; set; }
    public string? ThumbnailKey { get; set; }
    public string? PromoVideoKey { get; set; }
    /// <summary>Free-text rules. Separate from the <c>RulesPdf</c> media kind, which was the only
    /// way to publish rules before this: a PDF is unreadable on a phone and unsearchable.</summary>
    public string? Rules { get; set; }
    public string? FaqJson { get; set; }                // jsonb [{q,a}]

    // ── Legal (D-265) ────────────────────────────────────────────────────────
    // The platform's own terms always apply; these are the ORGANISER's additional terms for this
    // event. Null means "nothing beyond the platform terms", never "no terms".
    public string? TermsUrl { get; set; }
    public string? TermsText { get; set; }
    public string? CodeOfConduct { get; set; }
    public string? RefundPolicy { get; set; }
    public string? CancellationPolicy { get; set; }
    /// <summary>When true a registrant must accept <see cref="ConsentText"/>, and the acceptance is
    /// evidenced in <c>registration_consents</c>. Consent that cannot be evidenced is not consent.</summary>
    public bool RequiresConsent { get; set; }
    public string? ConsentText { get; set; }

    // V3 §4.1 (Phase 4): the OrgUnit this event is FILED UNDER within the represented org's structural
    // tree — a placement, not ownership (the owner is CreatedBy, D-268). Auto-set to the org's root unit —
    // a one-node tree "just works" and the picker never renders. Nullable: rows before the backfill, or
    // events seeded outside the service (dev/test), may carry none. Placement only; who may register is a
    // separate AudienceRule (§4.4, Phase 5).
    public Guid? OrgUnitId { get; set; }

    // Taxonomy / template
    public Guid? AudienceLevelId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? TypeId { get; set; }
    public Guid? TemplateId { get; set; }

    // V3 Kind registry (Event Architecture V3 §2, Phase 1). The analysable Kind slug (event_kinds),
    // derived from the selected Type/Category via the data-driven kind_aliases map. Additive: the legacy
    // Category/Type/AudienceLevel FKs above are still written and nothing is removed this phase.
    public string? KindSlug { get; set; }

    // ── D-266 (M1): the behaviour axis that supersedes KindSlug ──────────────
    /// <summary>The <see cref="EventArchetype"/> this event behaves as, snapshot at creation from the
    /// selected Type. Snapshot rather than resolved-on-read for the same reason
    /// <c>created_from_template_version</c> is: a later taxonomy edit must never silently change how a
    /// live event behaves. Nullable only until the M1 backfill completes.</summary>
    public string? ArchetypeSlug { get; set; }

    /// <summary>Public or Private product (D-266 D8). Drives capabilities, moderation and
    /// discoverability. Derived from the selected Type at creation and immutable thereafter, except
    /// through the explicit Private→Public conversion — Public→Private is refused outright.</summary>
    public EventProduct Product { get; set; } = EventProduct.Public;

    /// <summary>D-266 M3 — the organiser's single declared answer to "who may register?". The per-ticket
    /// <see cref="RegistrationGate"/>s are DERIVED from this by <c>PolicyResolver</c>, never set alongside
    /// it: two independently-settable axes for one question is how they drift apart.</summary>
    public EventRegistrationPolicy RegistrationPolicy { get; set; } = EventRegistrationPolicy.Open;

    // ── D-266 M3 Step 6 — taxonomy attributes (D11 §4) ───────────────────────────────────────
    // D11 collapsed 20 types into 4 because the distinction between them is descriptive, not
    // behavioural: a cricket tournament and a chess tournament run identically, so "which sport" is a
    // property of the event rather than a reason for a separate type. These are real columns, not JSON,
    // per the convention stated on EventCategory.MetadataJson — every field already asked for gets a
    // column. All four are search-, filter- and analytics-bearing, and none affects capability or
    // archetype resolution.

    /// <summary>Sports Tournament: Cricket, Football, Chess, Athletics, … (was 8 separate types).</summary>
    public string? Sport { get; set; }

    /// <summary>Creative Competition (Art, Photography, Film, Writing, Design) and Performance
    /// Competition (Music, Dance, Talent, Fashion). The two archetypes stay separate because submission
    /// and staged judging are different workflows; only the discipline is the attribute.</summary>
    public string? Discipline { get; set; }

    /// <summary>Exhibition: Art, Photography, … (was 3 separate types).</summary>
    public string? Subject { get; set; }

    /// <summary>Wedding: Destination. A destination wedding is a wedding with a location.</summary>
    public string? VenueType { get; set; }

    // ── D-266 M7 — financial review (D12 §6, A11 Fundraising) ────────────────
    // Columns rather than a table: this is one decision with no documents and no history beyond the audit
    // spine, and "has this event been financially cleared?" is a property of the event. The institutional
    // authorization earned its own entity because it carries evidence; this does not.

    /// <summary>Null = never reviewed. <c>Passed</c> is what clears the publish blocker for an archetype
    /// whose <c>RequiresFinancialReview</c> is set.</summary>
    public FinancialReviewStatus? FinancialReviewStatus { get; set; }
    public Guid? FinancialReviewedBy { get; set; }
    public DateTime? FinancialReviewedAt { get; set; }
    /// <summary>FinanceOps' note. Organiser-facing when the review fails — a refusal they cannot act on is
    /// not a review outcome, it is a dead end.</summary>
    public string? FinancialReviewNotes { get; set; }

    // ── D-266 M4 — who holds this item in the review queue ───────────────────
    // `UnderReview` said an item was claimed but never by WHOM, so two reviewers could both hold it, each
    // building their own checklist, neither able to see the other. Claiming is the queue handing work to
    // one person; a claim nobody owns hands it to everybody.

    /// <summary>The reviewer currently holding this item, set by <c>claim_review</c> and cleared on release
    /// or on a decision. Null whenever the event is not <c>UnderReview</c>.</summary>
    public Guid? ReviewClaimedBy { get; set; }

    /// <summary>When it was claimed — what an operator needs to judge whether a hold has gone stale.</summary>
    public DateTime? ReviewClaimedAt { get; set; }

    // Venue: either a saved Venue (VenueId set) or ad-hoc text/coordinates below.
    public Guid? VenueId { get; set; }
    public string VenueName { get; set; } = "";
    public string VenueAddress { get; set; } = "";
    public string City { get; set; } = "";               // denormalized from Venue.City when VenueId is set
    // Full location (country/state/district required for India-scale discovery + GST jurisdiction)
    public string Country { get; set; } = "IN";          // ISO 3166-1 alpha-2
    public string? State { get; set; }
    public string? District { get; set; }
    public string? PostalCode { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }

    // ── Location detail (D-265) ──────────────────────────────────────────────
    // "Which room" is the single most-asked question on the day of a campus event, and there was
    // nowhere to put the answer.
    public string? Building { get; set; }
    public string? Floor { get; set; }
    public string? Room { get; set; }
    public string? GoogleMapsUrl { get; set; }
    /// <summary>Online delivery platform label (Zoom, Meet, Teams…). The join link is <see cref="OnlineUrl"/>.</summary>
    public string? MeetingPlatform { get; set; }
    /// <summary>SECRET. Never logged, and only ever returned to a confirmed registrant — the whole
    /// point of a meeting password is that the public listing does not carry it.</summary>
    public string? MeetingPassword { get; set; }

    // Schedule
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Timezone { get; set; } = "Asia/Kolkata";

    // ── Lifecycle windows (D-265) ────────────────────────────────────────────
    // Registration windows existed only per ticket type (TicketType.SaleStarts/SaleEnds). That works
    // for selling but cannot express "registration for this event closes Friday" across every ticket
    // type, which is what organisers actually announce. Event-level here is the outer bound; a
    // ticket's own sale window narrows it further, never widens it.
    public DateTime? RegistrationOpensAt { get; set; }
    public DateTime? RegistrationClosesAt { get; set; }
    public DateTime? CheckinOpensAt { get; set; }
    public DateTime? CheckinClosesAt { get; set; }
    public DateTime? ResultDate { get; set; }
    public DateTime? CertificateReleaseAt { get; set; }
    /// <summary>Close registration automatically once <see cref="Capacity"/> is reached, rather than
    /// leaving a full event accepting registrations into a waitlist the organiser never wanted.</summary>
    public bool AutoClose { get; set; }

    public int? Capacity { get; set; }

    // ── Eligibility (D-265) ──────────────────────────────────────────────────
    // Demographic gates, deliberately separate from AudienceRule (§4.4), which is the org-membership
    // predicate. Both may apply; neither implies the other. Enforced server-side at registration.
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public GenderRestriction GenderRestriction { get; set; } = GenderRestriction.Any;
    /// <summary>Event-wide cap on the number of teams. Distinct from
    /// <c>TeamPolicy.MaxTeamsPerPersonInEvent</c>, which caps how many teams one PERSON may join.</summary>
    public int? MaxTeams { get; set; }

    // ── Commerce (D-265) ─────────────────────────────────────────────────────
    // Nullable = "use the platform default"; a stored 0 means a deliberate waiver, which is why these
    // are not non-nullable with a 0 default.
    public decimal? PlatformFeePercent { get; set; }
    public long? PlatformFeeFlatPaise { get; set; }
    public decimal? TaxPercent { get; set; }
    /// <summary>True = the displayed price already includes tax (the Indian retail convention).</summary>
    public bool TaxInclusive { get; set; } = true;
    /// <summary>Descriptive only — jsonb {cash_paise, goodies[], travel, accommodation, certificates}.
    /// Nothing pays out from this; it is what the listing advertises.</summary>
    public string? PrizePoolJson { get; set; }
    public EventVisibility Visibility { get; set; } = EventVisibility.Listed;
    public EventStatus Status { get; set; } = EventStatus.Draft;
    // Delivery mode (D-064 A7). Online/Hybrid carry an OnlineUrl; existing rows default Offline.
    public EventMode EventMode { get; set; } = EventMode.Offline;
    public string? OnlineUrl { get; set; }

    // Contact / links
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string Website { get; set; } = "";
    public string? SocialLinksJson { get; set; }

    public string? BannerKey { get; set; }

    // V3 §9.1 — the single currency this event settles in (ISO-4217), bound from its Org at creation and
    // immutable once money moves. Additive; INR today (multi-currency settlement is out of scope).
    public string SettlementCurrency { get; set; } = Money.DefaultCurrency;

    // Reserved for later phases (ticketing/certificates) — kept so those tables' FKs stay valid.
    public bool IsPaid { get; set; }
    public bool CertificatesEnabled { get; set; }
    public Guid? CertificateTemplateId { get; set; }
    public Guid? InviteTemplateId { get; set; }

    // Public-API engagement signals (D-018).
    public bool IsFeatured { get; set; }
    public int ViewCount { get; set; }
    public bool TransfersEnabled { get; set; } = true;

    // Admin moderation overrides (D-186) — orthogonal to Status, same shape as IsFeatured. A
    // suspended/hidden event keeps reporting its real Status; only public visibility is gated.
    public bool IsSuspended { get; set; }
    public string? SuspendedReason { get; set; }
    public bool IsHidden { get; set; }
    public string? HiddenReason { get; set; }

    // V3 §14.5 (Phase 14): a material change (date/venue/mode/sub-event cancellation) after any Registration exists
    // opens a refund window until this deadline; refunds within it are registrant-initiated via the existing RefundService.
    public DateTime? RefundWindowEndsAt { get; set; }

    // V3 §13.1 (Phase 15): the template version this event was snapshot-created from (with TemplateId = the template
    // root). Snapshot-applied at creation, so a later template edit never mutates this event.
    public int? CreatedFromTemplateVersion { get; set; }

    /// <summary>D-388 — the content version, incremented by the one-and-only apply path
    /// (<c>EventService.ApplyUpdateAsync</c>) on every change to the event row.
    ///
    /// <para>Exists so an <see cref="EventChangeRequest"/> can name the state it was authored against and
    /// be refused if the event has moved since. <see cref="UpdatedAt"/> could not serve: it is bumped by
    /// writes that change no content — the D-363 §4 review reopen sets it — so a pending request would go
    /// stale for a reason its author could not see.</para>
    ///
    /// <para>Not an EF concurrency token. It counts <i>content</i> changes for the approval workflow to
    /// reason about; making it <c>IsConcurrencyToken</c> would additionally fail unrelated concurrent
    /// writes across ~169 query sites, which is a different (and unrequested) change.</para></summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    /// <summary>Field-for-field copy, used by event cloning (D-340). The caller then resets the fields
    /// that must not travel — the inverse of the allowlist this replaced, which copied 47 of 110 columns
    /// and silently blanked the rest because every D-265/D-266 batch that added a column forgot to extend
    /// it. Copy-by-default fails loudly (a wrong value someone reports) instead of quietly (config loss
    /// nobody sees). Safe as a shallow copy: <c>Event</c> declares no navigation collections.</summary>
    public Event ShallowCopy() => (Event)MemberwiseClone();
}

public class TicketType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public string Name { get; set; } = null!;
    public long PricePaise { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;       // ISO-4217; the event's settlement currency (V3 §9.1)
    public PricingUnit PricingUnit { get; set; } = PricingUnit.PerTicket;
    public RegistrationMode RegistrationMode { get; set; } = RegistrationMode.Individual;
    public int? GroupMin { get; set; }
    public int? GroupMax { get; set; }
    public int Quantity { get; set; }
    public int Sold { get; set; }
    public DateTime SaleStarts { get; set; }
    public DateTime SaleEnds { get; set; }
    public int PerUserLimit { get; set; } = 5;
    public bool IsAllAccess { get; set; }               // parent-fest pass valid at every child gate
    public bool IsCompetition { get; set; }             // account always required; team joins are invite-only (D-036)

    // ── D-265 ────────────────────────────────────────────────────────────────
    /// <summary>What this ticket IS, orthogonal to <see cref="PricePaise"/>. Existing rows are Free
    /// or Paid by price, which is exactly what the backfill sets, so nothing changes for them.</summary>
    public TicketKind Kind { get; set; } = TicketKind.Free;
    /// <summary>Donation floor. <see cref="PricePaise"/> stays the suggested/default amount.</summary>
    public long? MinAmountPaise { get; set; }
    /// <summary>jsonb long[] — the quick-pick amounts a donation form offers.</summary>
    public string? SuggestedAmountsJson { get; set; }
    /// <summary>Per-ticket refund terms, overriding the event's when set.</summary>
    public string? RefundPolicy { get; set; }

    public DateTime? DeletedAt { get; set; }
}

/// <summary>D-366 — what a team of a given size pays.
///
/// <para><see cref="TicketType.PricePaise"/> is one number for a whole <c>GroupMin..GroupMax</c> range,
/// so every team size in it costs the same. Organisers price by size — 2 → ₹250, 3 → ₹300, 4–5 → ₹400 —
/// which is a set of rules, not a number. A min/max range describes *eligibility*; overloading it to
/// mean a price curve would be a lie in the schema.</para>
///
/// <para>Bands rather than single sizes, because a range is the general form and <c>Min == Max</c>
/// expresses the specific one. Overlap is prevented by a Postgres exclusion constraint over
/// <c>int4range(MinSize, MaxSize, '[]')</c> per ticket type, so "two rules match this team" cannot exist
/// in the database — and <see cref="TicketType.Quantity"/> stays one pool meaning "how many teams", which
/// is what a ticket-type-per-size would have destroyed.</para>
///
/// <para><b>No tiers means unchanged:</b> a ticket type with none prices from <c>PricePaise</c> exactly
/// as it always has, so every event that predates this is untouched.</para></summary>
public class TicketPriceTier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketTypeId { get; set; }
    /// <summary>Inclusive, both ends. A team whose size falls inside pays <see cref="PricePaise"/> in
    /// total — never multiplied by the roster, which is the D-372 rule this must not reintroduce.</summary>
    public int MinSize { get; set; }
    public int MaxSize { get; set; }
    public long PricePaise { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class FormField
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketTypeId { get; set; }
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
    public FormFieldType Type { get; set; }
    public FormFieldScope Scope { get; set; }
    public bool Required { get; set; }
    public string? OptionsJson { get; set; }            // for select: ["S","M","L"]
    public int Sort { get; set; }
}

public class FieldPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CategoryOrTypeSlug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;    // {registration_mode, group_min/max, pricing_unit, fields:[...]}
}

/// <summary>Flexible event staff assignment. One user can hold multiple roles at one event.
/// Accepted assignments automatically appear on the user's public profile experience section.</summary>
public class EventAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid OrgId { get; set; }
    public Guid UserId { get; set; }
    public Guid? InvitedBy { get; set; }
    // Role: one of 14 standard values or "Custom". Stored as text for extensibility.
    // Valid: Volunteer, Judge, Moderator, Registration Desk, Stage Manager, Security,
    //        Photographer, Videographer, Host, Media Team, Speaker Coordinator,
    //        Technical Team, Support Team, Custom
    public string Role { get; set; } = null!;
    public string? CustomRole { get; set; }             // populated when Role = "Custom"
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Invited;
    public bool ShowOnProfile { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>User bookmarks / saved events for later discovery.</summary>
public class SavedEvent
{
    public Guid UserId { get; set; }
    public Guid EventId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Post-event rating submitted by a verified ticket holder (one per user per event).</summary>
public class EventReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public Guid? TicketId { get; set; }
    public int Rating { get; set; }                     // 1–5
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsVerified { get; set; }                // true when TicketId confirmed to belong to UserId
    public EventReviewStatus Status { get; set; } = EventReviewStatus.Published;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Waitlist slot when a ticket type is sold out. Position is 1-based; a background job
/// advances the queue when capacity opens and notifies the next waiting user.</summary>
public class TicketWaitlist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    // V3 §8.5 (Phase 7): a waitlist is attached to a POOL, not an event — a VIP queue and a general queue are
    // different. Re-pointed to the ticket type's general inventory pool; nullable for rows before the backfill.
    public Guid? PoolId { get; set; }
    public Guid UserId { get; set; }
    public int Position { get; set; }
    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;
    public DateTime? NotifiedAt { get; set; }
    public DateTime? OfferExpiresAt { get; set; }       // user has 30 min to purchase after notified
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Hardware gate scanner registered for an event. DeviceTokenHash is a hashed secret
/// used by the scanning tablet to authenticate to ScanHub without a full user account.</summary>
public class EventCheckinDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid OrgId { get; set; }
    public Guid RegisteredBy { get; set; }
    public string DeviceName { get; set; } = null!;
    public string DeviceTokenHash { get; set; } = null!; // bcrypt/SHA-256 hash — never plaintext
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
