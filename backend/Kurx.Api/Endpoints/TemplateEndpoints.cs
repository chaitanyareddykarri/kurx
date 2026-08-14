using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record CreateTemplateBody(string Scope, Guid? OrgId, Guid? OrgUnitId, string Name,
    string? Description, string? KindSlug, string? ConfigJson, string? DefaultSectionsJson);
public record UpdateTemplateBody(string? Name, string? Description, string? KindSlug,
    string? ConfigJson, string? DefaultSectionsJson);
public record CloneTemplateBody(string? Name);

public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this WebApplication app)
    {
        // Public catalog (V3 §13.1): most-specific-first templates visible in a context. Anonymous = Platform only.
        app.MapGet("/v1/templates", async (Guid? orgId, Guid? orgUnitId, ClaimsPrincipal principal, ITemplateService svc, CancellationToken ct)
            => Results.Ok((await svc.ListForContextAsync(orgId, orgUnitId, TryUserId(principal), ct))))
            .WithTags("templates").Produces<IReadOnlyList<TemplateView>>();

        var t = app.MapGroup("/v1/templates").WithTags("templates").RequireAuthorization();

        t.MapPost("/", async (CreateTemplateBody body, ClaimsPrincipal principal, ITemplateService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(UserId(principal), IsAdmin(principal),
                new TemplateInput(body.Scope, body.OrgId, body.OrgUnitId, body.Name, body.Description, body.KindSlug, body.ConfigJson, body.DefaultSectionsJson), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).WithValidation<CreateTemplateBody>().Produces<TemplateView>();

        t.MapPatch("/{templateId:guid}", async (Guid templateId, UpdateTemplateBody body, ClaimsPrincipal principal, ITemplateService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateAsync(UserId(principal), templateId, IsAdmin(principal),
                body.Name, body.Description, body.KindSlug, body.ConfigJson, body.DefaultSectionsJson, ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<TemplateView>();

        t.MapPost("/{templateId:guid}/publish", async (Guid templateId, ClaimsPrincipal principal, ITemplateService svc, CancellationToken ct) =>
        {
            var result = await svc.PublishAsync(UserId(principal), templateId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<TemplateView>();

        t.MapPost("/{templateId:guid}/versions", async (Guid templateId, ClaimsPrincipal principal, ITemplateService svc, CancellationToken ct) =>
        {
            var result = await svc.NewVersionAsync(UserId(principal), templateId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<TemplateView>();

        t.MapPost("/{templateId:guid}/archive", async (Guid templateId, ClaimsPrincipal principal, ITemplateService svc, CancellationToken ct) =>
        {
            var result = await svc.ArchiveAsync(UserId(principal), templateId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<TemplateView>();

        t.MapPost("/{templateId:guid}/clone", async (Guid templateId, CloneTemplateBody? body, ClaimsPrincipal principal, ITemplateService svc, CancellationToken ct) =>
        {
            var result = await svc.CloneAsync(UserId(principal), templateId, IsAdmin(principal), body?.Name, ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<TemplateView>();

        t.MapDelete("/{templateId:guid}", async (Guid templateId, ClaimsPrincipal principal, ITemplateService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteAsync(UserId(principal), templateId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static Guid? TryUserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "template_in_use" or "system_template" or "not_draft" or "draft_exists" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

}
