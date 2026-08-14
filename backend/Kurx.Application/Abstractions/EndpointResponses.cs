namespace Kurx.Application.Abstractions;

// Wire contracts for endpoints whose hand-written mapper *translated* its View rather than merely
// renaming it (D-259 hardening, Stage D). Each record's property names snake_case — via
// SnakeCaseResponseConverter — to exactly the keys the anonymous object emitted, and each mapper now
// constructs one of these with the SAME expressions it always used. Nothing about the JSON moved.
//
// These are separate types rather than "just return the View" because the mappers do real work the View
// cannot express: 57 `.ToLowerInvariant()` calls across the API turn a PascalCase enum-ish string into
// the lowercase token the wire has always carried ("published", not "Published"), and a handful rename
// (`org_id` <- RepresentingOrgId, `ticket_type` <- TicketTypeName). Returning the View directly would
// silently change every one of those values.
//
// They live in this namespace because SnakeCaseResponseConverter selects response types BY NAMESPACE; a
// copy declared beside an endpoint would serialize camelCase and break every client with a green build.

/// <summary>Wire shape of <see cref="AdminEventView"/> as <c>ToAdminJson</c> has always emitted it.</summary>
public sealed record AdminEventResponse(
    Guid EventId,
    Guid RepresentingOrgId,
    Guid OrgId,
    string Title,
    string Slug,
    string Status,
    bool IsFeatured,
    DateTime StartsAt,
    DateTime CreatedAt,
    string? Category,
    string? Subcategory,
    string Visibility,
    string City,
    string VenueName,
    int? Capacity,
    DateTime EndsAt,
    bool IsPaid,
    string OrgVerification,
    DateTime UpdatedAt,
    bool IsSuspended,
    string? SuspendedReason,
    bool IsHidden,
    string? HiddenReason,
    string? BannerKey,
    int TicketsSold,
    int CheckedIn,
    int RegistrationsCount,
    long RevenuePaise,
    string Currency);

/// <summary>Wire shape of <see cref="BlacklistEntryView"/> as <c>ToJson</c> has always emitted it.</summary>
public sealed record BlacklistEntryResponse(
    Guid Id,
    string Kind,
    string Value,
    string? Reason,
    DateTime CreatedAt);

/// <summary>Wire shape of <see cref="AdminOrgView"/> as <c>ToAdminOrgJson</c> has always emitted it.</summary>
public sealed record AdminOrgResponse(
    Guid OrgId,
    string Name,
    string Slug,
    string? LogoKey,
    string Type,
    string VerificationStatus,
    string? PrimaryDomain,
    bool IsPersonal,
    int MemberCount,
    int EventCount,
    DateTime CreatedAt);

/// <summary>Wire shape of <see cref="OrgDetail"/> as <c>ToOrgDetailJson</c> has always emitted it.</summary>
public sealed record AdminOrgDetailResponse(
    Guid OrgId,
    string Name,
    string Slug,
    string? LogoKey,
    string? Bio,
    string? LinksJson,
    string PayoutAccountStatus,
    string? BankLast4,
    int Tier,
    string Type,
    string? PrimaryDomain,
    string VerificationStatus);

/// <summary>Wire shape of <see cref="CategoryView"/>.
///
/// <para>This said "as <c>ToJson</c> has always emitted it" and faithfully froze a seven-field mapper that
/// had never carried <see cref="ProductClass"/> — so D-313 typed the omission into the contract and the
/// drift gate went green over it (D-326). <see cref="CategoryView.ProductClass"/>'s own comment had already
/// warned that a product class no client can see is a mistake; the warning was one layer above the mapper
/// that dropped it. <b>When a field is added to <see cref="CategoryView"/>, add it here and to
/// <c>ToJson</c> — a view field with no wire field is invisible, and nothing fails.</b></para></summary>
/// <param name="ProductClass">"Public"/"Private" on a Type node, null elsewhere and on unclassified Types.
/// Trailing and optional so every existing construction site compiles unchanged. Emitted with its original
/// capitalisation: <c>SnakeCaseResponseConverter</c> renames keys, never values, and both clients compare
/// against <c>"Private"</c> exactly.</param>
public sealed record CategoryResponse(
    Guid Id,
    Guid? ParentId,
    string Level,
    string Name,
    string Slug,
    int Sort,
    bool IsVisible,
    string? ProductClass = null);

