using System.Globalization;
using System.Security.Claims;
using System.Text;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record ModerationActionBody(string? Reason);
public record OrganizerMessageBody(string Message);
public record EmergencyEditBody(string Reason, UpdateEventInput Input);

// D-191: eventIds is capped and action is one of a closed set (validated below) — every action loops the
// SAME single-event methods the individual routes above already call; this is never a second moderation
// implementation.
public record BulkEventActionBody(IReadOnlyList<Guid> EventIds, string Action, string? Reason);
public record BulkActionFailure(Guid EventId, string Error);

/// <summary>D-266 M5. <c>Decision</c> is <c>approve</c> | <c>reject</c> | <c>request_changes</c> — the same
/// vocabulary <c>ReviewOrgBody</c> uses, so a reviewer learns one set of words for the platform.</summary>
public record ReviewEventAuthorizationBody(string Decision, string? ReasonCode, string? Notes);

/// <summary>D-266 M7. <c>ItemKey</c> is a requirement code from the event's live reviewer checklist — the
/// service refuses any key not currently on it.</summary>
public record ReviewChecklistItemBody(string ItemKey, bool Checked);

/// <summary>D-266 M7 — FinanceOps' verdict on a fundraising event's money path. <c>Notes</c> is required
/// on a failure: a refusal the organiser cannot act on is a dead end, not a review outcome.</summary>
public record FinancialReviewBody(bool Passed, string? Notes);

/// <summary>Admin event-approval queue (M8, D-057) + global event management (D-061) + moderation overrides
/// and organizer messaging (D-186). Lists/searches events across all orgs; the content-lifecycle actions
/// (approve/reject/publish/archive/cancel) reuse the org-scoped transition endpoint, which already honours
/// the VerificationReviewer role (a reviewer may transition any org's event) — no duplicate endpoint here.
/// This file only adds what doesn't already exist: moderation overrides (suspend/hide) and organizer
/// messaging, neither of which fits the content-lifecycle transition table. Reviewer-gated.</summary>
public static class AdminEventEndpoints
{
    public static void MapAdminEventEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/admin/events").WithTags("admin").RequireAuthorization("VerificationReviewer");

