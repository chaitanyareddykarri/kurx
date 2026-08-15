using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record CreateCertificateTemplateBody(string Name, string? PageSize);
public record UpdateCertificateTemplateBody(string? Name, string? PageSize, string? Status);
public record PresignCertificateBackgroundBody(string ContentType, long MaxBytes);
public record SetCertificateBackgroundBody(string StorageKey, string ContentType, int WidthPx, int HeightPx);
public record ReplaceCertificateFieldsBody(IReadOnlyList<CertificateFieldInput> Fields);
public record IssueCertificateBody(string RecipientName, string? Email, Dictionary<string, string>? Values);

/// <summary>
/// Certificate designs (D-344, Phase 3).
///
/// <para>Two entry points by design. Event templates are created under an event, because that is where
/// authority for them comes from. Every by-id route then re-resolves authority from the template's own
/// event — or, for a library template, from its owner — rather than trusting the path, which is what stops
/// one event's manager editing another's designs.</para>
/// </summary>
public static class CertificateTemplateEndpoints
{
    public static void MapCertificateTemplateEndpoints(this WebApplication app)
    {
        var byEvent = app.MapGroup("/v1/events/{eventId:guid}/certificate-templates")
            .WithTags("certificate-templates").RequireAuthorization();

        byEvent.MapGet("/", async (Guid eventId, ClaimsPrincipal p, ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.ListForEventAsync(UserId(p), eventId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<CertificateTemplateView>>();

        byEvent.MapPost("/", async (Guid eventId, CreateCertificateTemplateBody body, ClaimsPrincipal p,
            ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateAsync(UserId(p), eventId,
                new CertificateTemplateInput(body.Name, body.PageSize), IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<CertificateTemplateView>();

        // The caller's own reusable library — templates attached to no event.
        var library = app.MapGroup("/v1/me/certificate-templates")
            .WithTags("certificate-templates").RequireAuthorization();

        library.MapGet("/", async (ClaimsPrincipal p, ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.ListLibraryAsync(UserId(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<CertificateTemplateView>>();

        library.MapPost("/", async (CreateCertificateTemplateBody body, ClaimsPrincipal p,
            ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateAsync(UserId(p), null,
                new CertificateTemplateInput(body.Name, body.PageSize), IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<CertificateTemplateView>();

        var byId = app.MapGroup("/v1/certificate-templates/{templateId:guid}")
            .WithTags("certificate-templates").RequireAuthorization();

        byId.MapGet("/", async (Guid templateId, ClaimsPrincipal p, ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.GetAsync(UserId(p), templateId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<CertificateTemplateView>();

        byId.MapPatch("/", async (Guid templateId, UpdateCertificateTemplateBody body, ClaimsPrincipal p,
            ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.UpdateAsync(UserId(p), templateId,
                new CertificateTemplateInput(body.Name, body.PageSize, body.Status), IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<CertificateTemplateView>();

        // Step 1 of an artwork upload: the browser PUTs the bytes straight to storage with this URL, so
        // no image passes through the API.
        byId.MapPost("/background/presign", async (Guid templateId, PresignCertificateBackgroundBody body,
            ClaimsPrincipal p, ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.PresignBackgroundAsync(UserId(p), templateId, body.ContentType, body.MaxBytes, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<PresignedUpload>();

        // Step 2: record it, once the bytes have actually landed.
        byId.MapPut("/background", async (Guid templateId, SetCertificateBackgroundBody body, ClaimsPrincipal p,
            ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.SetBackgroundAsync(UserId(p), templateId,
                new CertificateBackgroundInput(body.StorageKey, body.ContentType, body.WidthPx, body.HeightPx),
                IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<CertificateTemplateView>();

        // Reuse (D-344, Phase 13). Two verbs rather than one with a target, because "save this for later"
        // and "use this here" are different intentions and the second needs an entitlement on a
        // destination event that the first has no concept of.
        byId.MapPost("/copy-to-library", async (Guid templateId, CopyCertificateTemplateBody? body,
            ClaimsPrincipal p, ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.CopyToLibraryAsync(UserId(p), templateId, body?.Name, IsAdmin(p), ct);
            return r.Ok
                ? Results.Created($"/v1/certificate-templates/{r.Value!.Id}", r.Value)
                : Fail(r.Error);
        }).Produces<CertificateTemplateView>(StatusCodes.Status201Created);

        byId.MapPost("/copy-to-event/{eventId:guid}", async (Guid templateId, Guid eventId,
            CopyCertificateTemplateBody? body, ClaimsPrincipal p,
            ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.CopyToEventAsync(UserId(p), templateId, eventId, body?.Name, IsAdmin(p), ct);
            return r.Ok
                ? Results.Created($"/v1/certificate-templates/{r.Value!.Id}", r.Value)
                : Fail(r.Error);
        }).Produces<CertificateTemplateView>(StatusCodes.Status201Created);

        // Where the design already has something printed (D-344). The editor fetches this once and checks
        // overlaps locally as a field is dragged — a request per mouse move would be unusable, and reading
        // the artwork's pixels in the browser is blocked by canvas tainting whenever storage is a
        // different origin, which it is by default.
        byId.MapGet("/artwork-map", async (Guid templateId, ClaimsPrincipal p,
            ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.ArtworkMapAsync(UserId(p), templateId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<CertificateArtworkMap>();

        // The artwork's own colour behind a region — what a field needs to cover printed text without
        // leaving a visible patch (D-344).
        byId.MapGet("/artwork-colour", async (Guid templateId, double x, double y, double width,
            double height, ClaimsPrincipal p, ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.ArtworkColourAsync(
                UserId(p), templateId, new CertificateRegion(x, y, width, height), IsAdmin(p), ct);
            return r.Ok ? Results.Ok(new { colour = r.Value }) : Fail(r.Error);
        }).Produces<object>();

        // The OCR extension point (D-344, Phase 12). Answers 200 with `available: false` rather than an
        // error status: no engine configured is a fact about this deployment, not a failed request, and an
        // editor renders a capability from it rather than handling an exception. No build currently
        // registers an engine, so today the answer is always "not available" — and nothing in the save,
        // preview, approve or issue paths consults this at all.
        byId.MapPost("/detect-text", async (Guid templateId, ClaimsPrincipal p,
            ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.DetectBackgroundTextAsync(UserId(p), templateId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<TextDetectionResult>();

        // The editor's save. Whole-set rather than per-field, because that is what the canvas holds.
        byId.MapPut("/fields", async (Guid templateId, ReplaceCertificateFieldsBody body, ClaimsPrincipal p,
            ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.ReplaceFieldsAsync(UserId(p), templateId, body.Fields ?? [], IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<CertificateTemplateView>();

        // Preview with sample data — the SAME renderer issuing uses, so what comes back is a rendered
        // certificate rather than a picture of one. Returned inline so the browser can display it without
        // a download, which is what makes it usable as a check rather than a chore.
        byId.MapGet("/preview", async (Guid templateId, string? format, ClaimsPrincipal p,
            ICertificateIssuingService svc, CancellationToken ct) =>
        {
            var r = await svc.PreviewAsync(UserId(p), templateId, format ?? "png", IsAdmin(p), ct);
            if (!r.Ok) return Fail(r.Error);
            return Results.File(r.Value!.Bytes, r.Value.ContentType, r.Value.FileName, enableRangeProcessing: false);
        }).RequireRateLimiting("heavy");

        // One certificate, issued by hand. Bulk generation is a later phase and will call the same
        // assembly and the same renderer from a background job.
        byId.MapPost("/issue", async (Guid templateId, IssueCertificateBody body, ClaimsPrincipal p,
            ICertificateIssuingService svc, CancellationToken ct) =>
        {
            var r = await svc.IssueAsync(UserId(p), templateId,
                new IssueCertificateInput(body.RecipientName, body.Email,
                    body.Values ?? new Dictionary<string, string>()), IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).RequireRateLimiting("heavy").Produces<IssuedCertificateView>();

        byId.MapDelete("/", async (Guid templateId, ClaimsPrincipal p, ICertificateTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.ArchiveAsync(UserId(p), templateId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : Fail(r.Error);
        }).Produces<OperationAck>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "not_found" or "upload_not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}

/// <param name="Name">A new name for the copy, or null to keep the original's.</param>
public sealed record CopyCertificateTemplateBody(string? Name);