/// <summary>Wire shape of <see cref="AdminCategoryView"/>. Same D-326 correction as
/// <see cref="CategoryResponse"/> — the admin twin dropped <c>ProductClass</c> for the same reason, so the
/// taxonomy console could not show which product a Type belongs to either.</summary>
public sealed record AdminCategoryResponse(
    Guid Id,
    Guid? ParentId,
    string Level,
    string Name,
    string Slug,
    int Sort,
    bool IsVisible,
    string Status,
    string? Description,
    string? IconKey,
    string? Color,
    string? Badge,
    string? SearchKeywords,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? CreatedBy,
    Guid? UpdatedBy,
    int UsageCount,
    int? RegistrationsCount,
    int? AttendeesCount,
    long? RevenuePaise,
    int? ViewsCount,
    int? FavoritesCount,
    bool? IsTrending,
    /// <summary>D-326 — see <see cref="CategoryResponse.ProductClass"/>. Trailing so existing construction
    /// sites are unaffected.</summary>
    string? ProductClass = null);

/// <summary>Wire shape of <see cref="TaxonomyExportNode"/> as <c>ToJson</c> has always emitted it.</summary>
public sealed record TaxonomyExportNodeResponse(
    string Slug,
    string? ParentSlug,
    string Level,
    string Name,
    int Sort,
    bool IsVisible,
    string? Description,
    string? IconKey,
    string? Color,
    string? Badge,
    string? SearchKeywords);

/// <summary>Wire shape of <see cref="AssignmentView"/> as <c>ToJson</c> has always emitted it.</summary>
public sealed record AssignmentResponse(
    Guid Id,
    Guid EventId,
    Guid OrgId,
    Guid UserId,
    string Role,
    string? CustomRole,
    string Status,
    bool ShowOnProfile,
    string? Notes,
    DateTime CreatedAt,
    string AssigneeName,
    string? AssigneeUsername,
    string? AssigneeAvatarKey,
    // D-319 — event context, so /v1/me/assignments can render an invite the recipient can actually
    // identify. RepresentingOrgName is null for a self-represented event (D-268).
    string EventTitle,
    string? EventSlug,
    DateTime EventStartsAt,
    string? RepresentingOrgName);

/// <summary>Wire shape of <see cref="MembershipClaimView"/> as <c>ToJson</c> has always emitted it.</summary>
public sealed record MembershipClaimResponse(
    Guid Id,
    Guid OrgId,
    string OrgName,
    string OrgSlug,
    string ClaimedRole,
    string Status,
    bool FastTrack,
    DateTime? ValidUntil,
    DateTime? ReviewedAt,
    string? Notes,
    DateTime CreatedAt);

/// <summary>Wire shape of <see cref="TicketView"/> as <c>ToTicketJson</c> has always emitted it.</summary>
public sealed record TicketResponse(
    Guid Id,
    Guid Code,
    string State,
    DateTime? CheckedInAt,
    string? AnswersJson,
    DateTime CreatedAt);

/// <summary>Wire shape of <see cref="OrgDetail"/> as <c>ToOrgJson</c> has always emitted it.</summary>
public sealed record OrgDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    string? LogoKey,
    string? Bio,
    string? LinksJson,
    string PayoutAccountStatus,
    string? BankLast4,
    int Tier,
    string Role,
    string Type,
    string? PrimaryDomain,
    string VerificationStatus);

/// <summary>Wire shape of <see cref="OrgMember"/> as <c>ToMemberJson</c> has always emitted it.</summary>
public sealed record OrgMemberResponse(
    Guid UserId,
    string Phone,
    string Name,
    string? Username,
    string Role,
    DateTime JoinedAt,
    string? AvatarKey,
    bool IsVerified);

/// <summary>Wire shape of <see cref="KycOutcome"/> as <c>ToKycJson</c> has always emitted it.</summary>
public sealed record KycOutcomeResponse(
    string Status,
    string? Detail,
    string PayoutAccountStatus);

/// <summary>Wire shape of <see cref="OrgInvitationView"/> as <c>ToJson</c> has always emitted it.</summary>
public sealed record OrgInvitationResponse(
    Guid Id,
    Guid OrgId,
    string? OrgName,
    string? InvitedPhone,
    string Role,
    string Status,
    DateTime ExpiresAt,
    DateTime CreatedAt);

