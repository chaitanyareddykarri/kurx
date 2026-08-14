using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;

public static class PublicProfileEndpoints
{
    /// <summary>These endpoints stay anonymous-allowed. A bearer token, when present, is read only to
    /// widen what the caller may see under the Connections / EventParticipants tiers (D-221) — it is
    /// never required, and an absent or unparseable token simply reads as anonymous.</summary>
    private static Guid? ViewerId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>Marks a response as viewer-specific so no shared cache may reuse it (D-229/C3).
    ///
    /// <para>These routes were genuinely public and cacheable until D-221 made them viewer-aware. From
    /// that moment a CDN, reverse proxy or any intermediary cache could store one viewer's entitled
    /// view and serve it to another — including a resume PDF containing sections hidden from the
    /// public. Nothing in the backend logic prevents that; only these headers do.</para>
    ///
    /// <para><c>private</c> forbids shared caches, <c>no-store</c> forbids writing it down at all, and
    /// <c>Vary: Authorization</c> is belt-and-braces for any cache that honours Vary but not
    /// Cache-Control.</para></summary>
    private static void MarkViewerSpecific(HttpContext http)
    {
        http.Response.Headers.CacheControl = "private, no-store, no-cache, max-age=0";
        http.Response.Headers.Vary = "Authorization";
    }

    public static void MapPublicProfileEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/public/users").WithTags("public-profile");

        // Applied to the whole group rather than per-route: a route added later inherits it, which is
        // the opposite of the failure mode that produced this finding.
        g.AddEndpointFilter(async (ctx, next) =>
        {
            MarkViewerSpecific(ctx.HttpContext);
            return await next(ctx);
        });

        // Every handler below returns its Application record straight out of Results.Ok. The response
        // converter derives each snake_case key from the property name, so the wire is byte-identical to
        // the hand-written mappers this replaces — verified key by key — while the spec finally gains a
        // schema for responses it previously described as a bare "200 OK".
        g.MapGet("", async (string? q, int? page, int? pageSize, IPublicProfileService svc, CancellationToken ct) =>
        {
            var results = await svc.SearchUsersAsync(q ?? "", page ?? 1, Math.Clamp(pageSize ?? 20, 1, 50), ct);
            return Results.Ok(results);
        }).Produces<IReadOnlyList<PublicUserSearchResult>>();

        g.MapGet("/{username}", async (string username, ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var result = await svc.GetProfileAsync(username, ViewerId(p), ct);
            // The record is returned directly; the response converter derives every snake_case key
            // from the property names, so the wire is byte-identical to the mapper this replaces.
            // `_meta` is attached here rather than in the service because the provenance vocabulary
            // is an API-surface concern, not something the profile aggregate knows about.
            return result.Ok
                ? Results.Ok(result.Value! with { Meta = ProvenanceMeta })
                : Fail(result.Error);
        }).Produces<PublicProfileView>();

