using System.Security.Claims;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

/// <summary>Admin dashboard summary (D-058): the live counts the console landing page shows. Available to
/// any platform staff (not reviewer-only) — the numbers are non-PII aggregates and every staff role lands
/// here. Counts are computed live with cheap COUNTs; reuses the exact "pending" definitions of the
/// verification/claim/event queues so the tiles never disagree with the queues they link to.</summary>
public static class AdminDashboardEndpoints
{
    public static void MapAdminDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/admin/dashboard/summary", async (ClaimsPrincipal principal, KurxDbContext db,
            IPlatformRoleService roles, CancellationToken ct) =>
        {
            var userId = UserId(principal);
            if ((await roles.GetRolesAsync(userId, ct)).Count == 0)
                return ProblemResults.Problem("forbidden", StatusCodes.Status403Forbidden);

            var now = DateTime.UtcNow;
            var dayAgo = now.AddDays(-1);

            return Results.Ok(new AdminDashboardSummary(
                await db.Organizations.CountAsync(
                    o => o.VerificationStatus == OrgVerificationStatus.PendingReview && o.DeletedAt == null, ct),
                await db.MembershipClaims.CountAsync(
                    c => c.Status == MembershipClaimStatus.Submitted
                      || c.Status == MembershipClaimStatus.UnderReview
                      || c.Status == MembershipClaimStatus.OfficialContactVerification, ct),
                // D-266 M4: both review states, matching ListInReviewAsync exactly — counting only
                // PendingReview would hide every event a reviewer has already claimed.
                await db.Events.CountAsync(
                    e => e.Status == EventStatus.PendingReview || e.Status == EventStatus.UnderReview, ct),
                await db.BlacklistEntries.CountAsync(ct),
                await db.PlatformRoles
                    .Where(r => r.ExpiresAt == null || r.ExpiresAt > now)
                    .Select(r => r.UserId).Distinct().CountAsync(ct),
                await db.Users.CountAsync(u => u.CreatedAt >= dayAgo, ct),
                await db.Users.CountAsync(ct),
                await db.Organizations.CountAsync(o => o.DeletedAt == null, ct),
                await db.Events.CountAsync(ct)));
        }).RequireAuthorization().WithTags("admin").Produces<AdminDashboardSummary>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
