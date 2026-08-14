using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

public record SeriesBody(string? Name, string? Mode, string? Description, string? BannerKey, string? BrandAssetsJson,
    string? Rrule, IReadOnlyList<string>? ExceptionDates);
public record SeriesAttachBody(Guid EventId, int? EditionOrdinal, string? EditionLabel);

/// <summary>V3 §13.2 (Phase 12) — the EventSeries surface: create/manage a RECURRING or EDITIONS series, attach/detach
/// member events (an event belongs to ≤1 series, §3.4 rule 5), and follow a series (followers carry across members).
/// Series pages and member lists are public reads; management + follow require authentication; organiser gates
/// (Owner/Manager/Representative) live in the service. Additive; no existing route or response shape changed.</summary>
public static class SeriesEndpoints
{
    public static void MapSeriesEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1").WithTags("series").RequireAuthorization();

        g.MapPost("/orgs/{orgId:guid}/series", async (Guid orgId, SeriesBody b, ClaimsPrincipal p, ISeriesService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateAsync(UserId(p), orgId, IsAdmin(p), ToInput(b), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<SeriesView>();

        g.MapGet("/orgs/{orgId:guid}/series", async (Guid orgId, ISeriesService svc, CancellationToken ct) =>
        {
            var r = await svc.ListForOrgAsync(orgId, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<SeriesView>>();

        g.MapGet("/series/{seriesId:guid}", async (Guid seriesId, ISeriesService svc, CancellationToken ct) =>
        {
            var r = await svc.GetAsync(seriesId, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).AllowAnonymous().Produces<SeriesView>();

        g.MapGet("/series/{seriesId:guid}/events", async (Guid seriesId, ClaimsPrincipal p, ISeriesService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListMembersAsync(seriesId, OptionalUserId(p), IsAdmin(p), ct)))
            .AllowAnonymous().Produces<IReadOnlyList<SeriesMemberView>>();

        g.MapPatch("/series/{seriesId:guid}", async (Guid seriesId, SeriesBody b, ClaimsPrincipal p, ISeriesService svc, CancellationToken ct) =>
        {
            var r = await svc.UpdateAsync(UserId(p), seriesId, IsAdmin(p), ToInput(b), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<SeriesView>();

        g.MapDelete("/series/{seriesId:guid}", async (Guid seriesId, ClaimsPrincipal p, ISeriesService svc, CancellationToken ct) =>
        {
            var r = await svc.DeleteAsync(UserId(p), seriesId, IsAdmin(p), ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        g.MapPost("/series/{seriesId:guid}/events", async (Guid seriesId, SeriesAttachBody b, ClaimsPrincipal p, ISeriesService svc, CancellationToken ct) =>
        {
            var r = await svc.AttachEventAsync(UserId(p), seriesId, IsAdmin(p), new SeriesMemberInput(b.EventId, b.EditionOrdinal, b.EditionLabel), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<SeriesMemberView>();

        g.MapDelete("/series/{seriesId:guid}/events/{eventId:guid}", async (Guid seriesId, Guid eventId, ClaimsPrincipal p, ISeriesService svc, CancellationToken ct) =>
        {
            var r = await svc.DetachEventAsync(UserId(p), seriesId, eventId, IsAdmin(p), ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        g.MapPost("/series/{seriesId:guid}/follow", async (Guid seriesId, ClaimsPrincipal p, ISeriesService svc, CancellationToken ct) =>
        {
            var r = await svc.FollowAsync(UserId(p), seriesId, ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        g.MapDelete("/series/{seriesId:guid}/follow", async (Guid seriesId, ClaimsPrincipal p, ISeriesService svc, CancellationToken ct) =>
        {
            var r = await svc.UnfollowAsync(UserId(p), seriesId, ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);
    }

    private static SeriesInput ToInput(SeriesBody b) => new(b.Name, b.Mode, b.Description, b.BannerKey, b.BrandAssetsJson, b.Rrule, b.ExceptionDates);

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static Guid? OptionalUserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
