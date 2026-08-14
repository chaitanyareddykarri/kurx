using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record SponsorBody(string Name, string? LogoKey, string? Website, string? Tier, int? Priority);
public record AssignSponsorBody(Guid SponsorId, int? Sort);

public static class SponsorEndpoints
{
    public static void MapSponsorEndpoints(this WebApplication app)
    {
        var sponsors = app.MapGroup("/v1/orgs/{orgId:guid}/sponsors").WithTags("sponsors").RequireAuthorization();

        sponsors.MapPost("/", async (Guid orgId, SponsorBody body, ClaimsPrincipal principal, ISponsorService svc, CancellationToken ct) =>
        {
            var input = new SponsorInput(body.Name, body.LogoKey, body.Website, body.Tier, body.Priority);
            var result = await svc.CreateAsync(UserId(principal), orgId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(ToJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<SponsorBody>().Produces<SponsorResponse>();

        sponsors.MapGet("/", async (Guid orgId, ClaimsPrincipal principal, ISponsorService svc, CancellationToken ct) =>
        {
            var result = await svc.ListForOrgAsync(UserId(principal), orgId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value!.Select(ToJson)) : Fail(result.Error);
        }).Produces<IReadOnlyList<SponsorResponse>>();

        sponsors.MapPatch("/{sponsorId:guid}", async (Guid orgId, Guid sponsorId, SponsorBody body, ClaimsPrincipal principal, ISponsorService svc, CancellationToken ct) =>
        {
            var input = new SponsorInput(body.Name, body.LogoKey, body.Website, body.Tier, body.Priority);
            var result = await svc.UpdateAsync(UserId(principal), orgId, sponsorId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(ToJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<SponsorBody>().Produces<SponsorResponse>();

        sponsors.MapDelete("/{sponsorId:guid}", async (Guid orgId, Guid sponsorId, ClaimsPrincipal principal, ISponsorService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteAsync(UserId(principal), orgId, sponsorId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        var events = app.MapGroup("/v1/orgs/{orgId:guid}/events/{eventId:guid}/sponsors").WithTags("sponsors").RequireAuthorization();

        events.MapGet("/", async (Guid orgId, Guid eventId, ISponsorService svc, CancellationToken ct)
            => Results.Ok((await svc.ListForEventAsync(eventId, ct)).Select(ToJson))).Produces<IReadOnlyList<SponsorResponse>>();

        events.MapPost("/", async (Guid orgId, Guid eventId, AssignSponsorBody body, ClaimsPrincipal principal, ISponsorService svc, CancellationToken ct) =>
        {
            var result = await svc.AssignToEventAsync(UserId(principal), eventId, IsAdmin(principal), body.SponsorId, body.Sort, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        events.MapDelete("/{sponsorId:guid}", async (Guid orgId, Guid eventId, Guid sponsorId, ClaimsPrincipal principal, ISponsorService svc, CancellationToken ct) =>
        {
            var result = await svc.RemoveFromEventAsync(UserId(principal), eventId, IsAdmin(principal), sponsorId, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static SponsorResponse ToJson(SponsorView s) => new(
        s.Id,
        s.OrgId,
        s.Name,
        s.LogoKey,
        s.Website,
        s.Tier.ToLowerInvariant(),
        s.Priority);
}
