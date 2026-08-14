using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record ReviewOrgBody(string Decision, string? ReasonCode, string? Notes);
public record SuspendOrgBody(string? Reason);
public record MergeOrgBody(Guid DuplicateOrgId, Guid CanonicalOrgId);

/// <summary>Platform reviewer surface for organization verification (M5, D-044). Gated by the live
/// VerificationReviewer platform role (M2). The full admin console (queues across all subjects,
/// merge, blacklist, appeals) is M12; this is the org-verification slice it will build on.</summary>
public static class AdminOrgEndpoints
{
    public static void MapAdminOrgEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/admin/orgs").WithTags("admin").RequireAuthorization("VerificationReviewer");

        // D-194: platform-wide org list & detail — the standing gap documented in admin/STATUS.md §5.4.
        // GetAsync/ListForAdminAsync reuse OrgService's existing methods (isAdmin bypass, same one-parameter
        // shape D-186 already established elsewhere) rather than a parallel read path.
        g.MapGet("", async (string? q, string? status, string? type, int? limit, int? page, IOrgService svc, CancellationToken ct) =>
        {
            var filter = new AdminOrgListFilter(q, status, type, limit ?? 50, page ?? 1);
            var (items, total) = await svc.ListForAdminAsync(filter, ct);
            return Results.Ok(new AdminOrgPage(items.Select(ToAdminOrgJson), total));
            // Envelope ({items, total}), not a bare list — declared in Stage E with a named page type.
        }).Produces<AdminOrgPage>();

        g.MapGet("/{orgId:guid}", async (Guid orgId, ClaimsPrincipal p, IOrgService svc, CancellationToken ct) =>
        {
            var r = await svc.GetAsync(UserId(p), orgId, isAdmin: true, ct: ct);
            return r.Ok ? Results.Ok(ToOrgDetailJson(r.Value!)) : Fail(r.Error);
        }).Produces<AdminOrgDetailResponse>();

        g.MapGet("/pending", async (int? limit, IOrgVerificationService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListPendingAsync(limit ?? 50, ct)).Select(p => new
            {
                org_id = p.OrgId, name = p.Name, slug = p.Slug, type = p.Type.ToLowerInvariant(),
                primary_domain = p.PrimaryDomain, document_count = p.DocumentCount, submitted_at = p.SubmittedAt,
            })));

        g.MapPost("/{orgId:guid}/verification/review", async (Guid orgId, ReviewOrgBody body,
            ClaimsPrincipal p, IOrgVerificationService svc, CancellationToken ct) =>
        {
            var r = await svc.ReviewAsync(UserId(p), orgId, body.Decision, body.ReasonCode, body.Notes, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).Produces<OrgVerificationResponse>();

        g.MapPost("/{orgId:guid}/verification/suspend", async (Guid orgId, SuspendOrgBody body,
            ClaimsPrincipal p, IOrgVerificationService svc, CancellationToken ct) =>
        {
            var r = await svc.SuspendAsync(UserId(p), orgId, body.Reason, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).Produces<OrgVerificationResponse>();

        g.MapPost("/{orgId:guid}/verification/blacklist", async (Guid orgId, SuspendOrgBody body,
            ClaimsPrincipal p, IOrgVerificationService svc, CancellationToken ct) =>
        {
            var r = await svc.BlacklistAsync(UserId(p), orgId, body.Reason, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).Produces<OrgVerificationResponse>();

        // Merge a fresh duplicate org into the canonical one (M12).
        g.MapPost("/merge", async (MergeOrgBody body, ClaimsPrincipal p, IOrgVerificationService svc, CancellationToken ct) =>
        {
            var r = await svc.MergeAsync(UserId(p), body.DuplicateOrgId, body.CanonicalOrgId, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).Produces<OrgVerificationResponse>();

        // View one evidence document (D-055 G2): a short-lived presigned URL, presigned on open. Works for
        // any subject (org / membership / identity) since verification_documents is polymorphic.
        app.MapGet("/v1/admin/verification-documents/{documentId:guid}/view",
            async (Guid documentId, IAdminVerificationService svc, CancellationToken ct) =>
            {
                var url = await svc.GetDocumentViewUrlAsync(documentId, ct);
                return url is null ? ProblemResults.Problem("not_found", StatusCodes.Status404NotFound) : Results.Ok(new { url });
            }).WithTags("admin").RequireAuthorization("VerificationReviewer");

        // Cross-subject verification audit trail (M12): identity / organization / membership / event.
        app.MapGet("/v1/admin/verifications/{subjectType}/{subjectId:guid}/history",
            async (string subjectType, Guid subjectId, IAdminVerificationService svc, CancellationToken ct) =>
            {
                var h = await svc.GetHistoryAsync(subjectType, subjectId, ct);
                if (h is null) return ProblemResults.Problem("invalid_subject_type", StatusCodes.Status400BadRequest);
                return Results.Ok(new
                {
                    subject_type = h.SubjectType.ToLowerInvariant(), subject_id = h.SubjectId,
                    reviews = h.Reviews.Select(r => new
                    {
                        id = r.Id, decision = r.Decision.ToLowerInvariant(), reviewer_id = r.ReviewerId,
                        reason_code = r.ReasonCode, notes = r.Notes, risk_score = r.RiskScore, created_at = r.CreatedAt,
                    }),
                    documents = h.Documents.Select(d => new
                    {
                        id = d.Id, doc_type = d.DocType, status = d.Status.ToLowerInvariant(), created_at = d.CreatedAt,
                    }),
                });
            }).WithTags("admin").RequireAuthorization("VerificationReviewer");
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "duplicate_verified_org" or "already_merged" or "cannot_merge_has_events"
            or "cannot_merge_has_ledger" or "cannot_merge_has_funds"
            => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static OrgVerificationResponse ToJson(OrgVerificationView v) => new(
        v.OrgId, v.Name, v.Slug, v.Status.ToLowerInvariant(), v.ReviewedAt, v.Notes,
        v.Documents.Select(d => new VerificationDocumentResponse(
            d.Id, d.DocType, d.StorageKey, d.Status.ToLowerInvariant(), d.CreatedAt)));

    private static AdminOrgResponse ToAdminOrgJson(AdminOrgView v) => new(
        v.OrgId,
        v.Name,
        v.Slug,
        v.LogoKey,
        v.Type.ToLowerInvariant(),
        v.VerificationStatus.ToLowerInvariant(),
        v.PrimaryDomain,
        v.IsPersonal,
        v.MemberCount,
        v.EventCount,
        v.CreatedAt);

    private static AdminOrgDetailResponse ToOrgDetailJson(OrgDetail d) => new(
        d.Id,
        d.Name,
        d.Slug,
        d.LogoKey,
        d.Bio,
        d.LinksJson,
        d.PayoutAccountStatus.ToLowerInvariant(),
        d.BankLast4,
        d.Tier,
        d.Type.ToLowerInvariant(),
        d.PrimaryDomain,
        d.VerificationStatus.ToLowerInvariant());
}
