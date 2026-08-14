using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Auth;
using Kurx.Api.Validation;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Api.Endpoints;

public record RequestRefundBody(string Reason);

/// <summary>D-199: the HTTP surface over the pre-existing <c>IRefundService</c> (D-103). Reuses the same
/// financial-access bar as <c>WalletEndpoints</c> (Owner/Finance of the event's org) plus a FinanceOps/
/// SuperAdmin override — never a new permission model.</summary>
public static class RefundEndpoints
{
    public static void MapRefundEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/orders/{orderId:guid}/refund", async (Guid orderId, RequestRefundBody body,
            ClaimsPrincipal principal, IRefundService svc, CancellationToken ct) =>
        {
            var result = await svc.RequestRefundAsync(orderId, body.Reason, UserId(principal), IsFinanceStaff(principal), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).WithTags("refunds").RequireAuthorization().WithValidation<RequestRefundBody>()
          .WithSummary("Refund a Paid order in full — Owner/Finance of the event's org, or FinanceOps/SuperAdmin").Produces<RefundResult>();

        app.MapGet("/v1/orders/{orderId:guid}/refund", async (Guid orderId, ClaimsPrincipal principal,
            IRefundService svc, CancellationToken ct) =>
        {
            var result = await svc.GetForOrderAsync(orderId, UserId(principal), IsFinanceStaff(principal), ct);
            return result.Ok ? Results.Ok(ToViewJson(result.Value!)) : Fail(result.Error);
        }).WithTags("refunds").RequireAuthorization()
          .WithSummary("The refund for one order — visible to its buyer, the org's Owner/Finance, or FinanceOps/SuperAdmin").Produces<RefundResponse>();

        app.MapGet("/v1/refunds", async (ClaimsPrincipal principal, IRefundService svc, CancellationToken ct) =>
            Results.Ok((await svc.MyRefundsAsync(UserId(principal), ct)).Select(ToViewJson))
        ).WithTags("refunds").RequireAuthorization().WithSummary("The caller's own refunds, across every event").Produces<IReadOnlyList<RefundResponse>>();

        var admin = app.MapGroup("/v1/admin/refunds").WithTags("admin").RequireAuthorization("FinanceOps");

        admin.MapGet("", async (string? status, int? limit, int? page, IRefundService svc, CancellationToken ct) =>
        {
            var (items, total) = await svc.ListForAdminAsync(status, limit ?? 50, page ?? 1, ct);
            return Results.Ok(new AdminRefundPage(items.Select(ToViewJson), total));
            // Envelope ({items, total}), not a bare list — declared in Stage E with a named page type.
        }).WithSummary("Platform-wide refund list (FinanceOps/SuperAdmin)").Produces<AdminRefundPage>();
    }


    private static RefundResponse ToViewJson(RefundView v) => new(
        v.Id,
        v.OrderId,
        v.EventId,
        v.OrgId,
        v.AmountPaise,
        v.Currency,
        v.Reason,
        v.Status.ToLowerInvariant(),
        v.RazorpayRefundId,
        v.CreatedAt);

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    // SuperAdmin OR FinanceOps — the same pair the "FinanceOps" authorization policy requires (Program.cs),
    // checked inline here because this route also grants access to a matching org Owner/Finance, so the
    // whole route can't be gated by a single blanket policy the way /v1/admin/refunds is.
    private static bool IsFinanceStaff(ClaimsPrincipal principal)
        => principal.HasClaim(PlatformRoleClaimsTransformation.PlatformRoleClaim, nameof(PlatformRole.SuperAdmin))
        || principal.HasClaim(PlatformRoleClaimsTransformation.PlatformRoleClaim, nameof(PlatformRole.FinanceOps));

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
