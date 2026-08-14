namespace Kurx.Application.Abstractions;

public record MembershipEvidence(string DocType, string StorageKey);

public record MembershipClaimView(
    Guid Id, Guid OrgId, string OrgName, string OrgSlug, string ClaimedRole, string Status,
    bool FastTrack, DateTime? ValidUntil, DateTime? ReviewedAt, string? Notes, DateTime CreatedAt);

public record PendingClaimView(
    Guid Id, Guid UserId, string UserName, string? Username, Guid OrgId, string OrgName,
    string ClaimedRole, string Status, bool FastTrack, DateTime CreatedAt);

/// <summary>Membership-affiliation verification (M6, D-045): a user's evidence-backed CLAIM to represent
/// an org, reviewed independently of profile bio. On approval it marks the user's operational membership
/// as verified (creating a read-only Staff seat if none); organizer rights stay an explicit org-Owner act.</summary>
public interface IMembershipVerificationService
{
    Task<ServiceResult<MembershipClaimView>> SubmitAsync(Guid userId, Guid orgId, string claimedRole,
        DateTime? validUntil, IReadOnlyList<MembershipEvidence> evidence, CancellationToken ct = default);

    Task<IReadOnlyList<MembershipClaimView>> ListMineAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Reviewer queue (platform VerificationReviewer only — endpoint-gated).</summary>
    Task<IReadOnlyList<PendingClaimView>> ListPendingAsync(int limit = 50, CancellationToken ct = default);

    /// <summary>Reviewer decision: <c>approve</c> | <c>reject</c>.</summary>
    Task<ServiceResult<MembershipClaimView>> ReviewAsync(Guid reviewerId, Guid claimId, string decision,
        string? reasonCode, string? notes, CancellationToken ct = default);
}