/// <summary>Wire shape of <see cref="RefundView"/> as <c>ToViewJson</c> has always emitted it.</summary>
public sealed record RefundResponse(
    Guid Id,
    Guid OrderId,
    Guid EventId,
    Guid OrgId,
    long AmountPaise,
    string Currency,
    string Reason,
    string Status,
    string? RazorpayRefundId,
    DateTime CreatedAt);

/// <summary>Wire shape of <see cref="SessionView"/> as <c>ToJson</c> has always emitted it.</summary>
public sealed record SessionResponse(
    Guid Id,
    Guid EventId,
    string Title,
    string Description,
    string Kind,
    DateTime StartsAt,
    DateTime EndsAt,
    int Sort,
    IReadOnlyList<Guid> SpeakerIds);

/// <summary>Wire shape of <see cref="SponsorView"/> as <c>ToJson</c> has always emitted it.</summary>
public sealed record SponsorResponse(
    Guid Id,
    Guid OrgId,
    string Name,
    string? LogoKey,
    string Website,
    string Tier,
    int Priority);

/// <summary>Wire shape of <see cref="WaitlistView"/> as <c>ToJson</c> has always emitted it.</summary>
public sealed record WaitlistResponse(
    Guid Id,
    Guid EventId,
    Guid TicketTypeId,
    int Position,
    string Status,
    DateTime? OfferExpiresAt,
    DateTime CreatedAt);

/// <summary>One uploaded verification document, as both org-verification mappers have always emitted it.
/// <c>status</c> is lower-cased from <see cref="OrgVerificationDocView.Status"/>.</summary>
public sealed record VerificationDocumentResponse(
    Guid Id,
    string DocType,
    string StorageKey,
    string Status,
    DateTime CreatedAt);

/// <summary>Wire shape of <see cref="OrgVerificationView"/>. Shared by the admin and org-facing routes,
/// which emitted byte-identical objects from two separate mappers.</summary>
public sealed record OrgVerificationResponse(
    Guid OrgId,
    string Name,
    string Slug,
    string Status,
    DateTime? ReviewedAt,
    string? Notes,
    IEnumerable<VerificationDocumentResponse> Documents);

/// <summary>Wire shape of <see cref="TaxonomyExport"/>.</summary>
public sealed record TaxonomyExportResponse(
    IEnumerable<TaxonomyExportNodeResponse> Nodes,
    DateTime ExportedAt);

/// <summary>Wire shape of <see cref="ImportPreview"/>.</summary>
public sealed record ImportPreviewResponse(
    IEnumerable<TaxonomyExportNodeResponse> ToCreate,
    IEnumerable<TaxonomyExportNodeResponse> ToUpdate,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> Errors);

/// <summary>Wire shape of <see cref="OrderView"/>. <c>ticket_type</c> is the ticket type's NAME
/// (<see cref="OrderView.TicketTypeName"/>) — the rename is the contract, not an accident.</summary>
public sealed record OrderResponse(
    Guid Id,
    Guid EventId,
    Guid TicketTypeId,
    string Status,
    long AmountPaise,
    string Currency,
    string? RazorpayOrderId,
    Guid? GroupId,
    string? JoinCode,
    DateTime CreatedAt,
    IEnumerable<TicketResponse> Tickets,
    string? GuestAccessToken,
    string? EventTitle,
    string? EventSlug,
    string? TicketType);

/// <summary>Wire shape of <see cref="GroupView"/>. <c>members</c> passes <see cref="GroupMemberView"/>
/// through unchanged — its old mapper was a proven 1:1 snake_case projection.</summary>
public sealed record GroupResponse(
    Guid Id,
    Guid EventId,
    Guid TicketTypeId,
    int GroupNumber,
    string? DisplayName,
    string JoinCode,
    Guid LeaderUserId,
    int Capacity,
    IReadOnlyList<GroupMemberView> Members);

