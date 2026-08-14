using System.Security.Claims;
using Kurx.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record ApplyReferralBody(string ReferralCode);

public static class GamificationEndpoints
{
    public static void MapGamificationEndpoints(this WebApplication app)
    {
        var gamification = app.MapGroup("/v1").WithTags("gamification").RequireAuthorization();

        gamification.MapGet("/me/points", async (ClaimsPrincipal principal, IGamificationService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetPointsSummaryAsync(UserId(principal), ct);
            return Results.Ok(summary);
        }).Produces<PointsSummary>();

        gamification.MapGet("/me/badges", async (ClaimsPrincipal principal, IGamificationService svc, CancellationToken ct) =>
        {
            var badges = await svc.GetUserBadgesAsync(UserId(principal), ct);
            return Results.Ok(badges);
        }).Produces<IReadOnlyList<BadgeView>>();

        gamification.MapGet("/leaderboards", async (IGamificationService svc, [FromQuery] int? limit, CancellationToken ct) =>
        {
            var board = await svc.GetGlobalLeaderboardAsync(limit ?? 50, ct);
            return Results.Ok(board);
        }).Produces<IReadOnlyList<LeaderboardEntryView>>();

        gamification.MapGet("/leaderboards/events/{eventId:guid}", async (Guid eventId, IGamificationService svc, [FromQuery] int? limit, CancellationToken ct) =>
        {
            var board = await svc.GetEventLeaderboardAsync(eventId, limit ?? 50, ct);
            return Results.Ok(board);
        }).Produces<IReadOnlyList<LeaderboardEntryView>>();

        gamification.MapGet("/leaderboards/organizations", async (IGamificationService svc, [FromQuery] int? limit, CancellationToken ct) =>
        {
            var board = await svc.GetOrgLeaderboardAsync(limit ?? 50, ct);
            return Results.Ok(board);
        }).Produces<IReadOnlyList<OrgLeaderboardEntryView>>();

        gamification.MapPost("/referrals/apply", async (ApplyReferralBody body, ClaimsPrincipal principal, IGamificationService svc, CancellationToken ct) =>
        {
            var result = await svc.ApplyReferralCodeAsync(UserId(principal), body.ReferralCode, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).WithValidation<ApplyReferralBody>().Produces<OperationAck>();

        gamification.MapGet("/referrals", async (ClaimsPrincipal principal, IGamificationService svc, CancellationToken ct) =>
        {
            var referrals = await svc.GetReferralsAsync(UserId(principal), ct);
            return Results.Ok(referrals);
        }).Produces<IReadOnlyList<ReferralView>>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        "invalid_referral_code" => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
        "cannot_refer_self" => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
        "referral_already_applied" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
