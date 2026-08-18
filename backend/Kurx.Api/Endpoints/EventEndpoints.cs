using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

/// <summary>D-267: the organization the caller is representing is a FIELD here, not a path segment.
/// <c>RepresentingOrgId</c> null means "Personal" — the caller hosts under their own name. This is the
/// only reason event creation no longer needs an organization chosen before the form opens.</summary>
public record CreateEventBody(string Title, string? Subtitle, string? Description,
    Guid CategoryId, Guid? TypeId, Guid? AudienceLevelId, Guid? TemplateId, Guid? ParentEventId,
    Guid? RepresentingOrgId,
    IReadOnlyList<string>? Tags,
    Guid? VenueId, string? VenueName, string? VenueAddress, string? City, double? Lat, double? Lng,
    DateTime StartsAt, DateTime EndsAt, string? Timezone,
    int? Capacity, string? Visibility, string? Language,
    string? ContactEmail, string? ContactPhone, string? Website, string? SocialLinksJson,
    string? EventMode = null, string? OnlineUrl = null,
    // D-265 — the create-event wizard's field groups. Optional and trailing, so a client that
    // predates them binds exactly as before.
    EventContentInput? Content = null, EventLegalInput? Legal = null,
    EventScheduleInput? Schedule = null, EventLocationInput? Location = null,
    EventEligibilityInput? Eligibility = null, EventCommerceInput? Commerce = null);

public record UpdateEventBody(string? Title, string? Subtitle, string? Description,
    Guid? CategoryId, Guid? TypeId, Guid? AudienceLevelId, Guid? TemplateId,
    IReadOnlyList<string>? Tags,
    Guid? VenueId, string? VenueName, string? VenueAddress, string? City, double? Lat, double? Lng,
    DateTime? StartsAt, DateTime? EndsAt, string? Timezone,
    int? Capacity, string? Visibility, string? Language,
    string? ContactEmail, string? ContactPhone, string? Website, string? SocialLinksJson,
    string? BannerKey, bool? IsFeatured,
    string? EventMode = null, string? OnlineUrl = null,
    EventContentInput? Content = null, EventLegalInput? Legal = null,
    EventScheduleInput? Schedule = null, EventLocationInput? Location = null,
    EventEligibilityInput? Eligibility = null, EventCommerceInput? Commerce = null);

/// <summary>D-266 M4: <c>ReasonCode</c> is an <see cref="Kurx.Domain.Enums.EventReviewReason"/> name and
/// <c>Notes</c> is the organiser-facing explanation. Optional and trailing, so a client that predates the
/// review lifecycle binds exactly as before; the service rejects the actions that require them.</summary>
public record TransitionEventBody(string Action, string? ReasonCode = null, string? Notes = null);
public record CloneEventBody(string? Title);

/// <summary>D-388 — a host's proposed edit to a LIVE event. The field set is <see cref="UpdateEventBody"/>'s
/// exactly, because the values a host proposes are the values that will be applied: a separate shape would
/// be a second definition of "an event edit" and would drift from the apply path the first time a field was
/// added to one and not the other. <c>Reason</c> is the host's note to the reviewer.</summary>
public record EventChangeRequestBody(string? Title, string? Subtitle, string? Description,
    Guid? CategoryId, Guid? TypeId, Guid? AudienceLevelId, Guid? TemplateId,
    Guid? VenueId, string? VenueName, string? VenueAddress, string? City, double? Lat, double? Lng,
    DateTime? StartsAt, DateTime? EndsAt, string? Timezone,
    int? Capacity, string? Visibility,
    string? EventMode = null, string? OnlineUrl = null,
    EventLegalInput? Legal = null, EventLocationInput? Location = null,
    EventEligibilityInput? Eligibility = null, EventCommerceInput? Commerce = null,
    string? Reason = null);

/// <summary>D-388 — a reviewer's verdict on a change request. <c>ReasonCode</c> is an
/// <see cref="Kurx.Domain.Enums.EventReviewReason"/> name and is required on a rejection; the same closed
/// vocabulary the event review itself uses.</summary>
public record ChangeRequestDecisionBody(bool Approve, string? ReasonCode = null, string? Notes = null);