/// <summary>Wire shape of <see cref="TeamView"/>. <c>members</c> passes <see cref="TeamMemberView"/>
/// through unchanged — the nested projection was 1:1 on all seven of its properties.</summary>
public sealed record TeamResponse(
    Guid Id,
    Guid EventId,
    Guid TicketTypeId,
    string Name,
    string Slug,
    string? LogoUrl,
    string? Tagline,
    Guid? DeclaredOrgUnitId,
    string State,
    Guid? MergedIntoTeamId,
    int ActiveMemberCount,
    DateTime CreatedAt,
    IReadOnlyList<TeamMemberView> Members);

/// <summary>One media item on an event, as <c>ToEventJson</c> has always emitted it.
///
/// <para>Deliberately NOT <see cref="EventMediaView"/> passed through: that record carries a
/// <c>Url</c> (the D-302 presigned companion) which this projection does not emit, so returning it
/// would add a field to a public wire. <c>kind</c> is lower-cased.</para></summary>
public sealed record EventMediaResponse(
    Guid Id,
    string Kind,
    string Key,
    string Caption,
    int Sort);

/// <summary>Wire shape of <see cref="EventDetail"/> — the platform's largest contract, served by the
/// public <c>GET /v1/events/{slug}</c> as well as every organiser read.
///
/// <para>Field order matches the mapper it replaces. <c>venue</c> passes <see cref="EventVenueView"/>
/// through unchanged (a proven 1:1 projection); <c>media</c> does not, see
/// <see cref="EventMediaResponse"/>. <c>visibility</c>, <c>status</c> and <c>event_mode</c> are
/// lower-cased — the wire has always carried <c>"published"</c>, never <c>"Published"</c>.</para>
///
/// <para><c>org_id</c> is DEPRECATED (D-273a) and duplicates <c>representing_org_id</c>; it is kept
/// because clients deployed before the rename still read it. It never meant ownership.</para></summary>
public sealed record EventDetailResponse(
    EventContentView? Content,
    EventLegalView? Legal,
    EventScheduleView? Schedule,
    EventLocationDetailView? LocationDetail,
    EventEligibilityView? Eligibility,
    EventCommerceView? Commerce,
    EventRepresentationView? Representing,
    string? BannerUrl,
    Guid Id,
    Guid RepresentingOrgId,
    Guid OrgId,
    Guid? ParentEventId,
    string Title,
    string Slug,
    string ShortCode,
    string Subtitle,
    string Description,
    Guid CategoryId,
    Guid? TypeId,
    Guid? AudienceLevelId,
    Guid? TemplateId,
    IReadOnlyList<string> Tags,
    EventVenueView Venue,
    DateTime StartsAt,
    DateTime EndsAt,
    string Timezone,
    int? Capacity,
    string Visibility,
    string Status,
    string EventMode,
    string? OnlineUrl,
    string SettlementCurrency,
    string Language,
    string ContactEmail,
    string ContactPhone,
    string Website,
    string? SocialLinksJson,
    string? BannerKey,
    bool IsFeatured,
    int ViewCount,
    IEnumerable<EventMediaResponse> Media,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    DateTime UpdatedAt);


/// <summary><c>{"revoked": true}</c> — an ally connection or invitation that has been revoked.
/// Distinct from <see cref="SessionsRevokedResult"/>, whose <c>revoked</c> is a COUNT.</summary>
public sealed record RevokedAck(bool Revoked);

/// <summary><c>{"revoked": n}</c> — how many sessions "sign out everywhere" actually ended. The key
/// collides with <see cref="RevokedAck"/> but the type does not, which is exactly why they are two
/// records: a client reading this as a boolean would see every non-zero count as `true`.</summary>
public sealed record SessionsRevokedResult(int Revoked);

/// <summary><c>{"saved": bool}</c> — whether the caller now has this post saved.</summary>
public sealed record SavedAck(bool Saved);

/// <summary><c>{"following": bool}</c> — whether the caller now follows this subject.</summary>
public sealed record FollowingAck(bool Following);

// ── Paged envelopes ────────────────────────────────────────────────────────────────────────────────
// `{items, total}` wrappers around a typed list. Named per-endpoint rather than made generic: a generic
// `Page<T>` produces a schema name Swashbuckle spells `PageOfAdminEventResponse`, which is not a name any
// of the three hand-written clients would recognise. This follows the existing `PostPage`/`PostAdminPage`
// convention already in the codebase.

