using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record CreateReportBody(string EntityType, Guid EntityId, string Reason, string? Details);

/// <summary>Content moderation (C-5, D-059). Any authenticated user files a report; Moderation staff
/// (SuperAdmin / VerificationReviewer / Support) triage the queue and resolve/dismiss. Validation and the
/// audit-log write live in <see cref="IReportService"/> (Api never does business logic, CLAUDE.md §2).</summary>
public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/reports", async (CreateReportBody body, ClaimsPrincipal p, IReportService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateAsync(UserId(p), body.EntityType, body.EntityId, body.Reason, body.Details, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).RequireAuthorization().WithTags("reports").Produces<ReportView>();

        var admin = app.MapGroup("/v1/admin/reports").WithTags("admin").RequireAuthorization("Moderation");

        admin.MapGet("", async (string? status, int? limit, IReportService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListAsync(status, limit ?? 50, ct)))).Produces<IReadOnlyList<ReportView>>();

        admin.MapPost("/{id:guid}/resolve", async (Guid id, ClaimsPrincipal p, IReportService svc, CancellationToken ct) =>
        {
            var r = await svc.ResolveAsync(UserId(p), id, dismiss: false, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<ReportView>();

        admin.MapPost("/{id:guid}/dismiss", async (Guid id, ClaimsPrincipal p, IReportService svc, CancellationToken ct) =>
        {
            var r = await svc.ResolveAsync(UserId(p), id, dismiss: true, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<ReportView>();
    }

    private static IResult Fail(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "already_reported" or "already_closed" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };


    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