        g.MapGet("/{username}/timeline", async (string username, int? page, int? pageSize,
            ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var result = await svc.GetTimelineAsync(username, page ?? 1, Math.Clamp(pageSize ?? 15, 1, 50), ViewerId(p), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<IReadOnlyList<TimelineEntry>>();

        g.MapGet("/{username}/events", async (string username, string? type, int? page, int? pageSize,
            ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var result = await svc.GetEventsAsync(username, type ?? "conducted", page ?? 1, pageSize ?? 12, ViewerId(p), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<IReadOnlyList<PublicEventCard>>();

        g.MapGet("/{username}/certificates", async (string username, int? page,
            ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var result = await svc.GetCertificatesAsync(username, page ?? 1, 12, ViewerId(p), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<IReadOnlyList<PublicCertificateCard>>();

        g.MapGet("/{username}/metrics", async (string username,
            ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var result = await svc.GetMetricsAsync(username, ViewerId(p), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<ProfileMetrics>();

        g.MapGet("/{username}/experience", async (string username,
            ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var result = await svc.GetExperienceAsync(username, ViewerId(p), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<ExperienceSummary>();

        g.MapGet("/{username}/contributions", async (string username, int? months,
            ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var result = await svc.GetContributionsAsync(username, months ?? 12, ViewerId(p), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<ContributionsSummary>();

        // The resume (D-228). Rate-limited because PDF rendering is by far the most expensive public
        // operation on the platform, and the caller controls nothing about the document's composition
        // — there is deliberately no template or section parameter to inject through.
        g.MapGet("/{username}/resume", async (string username, HttpContext http,
            ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var profileUrl = $"{http.Request.Scheme}://{http.Request.Host}/u/{username}";
            var result = await svc.GetResumePdfAsync(username, profileUrl, ViewerId(p), ct);
            if (!result.Ok) return Fail(result.Error);

            return Results.File(result.Value!, "application/pdf", $"{username}-kurx-resume.pdf");
        }).RequireRateLimiting("resume").Produces(StatusCodes.Status200OK, typeof(byte[]), "application/pdf");

        // The Professional Journey (D-223) — unpaginated by design: it is a first-attainment ladder
        // with at most one node per tier, so it is bounded by the tier vocabulary, not by activity.
        g.MapGet("/{username}/journey", async (string username,
            ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var result = await svc.GetJourneyAsync(username, ViewerId(p), ct);
            return result.Ok ? Results.Ok(result.Value!.Select(JourneyNodeView.From)) : Fail(result.Error);
        }).Produces<IReadOnlyList<JourneyNodeView>>();

        // Sub-resources for the verified sources wired in D-222. New routes rather than new keys on the
        // root: the root response shape is frozen, and a section nobody opens costs nothing.
        g.MapGet("/{username}/competitions", async (string username, int? page, int? pageSize,
            ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var result = await svc.GetCompetitionResultsAsync(username, page ?? 1, Math.Clamp(pageSize ?? 12, 1, 50), ViewerId(p), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<IReadOnlyList<CompetitionResultCard>>();

        g.MapGet("/{username}/assignments", async (string username, int? page, int? pageSize,
            ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAssignmentsAsync(username, page ?? 1, Math.Clamp(pageSize ?? 12, 1, 50), ViewerId(p), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<IReadOnlyList<AssignmentCard>>();

        g.MapGet("/{username}/sessions", async (string username, int? page, int? pageSize,
            ClaimsPrincipal p, IPublicProfileService svc, CancellationToken ct) =>
        {
            var result = await svc.GetSpeakerSessionsAsync(username, page ?? 1, Math.Clamp(pageSize ?? 12, 1, 50), ViewerId(p), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<IReadOnlyList<SpeakerSessionCard>>();

        g.MapGet("/{username}/allies", async (string username, int? page, int? pageSize,
            ClaimsPrincipal p, IAllyService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAlliesForProfileAsync(username, page ?? 1, Math.Clamp(pageSize ?? 24, 1, 100), ViewerId(p), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<IReadOnlyList<AllyProfileCard>>();
    }

    private static readonly IReadOnlyDictionary<string, string> ProvenanceMeta = new Dictionary<string, string>
    {
        ["name"] = "self_declared",
        ["username"] = "self_declared",
        ["headline"] = "self_declared",
        ["bio"] = "self_declared",
        ["avatar_key"] = "self_declared",
        ["cover_key"] = "self_declared",
        ["links"] = "self_declared",
        ["skills"] = "self_declared",
        ["college"] = "self_declared",      // unverified affiliation; the verified path is a membership claim (D-220)
        ["stats"] = "verified",
        ["verification"] = "verified",
        ["organizations"] = "verified",
        ["achievements"] = "verified",
        ["joined_at"] = "verified",         // the account row's own creation stamp, not a claim
        ["summary"] = "derived",
        ["event_dna"] = "derived",
        ["identity_labels"] = "derived",
        ["derived_headline"] = "derived",
    };


    private static IResult Fail(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
