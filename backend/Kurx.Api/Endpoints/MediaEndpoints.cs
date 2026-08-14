using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record PresignMediaBody(string ContentType, long MaxBytes);
public record AttachMediaBody(string Kind, string Key, string? Caption);
/// <summary><c>Slot</c> is "avatar" or "cover" (D-219).</summary>
public record ProfileImagePresignBody(string? Slot, string? ContentType, long MaxBytes);

public static class MediaEndpoints
{
    private const long MaxUploadBytes = 25 * 1024 * 1024;
    // A profile picture is displayed at ≤ a few hundred px; 25 MB of event brochure is the wrong ceiling.
    private const long MaxProfileImageBytes = 5 * 1024 * 1024;

    public static void MapMediaEndpoints(this WebApplication app)
    {
        var media = app.MapGroup("/v1/orgs/{orgId:guid}/events/{eventId:guid}/media").WithTags("media").RequireAuthorization();

        media.MapPost("/presign", async (Guid orgId, Guid eventId, PresignMediaBody body, ClaimsPrincipal principal, IMediaService svc, CancellationToken ct) =>
        {
            var maxBytes = Math.Clamp(body.MaxBytes, 1, MaxUploadBytes);
            var result = await svc.PresignUploadAsync(UserId(principal), eventId, IsAdmin(principal), body.ContentType, maxBytes, ct);
            return result.Ok
                ? Results.Ok(result.Value!)
                : Fail(result.Error);
        }).WithValidation<PresignMediaBody>().Produces<PresignedUpload>();

        media.MapPost("/", async (Guid orgId, Guid eventId, AttachMediaBody body, ClaimsPrincipal principal, IMediaService svc, CancellationToken ct) =>
        {
            var result = await svc.AttachAsync(UserId(principal), eventId, IsAdmin(principal), body.Kind, body.Key, body.Caption, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).WithValidation<AttachMediaBody>().Produces<OperationAck>();

        media.MapDelete("/{mediaId:guid}", async (Guid orgId, Guid eventId, Guid mediaId, ClaimsPrincipal principal, IMediaService svc, CancellationToken ct) =>
        {
            var result = await svc.RemoveAsync(UserId(principal), eventId, IsAdmin(principal), mediaId, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        // Org-level credential uploads (verification letterhead / membership proof, D-055).
        // Separate from event media: keyed under orgs/{id}/verification/* so the credential is reused
        // across the org's events and can be uploaded before any event exists.
        var orgMedia = app.MapGroup("/v1/orgs/{orgId:guid}/media").WithTags("media").RequireAuthorization();

        orgMedia.MapPost("/presign", async (Guid orgId, PresignMediaBody body, ClaimsPrincipal principal, IMediaService svc, CancellationToken ct) =>
        {
            var maxBytes = Math.Clamp(body.MaxBytes, 1, MaxUploadBytes);
            var result = await svc.PresignOrgDocAsync(UserId(principal), orgId, IsAdmin(principal), body.ContentType, maxBytes, ct);
            return result.Ok
                ? Results.Ok(result.Value!)
                : Fail(result.Error);
        }).WithValidation<PresignMediaBody>().Produces<PresignedUpload>();

        // Membership-claim evidence upload (D-055 G4): any authenticated user uploads proof for their own
        // claim to an existing org — no membership required, unlike the org-verification presign above.
        app.MapPost("/v1/orgs/{orgId:guid}/membership-claims/media/presign",
            async (Guid orgId, PresignMediaBody body, ClaimsPrincipal principal, IMediaService svc, CancellationToken ct) =>
        {
            var maxBytes = Math.Clamp(body.MaxBytes, 1, MaxUploadBytes);
            var result = await svc.PresignClaimDocAsync(UserId(principal), orgId, body.ContentType, maxBytes, ct);
            return result.Ok
                ? Results.Ok(result.Value!)
                : Fail(result.Error);
        }).WithTags("membership").RequireAuthorization().WithValidation<PresignMediaBody>().Produces<PresignedUpload>();

        // Profile image upload (D-219): the caller's own avatar/cover. User-scoped with no org and no role
        // check — you may always replace your own picture. Two steps like every other upload here: presign
        // hands out a URL, then PATCH /v1/me/profile persists the returned key onto the user row.
        app.MapPost("/v1/me/profile-image/presign",
            async (ProfileImagePresignBody body, ClaimsPrincipal principal, IMediaService svc, CancellationToken ct) =>
        {
            var maxBytes = Math.Clamp(body.MaxBytes, 1, MaxProfileImageBytes);
            var result = await svc.PresignProfileImageAsync(UserId(principal), body.Slot ?? "", body.ContentType ?? "", maxBytes, ct);
            return result.Ok
                ? Results.Ok(result.Value!)
                : Fail(result.Error);
        }).WithTags("me").RequireAuthorization().Produces<PresignedUpload>();

        // Representation-request evidence upload (D-076): proof for a NOT-yet-registered institution, so it's
        // user-scoped with no orgId. SubmitRepresentationRequestAsync records these keys on the placeholder org.
        app.MapPost("/v1/orgs/representation-requests/media/presign",
            async (PresignMediaBody body, ClaimsPrincipal principal, IMediaService svc, CancellationToken ct) =>
        {
            var maxBytes = Math.Clamp(body.MaxBytes, 1, MaxUploadBytes);
            var result = await svc.PresignRepresentationDocAsync(UserId(principal), body.ContentType, maxBytes, ct);
            return result.Ok
                ? Results.Ok(result.Value!)
                : Fail(result.Error);
        }).WithTags("orgs").RequireAuthorization().WithValidation<PresignMediaBody>().Produces<PresignedUpload>();
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
}