/// <summary><c>GET /v1/admin/events</c>.</summary>
public sealed record AdminEventPage(IEnumerable<AdminEventResponse> Items, int Total);

/// <summary><c>GET /v1/admin/orgs</c>.</summary>
public sealed record AdminOrgPage(IEnumerable<AdminOrgResponse> Items, int Total);

/// <summary><c>GET /v1/admin/refunds</c>.</summary>
public sealed record AdminRefundPage(IEnumerable<RefundResponse> Items, int Total);

/// <summary><c>GET /v1/me/notifications</c>. Not a <c>total</c> — the second field is the caller's unread
/// count, which the bell badge reads.</summary>
public sealed record NotificationPage(IReadOnlyList<NotificationView> Items, int UnreadCount);

// ── Session tokens ─────────────────────────────────────────────────────────────────────────────────
// THREE distinct shapes, kept distinct on purpose (D-313). They differ only in which extra field they
// carry, and unifying them would ADD a field to endpoints that never sent it — an API change with no
// demonstrated need. Named separately so the contract states what each route actually returns.

/// <summary>The five-field session grant: password login, passkey login, recovery-code login, and the
/// device-approval path. Carries no <c>is_new_user</c> — these routes all sign in an existing account.</summary>
public sealed record SessionTokens(
    string AccessToken,
    DateTime AccessExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt,
    Guid? UserId);

/// <summary>The same grant plus the challenge's terminal <c>status</c>, returned by the device-approval
/// poll and the second-factor verify — both of which resolve a pending challenge rather than starting
/// one, so the caller needs to know how it ended.</summary>
public sealed record SecondFactorSession(
    string Status,
    string AccessToken,
    DateTime AccessExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt,
    Guid? UserId);

/// <summary>The OTP grant: <c>/v1/auth/otp/verify</c>, <c>/v1/auth/refresh</c> and the phone-change
/// confirm. The only shape carrying <c>is_new_user</c>, which the clients use to route a first-time
/// caller into onboarding — web's <c>tokenResponseSchema</c> declares it required.</summary>
public sealed record OtpSessionTokens(
    string AccessToken,
    DateTime AccessExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt,
    Guid? UserId,
    bool IsNewUser);

// ── Events: discovery, listings, workspace ─────────────────────────────────────────────────────────

/// <summary>Wire shape of <see cref="EventSummary"/> — the discovery card, returned by search,
/// for-you, upcoming, trending, featured, latest and related.
///
/// <para>Not <see cref="EventSummary"/> passed through: <c>status</c>, <c>visibility</c> and
/// <c>event_mode</c> are lower-cased (the wire has always carried <c>"published"</c>), and
/// <c>org_id</c> is a DEPRECATED duplicate of <c>representing_org_id</c> kept for clients deployed
/// before the D-273a rename. It never meant ownership — the owner is the user in <c>created_by</c>.</para></summary>
public sealed record EventSummaryResponse(
    Guid Id,
    Guid RepresentingOrgId,
    Guid OrgId,
    Guid? ParentEventId,
    string Title,
    string Slug,
    string ShortCode,
    string Subtitle,
    string? BannerKey,
    DateTime StartsAt,
    DateTime EndsAt,
    string Status,
    string Visibility,
    string VenueName,
    string City,
    string? BannerUrl,
    string EventMode,
    string? CategoryName,
    long? PriceFromPaise,
    string Currency,
    bool IsFeatured);

/// <summary><c>GET /v1/events</c> — the paged discovery feed.</summary>
public sealed record EventSummaryPage(IEnumerable<EventSummaryResponse> Items, int Total);

/// <summary><c>GET /v1/orgs/{orgId}/events</c>. <c>items</c> passes <see cref="OrgEventRow"/> through
/// unchanged — its old mapper was a proven 1:1 snake_case projection of all 16 properties.</summary>
public sealed record OrgEventPage(IReadOnlyList<OrgEventRow> Items, int Total);

/// <summary><c>GET /v1/me/events</c>. <c>items</c> passes <see cref="MyEventRow"/> through unchanged,
/// including its nested <see cref="RepresentationView"/>, which the old mapper projected 1:1.</summary>
public sealed record MyEventPage(IReadOnlyList<MyEventRow> Items, int Total);

