using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

/// <summary>Ally connections (D-201) — mutual, explicitly-consented professional relationships.
/// See <c>docs/architecture/PROFESSIONAL_IDENTITY_SPEC.md</c> §5 for the state machine.</summary>
public static class AllyEndpoints
{
    // Matches MaxPendingOutgoing's order of magnitude (AllyService.cs) — no legitimate first-party
    // list page ever asks about more than a page's worth of users at once.
    private const int MaxStatusBatchSize = 200;

    public static void MapAllyEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/allies/requests", async (RequestAllyBody body, ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
        {
            var r = await svc.RequestAsync(UserId(p), body.TargetUserId, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).RequireAuthorization().WithTags("allies").Produces<AllyConnectionView>();

        app.MapPost("/v1/allies/requests/{id:guid}/accept", async (Guid id, ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
        {
            var r = await svc.AcceptAsync(UserId(p), id, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).RequireAuthorization().WithTags("allies").Produces<AllyConnectionView>();

        app.MapPost("/v1/allies/requests/{id:guid}/decline", async (Guid id, ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
        {
            var r = await svc.DeclineAsync(UserId(p), id, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).RequireAuthorization().WithTags("allies").Produces<AllyConnectionView>();

        app.MapDelete("/v1/allies/{id:guid}", async (Guid id, ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
        {
            var r = await svc.RevokeAsync(UserId(p), id, ct);
            return r.Ok ? Results.Ok(new RevokedAck(true)) : Fail(r.Error);
        }).RequireAuthorization().WithTags("allies").Produces<RevokedAck>();

        // Per-connection visibility (D-219) — hide one ally without turning off ShowAllies entirely.
        // The column and its read-side enforcement shipped with D-201; this is the missing writer.
        app.MapPatch("/v1/allies/{id:guid}/visibility", async (Guid id, AllyVisibilityBody body, ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
        {
            var r = await svc.SetVisibilityAsync(UserId(p), id, body.Visibility ?? "", ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).RequireAuthorization().WithTags("allies").Produces<AllyConnectionView>();

        // page/pageSize are optional and default to page 1 of 50 (clamped to 100 in the service), so a
        // client written before paging existed keeps working — it just stops receiving an unbounded list.
        app.MapGet("/v1/allies/requests/incoming", async (int? page, int? pageSize, ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListIncomingAsync(UserId(p), page ?? 1, pageSize ?? 50, ct))))
            .RequireAuthorization().WithTags("allies").Produces<IReadOnlyList<AllyConnectionView>>();

        app.MapGet("/v1/allies/requests/outgoing", async (int? page, int? pageSize, ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListOutgoingAsync(UserId(p), page ?? 1, pageSize ?? 50, ct))))
            .RequireAuthorization().WithTags("allies").Produces<IReadOnlyList<AllyConnectionView>>();

        app.MapGet("/v1/me/allies", async (int? page, int? pageSize, ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListMineAsync(UserId(p), page ?? 1, pageSize ?? 50, ct))))
            .RequireAuthorization().WithTags("allies").Produces<IReadOnlyList<AllyConnectionView>>();

        // Batch primitive — every person-list surface (attendees/team/org-members/speakers/search/
        // suggestions) calls this once for a whole page of users instead of once per card (no N+1).
        app.MapPost("/v1/me/allies/status-batch", async (StatusBatchBody body, ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
        {
            var ids = body.UserIds ?? Array.Empty<Guid>();
            if (ids.Length > MaxStatusBatchSize)
                return ProblemResults.Problem("too_many_targets", StatusCodes.Status400BadRequest);
            var statuses = await svc.GetStatusBatchAsync(UserId(p), ids, ct);
            return Results.Ok(statuses.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value));
        }).RequireAuthorization().WithTags("allies").Produces<IReadOnlyDictionary<string, string>>();

        app.MapGet("/v1/me/allies/mutual/{otherUserId:guid}", async (Guid otherUserId, ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
        {
            var detail = await svc.GetMutualDetailAsync(UserId(p), otherUserId, ct);
            return Results.Ok(detail);
        }).RequireAuthorization().WithTags("allies").Produces<MutualDetail>();

        app.MapGet("/v1/me/allies/suggestions", async (int? limit, ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
        {
            var suggestions = await svc.GetSuggestionsAsync(UserId(p), Math.Clamp(limit ?? 20, 1, 50), ct);
            return Results.Ok(suggestions);
        }).RequireAuthorization().WithTags("allies").Produces<IReadOnlyList<AllySuggestion>>();
    }



    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "invalid_state" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        "invalid_visibility" => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
        // Both are throttles the caller can retry later, so both are 429 rather than a flat 400.
        "too_many_pending_requests" or "declined_recently" =>
            ProblemResults.Problem(error, StatusCodes.Status429TooManyRequests),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}

public record RequestAllyBody(Guid TargetUserId);
public record StatusBatchBody(Guid[]? UserIds);
public record AllyVisibilityBody(string? Visibility);