        g.MapGet("/pending", async (int? limit, IEventService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListInReviewAsync(limit ?? 50, ct)).Select(e => new PendingEventResponse(
                e.EventId, e.RepresentingOrgId,
                e.RepresentingOrgId,                   // DEPRECATED (D-273a)
                e.OrgName, e.Title, e.Slug, e.StartsAt, e.CreatedAt,
                // D-266 M7 — enough to triage the queue without a call per row. No meeting_password: the
                // view has no such field, so it cannot leak here or anywhere else this record is projected.
                e.Status, e.Product, e.ArchetypeSlug, e.IsPaid, e.AuthorizationStatus, e.City,
                // D-266 M4 — who holds this item; null when unclaimed.
                e.ReviewClaimedBy, e.ReviewClaimedByName, e.ReviewClaimedAt)))).Produces<IReadOnlyList<PendingEventResponse>>();

        // D-266 M4: queue tab counts. Derived from event status in one pass, so the tabs and the list they
        // head can never disagree.
        g.MapGet("/review-counts", async (IEventService svc, CancellationToken ct) =>
        {
            var c = await svc.GetReviewCountsAsync(ct);
            return Results.Ok(new EventReviewCountsResponse(
                c.PendingReview, c.UnderReview, c.ChangesRequested, c.Approved, c.Rejected,
                // legacy_in_review renames Legacy — the retired pre-M4 bucket, kept on the contract.
                c.Legacy));
        }).Produces<EventReviewCountsResponse>();

        /*
         * D-388 — the change-request queue.
         *
         * A READ only. The verdict goes through the org-scoped
         * `POST /v1/orgs/{orgId}/events/{eventId}/change-requests/{id}/decision`, for the same reason the
         * review verdicts do: there is no admin-side workflow route, because a duplicate admin action
         * endpoint is how two workflows drift apart (REVIEW_LIFECYCLE.md, "Admin API").
         */
        g.MapGet("/change-requests", async (int? limit, IEventService svc, CancellationToken ct) =>
        {
            var res = await svc.ListPendingChangeRequestsAsync(limit ?? 50, ct);
            return res.Ok ? Results.Ok(res.Value) : Results.Ok(Array.Empty<EventChangeRequestView>());
        }).Produces<IReadOnlyList<EventChangeRequestView>>();

        // D-266 M4: one event's decision history, from VerificationReview. Reviewer actions themselves go
        // through the org-scoped transition endpoint (reviewer-capable) — there is deliberately no
        // admin-side duplicate of the workflow, matching the pattern the force-actions already use.
        g.MapGet("/{eventId:guid}/review-history", async (Guid eventId, IEventService svc, CancellationToken ct) =>
        {
            var res = await svc.GetReviewHistoryAsync(eventId, ct);
            if (!res.Ok) return Results.NotFound();
            return Results.Ok(res.Value!);
        }).Produces<IReadOnlyList<EventReviewEntry>>();

        // D-266 M5: institutional authorization for one event. Reviewer-gated by this group's policy, which
        // IS the authorization — a platform reviewer holds no standing on the event and would 404 through
        // the organiser route, exactly like review-history above.
        g.MapGet("/{eventId:guid}/authorization", async (Guid eventId, IEventAuthorizationService svc, CancellationToken ct) =>
        {
            var view = await svc.GetForReviewAsync(eventId, ct);
            // 204 when there is nothing on file — declared alongside the 200 (D-313), because a
            // conditional endpoint that advertises only its body case tells a generated client the
            // body always arrives.
            return view is null ? Results.NoContent() : Results.Ok(view);
        }).Produces<EventAuthorizationView>().Produces(StatusCodes.Status204NoContent);

        // One review route carrying a decision, matching /v1/admin/orgs/{orgId}/verification/review. Four
        // verb-named routes would be four spellings of one state change, and a reviewer would have to learn
        // a different shape per surface.
        g.MapPost("/{eventId:guid}/authorization/review", async (Guid eventId, ReviewEventAuthorizationBody body,
            ClaimsPrincipal p, IEventAuthorizationService svc, CancellationToken ct) =>
        {
            var r = await svc.ReviewAsync(UserId(p), eventId, body.Decision, body.ReasonCode, body.Notes, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).WithValidation<ReviewEventAuthorizationBody>().Produces<EventAuthorizationView>();

        // D-266 M7: the reviewer's checklist. The ITEMS are a live projection of
        // PolicyResolver.ReviewerChecklist — the same array the publish blockers come from — so a reviewer
        // can never work a list that has drifted from the rules gating the publish. Only ticks are stored.
        g.MapGet("/{eventId:guid}/review-checklist", async (Guid eventId, ClaimsPrincipal p,
            IEventReviewChecklistService svc, CancellationToken ct) =>
        {
            var r = await svc.GetAsync(UserId(p), eventId, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<ReviewChecklistView>();

        g.MapPut("/{eventId:guid}/review-checklist", async (Guid eventId, ReviewChecklistItemBody body,
            ClaimsPrincipal p, IEventReviewChecklistService svc, CancellationToken ct) =>
        {
            var r = await svc.SetAsync(UserId(p), eventId, body.ItemKey, body.Checked, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).WithValidation<ReviewChecklistItemBody>().Produces<ReviewChecklistView>();

        // D-266 M7 — financial review (D12 §6, A11 Fundraising). **FinanceOps, not VerificationReviewer**:
        // clearing a money path is a different competence from reviewing an event's content, and the
        // platform already separates those roles. Mapped outside the reviewer-gated group above for that
        // reason alone.
        app.MapPost("/v1/admin/events/{eventId:guid}/financial-review",
            async (Guid eventId, FinancialReviewBody body, ClaimsPrincipal p, IEventService svc, CancellationToken ct) =>
            {
                var r = await svc.RecordFinancialReviewAsync(UserId(p), eventId, body.Passed, body.Notes, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).RequireAuthorization("FinanceOps").WithValidation<FinancialReviewBody>()
              .WithTags("admin").Produces<EventDetail>();

        // Global event management (D-061/D-186/D-187): the full admin filter set over every org's events. The
        // force publish/unpublish/close/archive/cancel actions reuse the org-scoped transition (reviewer-
        // capable, M8) — the admin frontend calls that route directly, no admin-side duplicate.
        g.MapGet("", async (
            string? q, string? status, Guid? categoryId, string? city, string? visibility, bool? isPaid,
            bool? verifiedOnly, DateTime? dateFrom, DateTime? dateTo, long? revenueMin, long? revenueMax,
            int? registrationsMin, int? registrationsMax, string? sort, int? limit, int? page,
            IEventService svc, CancellationToken ct) =>
        {
            var filter = new AdminEventListFilter(q, status, categoryId, city, visibility, isPaid, verifiedOnly,
                dateFrom, dateTo, revenueMin, revenueMax, registrationsMin, registrationsMax, sort, limit ?? 50, page ?? 1);
            var (items, total) = await svc.ListForAdminAsync(filter, ct);
            return Results.Ok(new AdminEventPage(items.Select(ToAdminJson), total));
            // Envelope ({items, total}), not a bare list — declared in Stage E with a named page type.
        }).Produces<AdminEventPage>();

        g.MapPost("/{eventId:guid}/feature", async (Guid eventId, IEventService svc, CancellationToken ct) =>
        {
            var r = await svc.SetFeaturedAsync(eventId, true, ct);
            return r.Ok ? Results.Ok(ToAdminJson(r.Value!)) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).Produces<AdminEventResponse>();

        g.MapPost("/{eventId:guid}/unfeature", async (Guid eventId, IEventService svc, CancellationToken ct) =>
        {
            var r = await svc.SetFeaturedAsync(eventId, false, ct);
            return r.Ok ? Results.Ok(ToAdminJson(r.Value!)) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).Produces<AdminEventResponse>();

        // D-186: moderation overrides, orthogonal to the content-lifecycle status (see EventService.SuspendAsync).
        g.MapPost("/{eventId:guid}/suspend", async (Guid eventId, ModerationActionBody body, IEventService svc, CancellationToken ct) =>
        {
            var r = await svc.SuspendAsync(eventId, body.Reason, ct);
            return r.Ok ? Results.Ok(ToAdminJson(r.Value!)) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).WithValidation<ModerationActionBody>().Produces<AdminEventResponse>();

        g.MapPost("/{eventId:guid}/unsuspend", async (Guid eventId, IEventService svc, CancellationToken ct) =>
        {
            var r = await svc.UnsuspendAsync(eventId, ct);
            return r.Ok ? Results.Ok(ToAdminJson(r.Value!)) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).Produces<AdminEventResponse>();

        g.MapPost("/{eventId:guid}/hide", async (Guid eventId, ModerationActionBody body, IEventService svc, CancellationToken ct) =>
        {
            var r = await svc.HideAsync(eventId, body.Reason, ct);
            return r.Ok ? Results.Ok(ToAdminJson(r.Value!)) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).WithValidation<ModerationActionBody>().Produces<AdminEventResponse>();

        g.MapPost("/{eventId:guid}/unhide", async (Guid eventId, IEventService svc, CancellationToken ct) =>
        {
            var r = await svc.UnhideAsync(eventId, ct);
            return r.Ok ? Results.Ok(ToAdminJson(r.Value!)) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).Produces<AdminEventResponse>();

        // D-186: organizer messaging — composition only (EventService.NotifyOrganizerAsync resolves
        // Event.CreatedBy and reuses the existing platform notification pipeline + audit spine). No new entity.
        g.MapPost("/{eventId:guid}/message", async (Guid eventId, OrganizerMessageBody body, ClaimsPrincipal p, IEventService svc, CancellationToken ct) =>
        {
            var r = await svc.NotifyOrganizerAsync(eventId, UserId(p), "admin.message", "Message from the Kurx team", body.Message, ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).WithValidation<OrganizerMessageBody>().Produces<OperationAck>();

        g.MapPost("/{eventId:guid}/warn", async (Guid eventId, OrganizerMessageBody body, ClaimsPrincipal p, IEventService svc, CancellationToken ct) =>
        {
            var r = await svc.NotifyOrganizerAsync(eventId, UserId(p), "admin.warning", "Warning from the Kurx team", body.Message, ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).WithValidation<OrganizerMessageBody>().Produces<OperationAck>();

        // D-191/D-193: bulk moderation — loops the SAME single-event methods every route above already
        // calls (never a parallel implementation). `reason` is required for suspend/hide, matching the
        // single-event routes' policy (BulkEventActionBodyValidator) — not enforced for unsuspend/unhide/
        // approve/reject/archive, which have no reason field in this data model at all.
        var knownActions = new HashSet<string>
            { "suspend", "unsuspend", "hide", "unhide", "approve", "reject", "archive" };
        g.MapPost("/bulk", async (BulkEventActionBody body, ClaimsPrincipal p, IEventService svc, CancellationToken ct) =>
        {
            if (!knownActions.Contains(body.Action))
                return ProblemResults.Problem("unknown_action", StatusCodes.Status400BadRequest);
            if (body.EventIds.Count == 0 || body.EventIds.Count > 200)
                return ProblemResults.Problem("invalid_batch_size", StatusCodes.Status400BadRequest);

            var actorId = UserId(p);
            var succeeded = new List<Guid>();
            var failed = new List<BulkActionFailure>();
            foreach (var eventId in body.EventIds)
            {
                (bool Ok, string? Error) r = body.Action switch
                {
                    "suspend" => Outcome(await svc.SuspendAsync(eventId, body.Reason, ct)),
                    "unsuspend" => Outcome(await svc.UnsuspendAsync(eventId, ct)),
                    "hide" => Outcome(await svc.HideAsync(eventId, body.Reason, ct)),
                    "unhide" => Outcome(await svc.UnhideAsync(eventId, ct)),
                    "approve" => Outcome(await svc.TransitionAsync(actorId, eventId, isAdmin: false, isReviewer: true, action: "publish", ct: ct)),
                    "reject" => Outcome(await svc.TransitionAsync(actorId, eventId, isAdmin: false, isReviewer: true, action: "reject", ct: ct)),
                    "archive" => Outcome(await svc.TransitionAsync(actorId, eventId, isAdmin: false, isReviewer: true, action: "archive", ct: ct)),
                    _ => (false, "unknown_action"),   // unreachable, knownActions already checked
                };
                if (r.Ok) succeeded.Add(eventId);
                else failed.Add(new BulkActionFailure(eventId, r.Error ?? "unknown_error"));
            }
            // snake_case, matching ToAdminJson's convention below — the default (camelCase) minimal-API
            // serialization would otherwise be the one inconsistent response shape on this whole route group.
            return Results.Ok(new BulkEventActionResult(
                succeeded,
                failed.Select(f => new BulkActionFailureItem(f.EventId, f.Error)).ToList()));
        }).WithValidation<BulkEventActionBody>().Produces<BulkEventActionResult>();

        // D-191: streams the same filter/rows GET "" already computes — no second filter parser, no second
        // per-event aggregate. eventIds narrows to a selection when present ("export selected").
        g.MapGet("/export", async (
            string? q, string? status, Guid? categoryId, string? city, string? visibility, bool? isPaid,
            bool? verifiedOnly, DateTime? dateFrom, DateTime? dateTo, long? revenueMin, long? revenueMax,
            int? registrationsMin, int? registrationsMax, string? sort, string? eventIds,
            IEventService svc, CancellationToken ct) =>
        {
            var filter = new AdminEventListFilter(q, status, categoryId, city, visibility, isPaid, verifiedOnly,
                dateFrom, dateTo, revenueMin, revenueMax, registrationsMin, registrationsMax, sort, 1000, 1);
            var (items, _) = await svc.ListForAdminAsync(filter, ct);
            if (!string.IsNullOrWhiteSpace(eventIds))
            {
                var selected = eventIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(s => Guid.TryParse(s, out _)).Select(Guid.Parse).ToHashSet();
                items = items.Where(e => selected.Contains(e.EventId)).ToList();
            }

            var csv = new StringBuilder();
            csv.AppendLine("EventId,Title,Status,Organization,Category,City,Visibility,Paid,Registrations,CheckedIn,RevenuePaise,Currency,StartsAt,UpdatedAt");
            foreach (var e in items)
            {
                csv.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},\"{1}\",{2},\"{3}\",{4},{5},{6},{7},{8},{9},{10},{11},{12:O},{13:O}",
                    e.EventId, e.Title.Replace("\"", "\"\""), e.Status, e.OrgName.Replace("\"", "\"\""),
                    e.CategoryName ?? "", e.City, e.Visibility, e.IsPaid, e.RegistrationsCount, e.CheckedIn,
                    e.RevenuePaise, e.Currency, e.StartsAt, e.UpdatedAt));
            }
            return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "admin-events.csv");
        }).Produces(StatusCodes.Status200OK, typeof(byte[]), "text/csv");

        // D-191: Super Admin only — a distinct policy from this group's VerificationReviewer, so mapped
        // outside `g` rather than relying on additive-policy composition.
        app.MapPost("/v1/admin/events/{eventId:guid}/emergency-edit", async (Guid eventId, EmergencyEditBody body, ClaimsPrincipal p, IEventService svc, CancellationToken ct) =>
        {
            var r = await svc.EmergencyUpdateAsync(UserId(p), eventId, body.Reason, body.Input, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).RequireAuthorization("KurxAdmin").WithValidation<EmergencyEditBody>().WithTags("admin").Produces<EventDetail>();
    }

    private static (bool Ok, string? Error) Outcome<T>(ServiceResult<T> r) => (r.Ok, r.Error);

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static AdminEventResponse ToAdminJson(AdminEventView e) => new(
        e.EventId,
        e.RepresentingOrgId,
        e.RepresentingOrgId,                   // DEPRECATED (D-273a)
        e.OrgName,
        e.Title,
        e.Slug,
        e.Status.ToLowerInvariant(),
        e.IsFeatured,
        e.StartsAt,
        e.CreatedAt,
        e.CategoryName,
        e.SubcategoryName,
        e.Visibility.ToLowerInvariant(),
        e.City,
        e.VenueName,
        e.Capacity,
        e.EndsAt,
        e.IsPaid,
        e.OrgVerificationStatus.ToLowerInvariant(),
        e.UpdatedAt,
        e.IsSuspended,
        e.SuspendedReason,
        e.IsHidden,
        e.HiddenReason,
        e.BannerKey,
        e.TicketsSold,
        e.CheckedIn,
        e.RegistrationsCount,
        e.RevenuePaise,
        e.Currency,
        // D-381 — the creator is a USER and the organization is what the event REPRESENTS. Emitted
        // separately so the console stops printing one under the other's label.
        e.CreatorId,
        e.CreatorName,
        e.OrgIsPersonal);
}