/// <summary><c>GET /v1/orgs/{orgId}/events/{eventId}/attendees</c>.</summary>
public sealed record AttendeePage(IReadOnlyList<AttendeeRow> Items, int Total);

/// <summary>Wire shape of <see cref="ResolvedCapability"/>. <c>state</c> is lower-cased.</summary>
public sealed record ResolvedCapabilityResponse(
    string Slug,
    string Name,
    string GroupSlug,
    string State,
    string? WorkspaceTab);

// ── Admin console ──────────────────────────────────────────────────────────────────────────────────

/// <summary><c>GET /v1/admin/dashboard/summary</c> — the console's tile counts.
///
/// <para><c>new_users_24h</c> carries an explicit <c>[JsonPropertyName]</c>: the snake_case converter
/// turns <c>NewUsers24h</c> into <c>new_users24h</c>, because a digit does not start a new word. The
/// attribute survives the converter (it renames the already-emitted key) and preserves the wire.</para></summary>
public sealed record AdminDashboardSummary(
    int PendingOrgVerifications,
    int PendingMembershipClaims,
    int PendingEvents,
    int BlacklistEntries,
    int StaffCount,
    [property: System.Text.Json.Serialization.JsonPropertyName("new_users_24h")] int NewUsers24h,
    int TotalUsers,
    int TotalOrgs,
    int TotalEvents);

/// <summary>One row of <c>GET /v1/admin/audit</c>.</summary>
public sealed record AuditLogEntry(
    Guid Id,
    string ActorType,
    Guid? ActorId,
    string Action,
    string Entity,
    Guid? EntityId,
    string? Details,
    DateTime CreatedAt);

/// <summary>A day bucket in the platform analytics series. <c>date</c> is <c>yyyy-MM-dd</c>.</summary>
public sealed record AnalyticsDayCount(string Date, int Count);

/// <summary>Event count per lifecycle status; <c>status</c> is lower-cased.</summary>
public sealed record AnalyticsStatusCount(string Status, int Count);

/// <summary>An organization ranked by published events.</summary>
public sealed record AnalyticsTopOrganizer(Guid OrgId, string Name, int PublishedEvents);

/// <summary>An event ranked by view count, read from the EventViews leaf-fact table (V3 §16).</summary>
public sealed record AnalyticsTopEvent(Guid EventId, string Title, int Views);

/// <summary><c>GET /v1/admin/analytics</c>.</summary>
public sealed record PlatformAnalytics(
    int WindowDays,
    int TotalUsers,
    int TotalOrgs,
    int TotalEvents,
    int NewUsersInWindow,
    IEnumerable<AnalyticsDayCount> SignupsByDay,
    IEnumerable<AnalyticsDayCount> EventsByDay,
    IEnumerable<AnalyticsStatusCount> EventsByStatus,
    IEnumerable<AnalyticsTopOrganizer> TopOrganizers,
    IEnumerable<AnalyticsTopEvent> TopEvents);

/// <summary><c>GET /v1/admin/fraud-signals/score</c> — one subject's risk score.</summary>
public sealed record SubjectRiskScore(string SubjectType, Guid SubjectId, int RiskScore);

/// <summary>One entry of the batched <c>GET /v1/admin/fraud-signals/scores</c>. It deliberately omits
/// <c>subject_type</c>, which the caller supplied and the batch response has never echoed.</summary>
public sealed record SubjectRiskScoreEntry(Guid SubjectId, int RiskScore);

// ── Small singletons ───────────────────────────────────────────────────────────────────────────────

/// <summary>One key in the JWKS document (RFC 7517). Field names are fixed by the standard, not by
/// Kurx's snake_case convention — <c>kty</c>, <c>crv</c>, <c>x</c>, <c>y</c> are already lowercase and
/// single-token, so the converter passes them through unchanged.</summary>
public sealed record JsonWebKey(
    string Kty,
    string Crv,
    string Alg,
    string Use,
    string Kid,
    string X,
    string Y);

/// <summary><c>GET /.well-known/jwks.json</c> — the published ES256 verification keys (D-099).</summary>
public sealed record JwksDocument(IEnumerable<JsonWebKey> Keys);

/// <summary><c>GET /v1/chat/attachments/{id}/url</c> — a short-lived presigned URL.</summary>
public sealed record AttachmentUrl(string Url);

