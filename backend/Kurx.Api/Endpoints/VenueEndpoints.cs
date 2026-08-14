using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record VenueBody(string Name, string? Address, string? City, double? Lat, double? Lng,
    string? GoogleMapsUrl, int? Capacity, bool? HasParking, bool? IsAccessible, string? Notes);
public record AddVenueImageBody(string Key);

public static class VenueEndpoints
{
    public static void MapVenueEndpoints(this WebApplication app)
    {
        var venues = app.MapGroup("/v1/orgs/{orgId:guid}/venues").WithTags("venues").RequireAuthorization();

        venues.MapPost("/", async (Guid orgId, VenueBody body, ClaimsPrincipal principal, IVenueService svc, CancellationToken ct) =>
        {
            var input = new VenueInput(body.Name, body.Address, body.City, body.Lat, body.Lng,
                body.GoogleMapsUrl, body.Capacity, body.HasParking, body.IsAccessible, body.Notes);
            var result = await svc.CreateAsync(UserId(principal), orgId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).WithValidation<VenueBody>().Produces<VenueView>();

        venues.MapGet("/", async (Guid orgId, ClaimsPrincipal principal, IVenueService svc, CancellationToken ct) =>
        {
            var result = await svc.ListForOrgAsync(UserId(principal), orgId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<IReadOnlyList<VenueView>>();

        venues.MapGet("/{venueId:guid}", async (Guid orgId, Guid venueId, ClaimsPrincipal principal, IVenueService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAsync(UserId(principal), venueId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<VenueView>();

        venues.MapPatch("/{venueId:guid}", async (Guid orgId, Guid venueId, VenueBody body, ClaimsPrincipal principal, IVenueService svc, CancellationToken ct) =>
        {
            var input = new VenueInput(body.Name, body.Address, body.City, body.Lat, body.Lng,
                body.GoogleMapsUrl, body.Capacity, body.HasParking, body.IsAccessible, body.Notes);
            var result = await svc.UpdateAsync(UserId(principal), venueId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).WithValidation<VenueBody>().Produces<VenueView>();

        venues.MapDelete("/{venueId:guid}", async (Guid orgId, Guid venueId, ClaimsPrincipal principal, IVenueService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteAsync(UserId(principal), venueId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        venues.MapPost("/{venueId:guid}/images", async (Guid orgId, Guid venueId, AddVenueImageBody body, ClaimsPrincipal principal, IVenueService svc, CancellationToken ct) =>
        {
            var result = await svc.AddImageAsync(UserId(principal), venueId, IsAdmin(principal), body.Key, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        venues.MapDelete("/{venueId:guid}/images/{imageId:guid}", async (Guid orgId, Guid venueId, Guid imageId, ClaimsPrincipal principal, IVenueService svc, CancellationToken ct) =>
        {
            var result = await svc.RemoveImageAsync(UserId(principal), venueId, IsAdmin(principal), imageId, ct);
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
        "venue_in_use" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

}