/// <summary>D-266 M5 — document fields are storage <b>keys</b> handed back from
/// <c>POST …/authorization/presign</c>, never file bytes and never URLs.</summary>
/// <summary>D-266 M8 — the wizard's in-progress form. <c>PayloadJson</c> is stored verbatim and never
/// parsed by the server: it is unvalidated client state, including steps that would fail validation.</summary>
public record EventDraftBody(string PayloadJson, string? StepKey);

public record EventAuthorizationBody(
    string HeadName, string HeadDesignation, string OfficialEmail, string OfficialPhone,
    string? LetterheadDocumentKey, string? SignatureDocumentKey,
    IReadOnlyList<string>? SupportingDocumentKeys,
    // D-266 M5. Role comes from `RepresentativeRoles.All`; RoleOther carries the words when it is
    // `Other`. RepresentativeUserId links a Kurx account and grants nothing (D-269 owns authority).
    string RepresentativeRole = "", string? RepresentativeRoleOther = null,
    Guid? RepresentativeUserId = null);

public static class EventEndpoints
{
    public static void MapEventEndpoints(this WebApplication app)
    {
        var events = app.MapGroup("/v1/orgs/{orgId:guid}/events").WithTags("events").RequireAuthorization();

        // Event creation lives at POST /v1/events (D-267) — an organization is never required to reach it.
        // This group keeps the per-org LIST (the admin console's view of one organization's events) and the
        // management sub-resources, whose orgId the client derives from the event it already opened.
        events.MapGet("/", async (Guid orgId, int? page, int? pageSize, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.ListForOrgAsync(UserId(principal), orgId, IsAdmin(principal), page ?? 1, pageSize ?? 20, ct);
            return result.Ok
                ? Results.Ok(new OrgEventPage(result.Value.Items, result.Value.Total))
                : Fail(result.Error);
        }).Produces<OrgEventPage>();

        events.MapGet("/{eventId:guid}", async (Guid eventId, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAsync(eventId, UserId(principal), IsAdmin(principal), ct: ct);
            return result.Ok ? Results.Ok(ToEventJson(result.Value!)) : Fail(result.Error);
        }).Produces<EventDetailResponse>();

        events.MapPatch("/{eventId:guid}", async (Guid eventId, UpdateEventBody body, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var input = new UpdateEventInput(body.Title, body.Subtitle, body.Description, body.CategoryId, body.TypeId,
                body.AudienceLevelId, body.TemplateId, body.Tags, body.VenueId, body.VenueName, body.VenueAddress,
                body.City, body.Lat, body.Lng, body.StartsAt, body.EndsAt, body.Timezone, body.Capacity, body.Visibility,
                body.Language, body.ContactEmail, body.ContactPhone, body.Website, body.SocialLinksJson, body.BannerKey, body.IsFeatured,
                body.EventMode, body.OnlineUrl,
                // ListedStandalone is not on UpdateEventBody — it is set through the series endpoints,
                // so it stays null here and the D-265 groups follow it positionally.
                null,
                body.Content, body.Legal, body.Schedule, body.Location, body.Eligibility, body.Commerce);
            var result = await svc.UpdateAsync(UserId(principal), eventId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(ToEventJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<UpdateEventBody>().Produces<EventDetailResponse>();

        events.MapPost("/{eventId:guid}/transition", async (Guid eventId, TransitionEventBody body, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.TransitionAsync(UserId(principal), eventId, IsAdmin(principal), IsReviewer(principal), body.Action, body.ReasonCode, body.Notes, ct);
            return result.Ok ? Results.Ok(ToEventJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<TransitionEventBody>().Produces<EventDetailResponse>();

        /*
         * D-388 — change requests on a LIVE event.
         *
         * Org-scoped, on the same group as the event's own routes, for the reason REVIEW_LIFECYCLE.md
         * already states about reviewer actions: there is no admin-side workflow route. A duplicate admin
         * endpoint doing the same thing is how two workflows drift apart. A reviewer drives the decision
         * leg here on any org's event, exactly as they drive `transition`.
         */
        events.MapPost("/{eventId:guid}/change-requests", async (Guid eventId, EventChangeRequestBody body,
            ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateChangeRequestAsync(UserId(principal), eventId, ToUpdateInput(body), body.Reason, ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).WithValidation<EventChangeRequestBody>().Produces<EventChangeRequestView>();

        events.MapGet("/{eventId:guid}/change-requests", async (Guid eventId, ClaimsPrincipal principal,
            IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.ListChangeRequestsAsync(UserId(principal), eventId, IsAdmin(principal), IsReviewer(principal), ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).Produces<IReadOnlyList<EventChangeRequestView>>();

        events.MapDelete("/{eventId:guid}/change-requests/{changeRequestId:guid}", async (Guid eventId,
            Guid changeRequestId, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.WithdrawChangeRequestAsync(UserId(principal), eventId, changeRequestId, ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).Produces<EventChangeRequestView>();

        events.MapPost("/{eventId:guid}/change-requests/{changeRequestId:guid}/decision", async (Guid eventId,
            Guid changeRequestId, ChangeRequestDecisionBody body, ClaimsPrincipal principal, IEventService svc,
            CancellationToken ct) =>
        {
            var result = await svc.DecideChangeRequestAsync(UserId(principal), eventId, changeRequestId,
                body.Approve, body.ReasonCode, body.Notes, IsAdmin(principal), IsReviewer(principal), ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).WithValidation<ChangeRequestDecisionBody>().Produces<EventChangeRequestView>();

        // D-101 (M7): duplicate an event as a fresh Draft — content is copied (ticket types with Sold reset,
        // form fields, media, speakers, sponsors, sessions, tags), runtime state never is.
        // D-340: the event row itself now copies by default with a named reset list, rather than by an
        // allowlist that named 47 of 110 columns and silently defaulted the rest.
        events.MapPost("/{eventId:guid}/clone", async (Guid eventId, CloneEventBody? body,
            ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.CloneAsync(UserId(principal), eventId, IsAdmin(principal), body?.Title, ct);
            return result.Ok ? Results.Ok(ToEventJson(result.Value!)) : Fail(result.Error);
        }).Produces<EventDetailResponse>();

        // Live payment-readiness (M8): is this event paid, and would payments be enabled right now?
        events.MapGet("/{eventId:guid}/payment-readiness", async (Guid eventId, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.GetPaymentReadinessAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            return result.Ok
                ? Results.Ok(result.Value!)
                : Fail(result.Error);
        }).Produces<PaymentReadiness>();

        // Aggregate analytics summary for one event (M8/D-053) — Owner/Manager, counts + gross only.
        events.MapGet("/{eventId:guid}/analytics", async (Guid eventId, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAnalyticsAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<EventAnalytics>();

        // Attendee roster + CSV export (D-054) — Owner/Manager/Staff. Returns attendee PII.
        events.MapGet("/{eventId:guid}/attendees", async (Guid eventId, string? q, Guid? ticketTypeId, Guid? groupId,
            string? state, int? page, int? pageSize, ClaimsPrincipal principal, IAttendeeService svc, CancellationToken ct) =>
        {
            var filter = new AttendeeListFilter(q, ticketTypeId, groupId, state, page ?? 1, pageSize ?? 50);
            var result = await svc.ListAsync(UserId(principal), eventId, filter, ct);
            return result.Ok ? Results.Ok(new AttendeePage(result.Value.Items, result.Value.Total)) : Fail(result.Error);
        }).Produces<AttendeePage>();

        events.MapGet("/{eventId:guid}/attendees/export", async (Guid eventId, ClaimsPrincipal principal, IAttendeeService svc, CancellationToken ct) =>
        {
            // DB-6: the roster streams. Authorization already ran inside ExportCsvAsync and returned
            // before any byte was written, so `Fail` still produces a real 404/403 body — Results.Stream
            // is only reached once the answer is "yes".
            var result = await svc.ExportCsvAsync(UserId(principal), eventId, ct);
            return result.Ok
                ? Results.Stream(stream => result.Value!(stream, ct), "text/csv", $"attendees-{eventId}.csv")
                : Fail(result.Error);
        }).Produces(StatusCodes.Status200OK, typeof(byte[]), "text/csv");


        events.MapDelete("/{eventId:guid}", async (Guid eventId, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteDraftAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        // V3 Capability set for one event (Phase 2, V3 §11) — reuses the event's view authorization
        // (svc.GetAsync 404s for a non-member on a non-Published event, never 403).
        events.MapGet("/{eventId:guid}/capabilities", async (Guid eventId, ClaimsPrincipal principal,
            IEventService svc, ICapabilityService caps, CancellationToken ct) =>
        {
            var ev = await svc.GetAsync(eventId, UserId(principal), IsAdmin(principal), ct: ct);
            if (!ev.Ok) return Fail(ev.Error);
            var list = await caps.GetForEventAsync(eventId, ct);
            return Results.Ok(list.Select(CapabilityEndpoints.ToJson));
        }).Produces<IReadOnlyList<ResolvedCapabilityResponse>>();

        // D-266 M3: the single source of truth for what an event is ALLOWED to do. Authorization is the
        // event read above — this endpoint, like /capabilities, performs none of its own.
        events.MapGet("/{eventId:guid}/policy-requirements", async (Guid eventId, ClaimsPrincipal principal,
            IEventService svc, IEventPolicyService policy, CancellationToken ct) =>
        {
            var ev = await svc.GetAsync(eventId, UserId(principal), IsAdmin(principal), ct: ct);
            if (!ev.Ok) return Fail(ev.Error);
            var res = await policy.GetForEventAsync(eventId, ct);
            return res.Ok ? Results.Ok(res.Value) : Fail(res.Error);
        }).Produces<PolicyRequirementsView>();

        // V3 §20 (Phase 15): the generated organiser workspace — capabilities grouped by workspace_tab + the live
        // publish checklist (a projection of the §14.2 gates). Owner/Manager/Representative; non-member → 404.
        //
        // ⚠️ NO CLIENT CALLS THIS YET (D-310). Both web and Flutter hardcode their sixteen management tabs,
        // and eighteen of the thirty capabilities name a tab no screen renders. It is kept — the contributor
        // model is what stopped subsystems having to masquerade as event capabilities to show a page — but
        // it describes a *generated* workspace that is not yet the shipped one. Do not read a passing
        // response here as evidence that a tab exists on any client.
        events.MapGet("/{eventId:guid}/workspace", async (Guid eventId, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.GetWorkspaceAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            if (!result.Ok) return Fail(result.Error);
            var w = result.Value!;
            return Results.Ok(w);
        }).Produces<EventWorkspaceView>();

        // ── User-first event surface (D-267) ──────────────────────────────────────────────────────
        // A person's events belong to the person, not to an organization they had to open first. These
        // three routes are what Workspace and Create Event actually call; the /v1/orgs/{orgId}/events/*
        // group above stays for the admin console's per-org view and for the management sub-resources,
        // whose orgId is now derived from the event rather than from a user-facing org selection.
        var myEvents = app.MapGroup("/v1/me/events").WithTags("events").RequireAuthorization();

        myEvents.MapGet("/", async (int? page, int? pageSize, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var (items, total) = await svc.ListMineAsync(UserId(principal), page ?? 1, pageSize ?? 20, ct);
            return Results.Ok(new MyEventPage(items, total));
        }).Produces<MyEventPage>();

        var publicEvents = app.MapGroup("/v1/events").WithTags("public-events");

        // Create Event needs no organization in the path — representation is a field inside the request,
        // chosen during creation (D-267). Omitting `representingOrgId` means **Personal**: the user
        // represents themselves. The event's owner is the caller, always (D-268).
        publicEvents.MapPost("/", async (CreateEventBody body, ClaimsPrincipal principal,
            IEventService svc, CancellationToken ct) =>
        {
            var userId = UserId(principal);
            var input = new CreateEventInput(body.Title, body.Subtitle, body.Description, body.CategoryId, body.TypeId,
                body.AudienceLevelId, body.TemplateId, body.ParentEventId, body.Tags, body.VenueId, body.VenueName,
                body.VenueAddress, body.City, body.Lat, body.Lng, body.StartsAt, body.EndsAt, body.Timezone,
                body.Capacity, body.Visibility, body.Language, body.ContactEmail, body.ContactPhone, body.Website, body.SocialLinksJson,
                body.EventMode, body.OnlineUrl,
                body.Content, body.Legal, body.Schedule, body.Location, body.Eligibility, body.Commerce);
            var result = await svc.CreateAsync(userId, body.RepresentingOrgId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(ToEventJson(result.Value!)) : Fail(result.Error);
        }).RequireAuthorization().WithValidation<CreateEventBody>().Produces<EventDetailResponse>();

        // Addressing an event by its own id — the guid constraint keeps this ahead of the public
        // {slug} route below. Authorization is CanViewAsync against the event's own org, so a manager
        // reaches their event without the client having to know (or select) which org it belongs to.
        publicEvents.MapGet("/{eventId:guid}", async (Guid eventId, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAsync(eventId, UserId(principal), IsAdmin(principal), ct: ct);
            return result.Ok ? Results.Ok(ToEventJson(result.Value!)) : Fail(result.Error);
        }).RequireAuthorization().Produces<EventDetailResponse>();

        // ── D-266 M5 · institutional authorization ────────────────────────────────────────────────
        // Evidence that the organization this event REPRESENTS consented to being represented by it.
        // Distinct from IEventAuthority (D-269), which decides who may act — see IEventAuthorizationService.
        //
        // On the user-first /v1/events/{id} surface rather than the org-scoped group: an authorization
        // belongs to the event, and after D-267 a client addressing an event does not first select an
        // organization. All three resolve the caller's standing through IEventAuthority, so a stranger
        // gets 404 (D-018) and a non-manager gets 403.
        // The closed role vocabulary, PUBLISHED rather than duplicated. The server already validates
        // against `RepresentativeRoles.All`, so this only exposes the list that is already the source of
        // truth — three clients hardcoding their own copy is how a role ends up offered on one surface and
        // rejected by the API. Unscoped by event: the vocabulary is a property of the platform, not of one
        // event. No `{eventId:guid}` ambiguity — the literal segment cannot match the guid constraint.
        publicEvents.MapGet("/authorization/roles", () => Results.Ok(RepresentativeRoles.All))
            .WithTags("events").RequireAuthorization().Produces<IReadOnlyList<string>>();

        publicEvents.MapPost("/{eventId:guid}/authorization", async (Guid eventId, EventAuthorizationBody body,
            ClaimsPrincipal principal, IEventAuthorizationService svc, CancellationToken ct) =>
        {
            var result = await svc.SubmitAsync(UserId(principal), eventId, IsAdmin(principal),
                new EventAuthorizationInput(body.HeadName, body.HeadDesignation, body.OfficialEmail,
                    body.OfficialPhone, body.LetterheadDocumentKey, body.SignatureDocumentKey,
                    body.SupportingDocumentKeys, body.RepresentativeRole, body.RepresentativeRoleOther,
                    body.RepresentativeUserId), ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).RequireAuthorization().WithValidation<EventAuthorizationBody>().Produces<EventAuthorizationView>();

        publicEvents.MapGet("/{eventId:guid}/authorization", async (Guid eventId, ClaimsPrincipal principal,
            IEventAuthorizationService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            if (!result.Ok) return Fail(result.Error);
            // 204, not 404: the event exists and the caller may see it — there is simply no authorization
            // on file yet. A 404 here would be indistinguishable from "no such event".
            return result.Value is null ? Results.NoContent() : Results.Ok(result.Value);
            // BOTH outcomes are declared (D-313). Declaring only the 200 describes half of a
            // conditional endpoint, and it is the more misleading half: a generated client reads
            // "200 + schema" as "a body always arrives" and dereferences nothing on the empty case.
        }).RequireAuthorization().Produces<EventAuthorizationView>()
          .Produces(StatusCodes.Status204NoContent);

        publicEvents.MapPost("/{eventId:guid}/authorization/presign", async (Guid eventId, PresignMediaBody body,
            ClaimsPrincipal principal, IEventAuthorizationService svc, CancellationToken ct) =>
        {
            var maxBytes = Math.Clamp(body.MaxBytes, 1, 10_000_000);
            var result = await svc.PresignDocumentAsync(UserId(principal), eventId, IsAdmin(principal),
                body.ContentType, maxBytes, ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).RequireAuthorization().WithValidation<PresignMediaBody>().Produces<PresignedUpload>();

        // ── D-266 M8 · wizard autosave ────────────────────────────────────────────────────────────
        // Opaque client state, per (event, caller). Never parsed server-side and never read by the publish
        // path — it is what the organiser has typed, not what the event is.
        publicEvents.MapPut("/{eventId:guid}/draft", async (Guid eventId, EventDraftBody body,
            ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.SaveDraftAsync(UserId(principal), eventId, IsAdmin(principal),
                body.PayloadJson, body.StepKey, ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).RequireAuthorization().WithValidation<EventDraftBody>().Produces<EventDraftView>();

        publicEvents.MapGet("/{eventId:guid}/draft", async (Guid eventId, ClaimsPrincipal principal,
            IEventService svc, CancellationToken ct) =>
        {
            var result = await svc.GetDraftAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            if (!result.Ok) return Fail(result.Error);
            // 204 when nothing has been autosaved: a state, not an error — and one the contract has to
            // state, because the wizard's very first load hits exactly this path (D-313).
            return result.Value is null ? Results.NoContent() : Results.Ok(result.Value);
        }).RequireAuthorization().Produces<EventDraftView>()
          .Produces(StatusCodes.Status204NoContent);

        publicEvents.MapGet("/", async (string? q, Guid? categoryId, Guid? orgId, string? city,
            DateTime? dateFrom, DateTime? dateTo, string? sort, int? page, int? pageSize, string? price, string? mode,
            // V3 §15 (Phase 16) — additive optional discovery filters; omitting them keeps the prior behaviour.
            string? kind, string? language, double? lat, double? lng, double? radiusKm,
            IEventService svc, CancellationToken ct) =>
        {
            var filter = new EventListFilter(q, categoryId, orgId, city, null, null, dateFrom, dateTo, sort, page ?? 1, pageSize ?? 20,
                price, mode, kind, language, lat, lng, radiusKm);
            var (items, total) = await svc.SearchAsync(filter, ct);
            return Results.Ok(new EventSummaryPage(items.Select(ToSummaryJson), total));
        }).Produces<EventSummaryPage>();

        // V3 §15 (Phase 16): the eligibility-aware "events you can attend" feed — Published + Public events whose
        // audience rule this user satisfies, ranked. Authenticated; internal events are never indexed, so this never
        // leaks their existence (404-not-403 preserved). New capability, so a new route (not a replaced one).
        publicEvents.MapGet("/for-you", async (int? limit, ClaimsPrincipal principal, ISearchService search, CancellationToken ct)
            => Results.Ok((await search.EligibleForAsync(UserId(principal), limit ?? 10, ct)).Select(ToSummaryJson)))
            .RequireAuthorization().Produces<IReadOnlyList<EventSummaryResponse>>();

        publicEvents.MapGet("/upcoming", async (int? limit, IEventService svc, CancellationToken ct)
            => Results.Ok((await svc.UpcomingAsync(limit ?? 10, ct)).Select(ToSummaryJson))).Produces<IReadOnlyList<EventSummaryResponse>>();

        publicEvents.MapGet("/trending", async (int? limit, IEventService svc, CancellationToken ct)
            => Results.Ok((await svc.TrendingAsync(limit ?? 10, ct)).Select(ToSummaryJson))).Produces<IReadOnlyList<EventSummaryResponse>>();

        publicEvents.MapGet("/featured", async (int? limit, IEventService svc, CancellationToken ct)
            => Results.Ok((await svc.FeaturedAsync(limit ?? 10, ct)).Select(ToSummaryJson))).Produces<IReadOnlyList<EventSummaryResponse>>();

        publicEvents.MapGet("/latest", async (int? limit, IEventService svc, CancellationToken ct)
            => Results.Ok((await svc.LatestAsync(limit ?? 10, ct)).Select(ToSummaryJson))).Produces<IReadOnlyList<EventSummaryResponse>>();

        publicEvents.MapGet("/{slug}", async (string slug, HttpContext http, ClaimsPrincipal principal, IEventService svc, CancellationToken ct) =>
        {
            var viewerId = OptionalUserId(principal);
            var result = await svc.GetBySlugAsync(slug, viewerId, IsAdmin(principal),
                viewVisitorKey: VisitorKey(http, viewerId), ct: ct);
            return result.Ok ? Results.Ok(ToEventJson(result.Value!)) : Fail(result.Error);
        }).Produces<EventDetailResponse>();

        publicEvents.MapGet("/{slug}/related", async (string slug, int? limit, IEventService svc, CancellationToken ct) =>
        {
            var ev = await svc.GetBySlugAsync(slug, null, false, ct: ct);
            if (!ev.Ok) return Fail(ev.Error);
            return Results.Ok((await svc.RelatedAsync(ev.Value!.Id, limit ?? 6, ct)).Select(ToSummaryJson));
        }).Produces<IReadOnlyList<EventSummaryResponse>>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static Guid? OptionalUserId(ClaimsPrincipal principal)
        => principal.Identity?.IsAuthenticated == true
            && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    /// <summary>Pseudonymous per-day visitor identifier for the event-view stream (D-130).
    ///
    /// <para>Salted with the UTC date so the same visitor hashes differently tomorrow: the value is
    /// usable for "unique visitors today" — the only window <c>event_analytics_daily</c> reports — and
    /// useless for tracking anyone across days. The raw IP never leaves this method.</para></summary>
    private static string VisitorKey(HttpContext http, Guid? userId)
    {
        // A signed-in viewer is identified by their id, so the same person on phone and laptop counts once.
        var identity = userId?.ToString()
            ?? $"{http.Connection.RemoteIpAddress}|{http.Request.Headers.UserAgent}";
        var salted = $"{DateTime.UtcNow:yyyy-MM-dd}|{identity}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(salted)));
    }

    // A platform reviewer (VerificationReviewer, or SuperAdmin via the live kurx_admin claim) may
    // approve/reject paid events under review (M8). Claims are populated live per request (M2, D-040).
    private static bool IsReviewer(ClaimsPrincipal principal)
        => principal.HasClaim("kurx_admin", "true") || principal.HasClaim("platform_role", "VerificationReviewer");

    /// <summary>D-388 — the proposal body onto the shared update input. Positional, following
    /// <c>UpdateEventBody</c>'s own mapping directly above; the fields absent from
    /// <see cref="EventChangeRequestBody"/> (tags, language, contact, website, socials, banner, featured,
    /// listed-standalone, the content and schedule groups) are the OPERATIONAL ones a host edits directly
    /// on a live event, so a proposal never carries them.</summary>
    private static UpdateEventInput ToUpdateInput(EventChangeRequestBody b) => new(
        b.Title, b.Subtitle, b.Description, b.CategoryId, b.TypeId, b.AudienceLevelId, b.TemplateId,
        null, b.VenueId, b.VenueName, b.VenueAddress, b.City, b.Lat, b.Lng,
        b.StartsAt, b.EndsAt, b.Timezone, b.Capacity, b.Visibility,
        null, null, null, null, null, null, null,
        b.EventMode, b.OnlineUrl, null,
        null, b.Legal, null, b.Location, b.Eligibility, b.Commerce);

    private static IResult Fail(string? error) => error switch
    {
        // "reviewer_required" is an authorization outcome, not a malformed request: the caller may manage
        // the event but a reviewer decision is not theirs to make (D-266 M4). It belongs with the other
        // 403s — falling through to the 400 default made "not allowed" indistinguishable from "bad input".
        "forbidden" or "organizer_not_verified_for_paid" or "org_not_verified" or "pending_org_verification"
            or "representation_vacant" or "reviewer_required" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        // "event_under_review" is a 409 for the same reason as the others here: the request is well-formed,
        // the caller is entitled, and the state simply forbids it right now (D-266 M4 edit lock).
        // D-367 `type_conflicts_with_team_ticket` joins them: the payload is valid and the caller is
        // entitled — the event's own team registration is what forbids this Type, and the organiser
        // resolves it by changing the registration, not the request.
        "event_archived" or "not_draft" or "invalid_transition" or "paid_event_requires_review"
            or "event_already_started" or "event_under_review"
            or "type_conflicts_with_team_ticket" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        // D-266 M5 publish blockers. 409, not 400: the request is well-formed and the caller is entitled —
        // the event's state simply forbids publishing right now, which is the same shape as the review
        // lock above. A 400 would tell a client to fix its payload, and there is nothing in the payload
        // to fix.
        "event_authorization_required" or "representation_required"
            => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        // D-388. `cannot_review_own_request` and `requester_no_longer_authorized` are authorization
        // outcomes; the rest are well-formed requests the state forbids, which is the same 409 shape as
        // the edit lock above. `change_request_required` in particular must NOT be a 400: the payload is
        // perfectly valid, and telling a client to fix it would send them looking for a fault that is not
        // there. The route they want is POST .../change-requests.
        "cannot_review_own_request" or "requester_no_longer_authorized"
            => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "change_request_required" or "not_live_protected" or "change_request_decided"
            or "version_conflict" or "no_changes"
            => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    /// <remarks>Serves the organiser reads AND the public <c>GET /v1/events/{slug}</c>. Anything added
    /// here is public — which is why <c>MeetingPassword</c> is absent from
    /// <see cref="EventLocationDetailView"/> and never reaches this method (D-265).</remarks>
    private static EventDetailResponse ToEventJson(EventDetail e) => new(
        // D-265 groups. Nested rather than flattened so the response mirrors the request shape.
        e.Content,
        e.Legal,
        e.Schedule,
        e.LocationDetail,
        e.Eligibility,
        // D-356 — narrowed here rather than on `EventDetail`, because this method IS the public boundary
        // (see the remarks above) and the service layer still needs the full figures. `From` is the only
        // way the internal record reaches the wire, so a field added to it cannot leak by default.
        PublicEventCommerceView.From(e.Commerce),
        // D-302 — the represented organization, named. Null for a self-represented event: an `IsPersonal`
        // row is persistence, not an institution (D-268), so clients show the creator alone.
        e.Representing,
        e.BannerUrl,
        e.Id,
        e.RepresentingOrgId,
        // D-273a expand-and-contract: `org_id` is DEPRECATED and kept only for clients deployed before
        // the rename. It always equals representing_org_id and never meant ownership — the owner is the
        // user in created_by (D-268). Drop it in the contract phase once no client reads it.
        e.RepresentingOrgId,
        e.ParentEventId,
        e.Title,
        e.Slug,
        e.ShortCode,
        e.Subtitle,
        e.Description,
        e.CategoryId,
        e.TypeId,
        e.AudienceLevelId,
        e.TemplateId,
        e.Tags,
        // EventVenueView is a proven 1:1 snake_case projection, so it passes through unchanged.
        e.Venue,
        e.StartsAt,
        e.EndsAt,
        e.Timezone,
        e.Capacity,
        e.Visibility.ToLowerInvariant(),
        e.Status.ToLowerInvariant(),
        e.EventMode.ToLowerInvariant(),
        e.OnlineUrl,
        e.SettlementCurrency,
        e.Language,
        e.ContactEmail,
        e.ContactPhone,
        e.Website,
        e.SocialLinksJson,
        e.BannerKey,
        e.IsFeatured,
        e.ViewCount,
        // NOT EventMediaView passed through: that record carries a Url this projection never emitted.
        e.Media.Select(m => new EventMediaResponse(m.Id, m.Kind.ToLowerInvariant(), m.Key, m.Caption, m.Sort)).ToList(),
        e.CreatedAt,
        e.PublishedAt,
        e.UpdatedAt);

    internal static EventSummaryResponse ToSummaryJson(EventSummary e) => new(
        e.Id,
        e.RepresentingOrgId,
        // DEPRECATED (D-273a) — always equals representing_org_id, never meant ownership.
        e.RepresentingOrgId,
        e.ParentEventId,
        e.Title,
        e.Slug,
        e.ShortCode,
        e.Subtitle,
        e.BannerKey,
        e.StartsAt,
        e.EndsAt,
        e.Status.ToLowerInvariant(),
        e.Visibility.ToLowerInvariant(),
        e.VenueName,
        e.City,
        // Presigned; the raw key beside it is not fetchable (D-302).
        e.BannerUrl,
        e.EventMode.ToLowerInvariant(),
        e.CategoryName,
        e.PriceFromPaise,
        e.Currency,
        e.IsFeatured,
        // D-376 — emitted with its original capitalisation ("PerGroup"), like every other enum-ish
        // string on the wire: SnakeCaseResponseConverter renames keys, never values.
        e.PriceFromUnit);


}