/// <summary><c>GET /v1/auth/password/status</c>. The two bounds are constants the sign-up form reads so
/// it can validate before a round trip; they are not user state.</summary>
public sealed record PasswordPolicyStatus(bool HasPassword, int MinLength, int MaxLength);

/// <summary>Wire shape of <see cref="PendingClaimView"/> for the admin queue. <c>claimed_role</c> and
/// <c>status</c> are lower-cased.</summary>
public sealed record PendingClaimResponse(
    Guid Id,
    Guid UserId,
    string UserName,
    string? Username,
    Guid OrgId,
    string OrgName,
    string ClaimedRole,
    string Status,
    bool FastTrack,
    DateTime CreatedAt);

// ── Organizations ──────────────────────────────────────────────────────────────────────────────────

/// <summary>One hit from the verified-organization registry search (D-043). <c>type</c> and
/// <c>verification_status</c> are lower-cased; <c>match</c> names which signal matched (name, alias,
/// domain), which is how the picker explains a fuzzy result.</summary>
public sealed record OrgSearchHit(
    Guid Id,
    string Name,
    string Slug,
    string? LogoKey,
    string Type,
    string? PrimaryDomain,
    string VerificationStatus,
    double Score,
    string Match);

/// <summary>An organization the caller may represent (D-075). <c>authority</c> is the caller's standing
/// to act for it — never a role over its events.</summary>
public sealed record RepresentableOrg(
    Guid OrganizationId,
    string Name,
    string Slug,
    string? LogoKey,
    string Authority);

/// <summary><c>GET /v1/orgs/{orgId}/my-capabilities</c> — the caller's live trust capabilities for one
/// organization (M7), resolved per request and never read from a token claim.</summary>
public sealed record OrgCapabilitiesResponse(
    bool CanRepresentOrg,
    bool IsOrgVerifiedRep,
    bool IsOrgVerified);

// ── Admin event review queue ───────────────────────────────────────────────────────────────────────

/// <summary>One row of the reviewer queue. Not <see cref="PendingEventView"/> passed through: the wire
/// also carries <c>org_id</c>, a DEPRECATED duplicate of <c>representing_org_id</c> kept for clients
/// deployed before the D-273a rename.</summary>
public sealed record PendingEventResponse(
    Guid EventId,
    Guid RepresentingOrgId,
    Guid OrgId,
    string OrgName,
    string Title,
    string Slug,
    DateTime StartsAt,
    DateTime CreatedAt,
    string Status,
    string Product,
    string? ArchetypeSlug,
    bool IsPaid,
    string? AuthorizationStatus,
    string? City,
    Guid? ReviewClaimedBy,
    string? ReviewClaimedByName,
    DateTime? ReviewClaimedAt);

/// <summary>Queue tab counts (D-266 M4). <c>legacy_in_review</c> renames
/// <see cref="EventReviewCounts.Legacy"/> — the pre-M4 bucket, now always 0. The field stays because
/// removing one is a client break.</summary>
public sealed record EventReviewCountsResponse(
    int PendingReview,
    int UnderReview,
    int ChangesRequested,
    int Approved,
    int Rejected,
    int LegacyInReview);

/// <summary>Outcome of a bulk moderation action. Every action loops the SAME single-event method the
/// individual routes call, so a partial failure is reported per event rather than failing the batch.</summary>
public sealed record BulkEventActionResult(
    IReadOnlyList<Guid> Succeeded,
    IReadOnlyList<BulkActionFailureItem> Failed);

/// <summary>One event a bulk action could not be applied to, with the service's error code.</summary>
public sealed record BulkActionFailureItem(Guid EventId, string Error);

/// <summary><c>GET /v1/tags</c> — the tag picker's options.</summary>
public sealed record TagResponse(Guid Id, string Name, string Slug);

/// <summary><c>GET /v1/field-presets</c> — category/type field metadata driving the Create-Event form.
/// <c>payload</c> is returned exactly as stored; the server never interprets it.</summary>
public sealed record FieldPresetResponse(Guid Id, string Slug, string Name, string Payload);

/// <summary>An organization the caller follows (<c>GET /v1/me/following</c>).</summary>
public sealed record FollowedOrg(Guid OrgId, string Name, string Slug, string? LogoKey);
