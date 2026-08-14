namespace Kurx.Application.Abstractions;

public record OrgVerificationEvidence(string DocType, string StorageKey);

public record OrgVerificationDocView(Guid Id, string DocType, string StorageKey, string Status, DateTime CreatedAt);

public record OrgVerificationView(
    Guid OrgId, string Name, string Slug, string Status, DateTime? ReviewedAt, string? Notes,
    IReadOnlyList<OrgVerificationDocView> Documents);

public record OrgPendingView(
    Guid OrgId, string Name, string Slug, string Type, string? PrimaryDomain, int DocumentCount, DateTime? SubmittedAt);

/// <summary>Organization verification lifecycle (M5, D-044). Owner submits evidence → PendingReview;
/// a platform VerificationReviewer (M2) approves/rejects/requests-changes/suspends. Approval reserves
/// the org's normalized name (hard dedup against other verified orgs). Every decision is audited via
/// <c>verification_reviews</c>. Merge of a duplicate into the canonical org is the admin console (M12).</summary>
public interface IOrgVerificationService
{
    /// <summary>Owner submits the org for verification with evidence documents.</summary>
    Task<ServiceResult<OrgVerificationView>> SubmitAsync(Guid userId, Guid orgId,
        IReadOnlyList<OrgVerificationEvidence> evidence, CancellationToken ct = default);

    /// <summary>Any org member views their org's verification status + evidence.</summary>
    Task<ServiceResult<OrgVerificationView>> GetAsync(Guid userId, Guid orgId, CancellationToken ct = default);

    /// <summary>Reviewer queue: orgs awaiting review (platform VerificationReviewer only — endpoint-gated).</summary>
    Task<IReadOnlyList<OrgPendingView>> ListPendingAsync(int limit = 50, CancellationToken ct = default);

    /// <summary>Reviewer decision: <c>approve</c> | <c>reject</c> | <c>request_changes</c>.</summary>
    Task<ServiceResult<OrgVerificationView>> ReviewAsync(Guid reviewerId, Guid orgId, string decision,
        string? reasonCode, string? notes, CancellationToken ct = default);

    /// <summary>Reviewer suspends a currently-verified org.</summary>
    Task<ServiceResult<OrgVerificationView>> SuspendAsync(Guid reviewerId, Guid orgId, string? reason, CancellationToken ct = default);

    /// <summary>Reviewer blacklists an org (impersonation/fraud) — a terminal block (M12).</summary>
    Task<ServiceResult<OrgVerificationView>> BlacklistAsync(Guid reviewerId, Guid orgId, string? reason, CancellationToken ct = default);

    /// <summary>Merge a FRESH duplicate org (no events, no financial history) into the canonical one
    /// (M12): repoints memberships/claims/aliases, records the duplicate's name as an alias of the
    /// canonical org, and soft-deletes the duplicate pointing its CanonicalOrgId at the survivor.</summary>
    Task<ServiceResult<OrgVerificationView>> MergeAsync(Guid reviewerId, Guid duplicateOrgId, Guid canonicalOrgId, CancellationToken ct = default);
}
