using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Orgs;

/// <summary>Membership-affiliation verification (M6, D-045). A user submits an evidence-backed claim to
/// represent an org; a platform VerificationReviewer approves/rejects. Reviewed only from the claim +
/// its <c>verification_documents</c> — never from profile bio. On approval the user's operational
/// membership is marked verified (a read-only Staff seat is created if none); organizer elevation stays
/// an explicit org-Owner act (D-015). Every decision writes a <c>verification_reviews</c> row.</summary>
public class MembershipVerificationService(KurxDbContext db,
    Providers.UploadScanGate scanGate) : IMembershipVerificationService
{
    public const int MaxEvidenceDocs = 25;

    public async Task<ServiceResult<MembershipClaimView>> SubmitAsync(Guid userId, Guid orgId, string claimedRole,
        DateTime? validUntil, IReadOnlyList<MembershipEvidence> evidence, CancellationToken ct = default)
    {
        if (!Enum.TryParse<MembershipClaimRole>(claimedRole, ignoreCase: true, out var role))
            return Fail("invalid_role");

        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId && o.DeletedAt == null, ct);
        if (org is null) return Fail("not_found");

        if (evidence.Count is 0 or > MaxEvidenceDocs) return Fail("invalid_evidence");
        foreach (var e in evidence)
            if (string.IsNullOrWhiteSpace(e.DocType) || string.IsNullOrWhiteSpace(e.StorageKey))
                return Fail("invalid_evidence");

        // D-338 — reviewer-facing evidence, scanned before it is persisted. Ahead of the duplicate-claim
        // check on purpose: an infected upload is refused even when the claim would have been rejected
        // anyway, so the bytes never linger in storage waiting for a cleanup sweep that does not exist.
        if (await scanGate.RejectAnyAsync(evidence.Select(e => e.StorageKey), userId, "membership_claims", orgId, ct)
            is { } scanError)
            return Fail(scanError);

        // One active claim per (user, org) at a time.
        var hasActive = await db.MembershipClaims.AnyAsync(c => c.UserId == userId && c.OrgId == orgId
            && (c.Status == MembershipClaimStatus.Submitted || c.Status == MembershipClaimStatus.UnderReview
                || c.Status == MembershipClaimStatus.OfficialContactVerification || c.Status == MembershipClaimStatus.Approved), ct);
        if (hasActive) return Fail("already_claimed");

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var claim = new MembershipClaim
        {
            UserId = userId,
            OrgId = orgId,
            ClaimedRole = role,
            ValidUntil = validUntil,
            // Lower-friction review hint (still reviewed): the user's email domain matches the org's.
            // Bio is never a signal; email verification (ID1) strengthens this but isn't required here.
            FastTrack = DomainOf(user.Email) is { } d && d == org.PrimaryDomain,
        };
        db.MembershipClaims.Add(claim);

        foreach (var e in evidence)
            db.VerificationDocuments.Add(new VerificationDocument
            {
                SubjectType = VerificationSubjectType.Membership,
                SubjectId = claim.Id,
                DocType = e.DocType.Trim().ToLowerInvariant(),
                StorageKey = e.StorageKey.Trim(),
                UploadedBy = userId,
            });

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId,
            Action = "membership.claim.submit", Entity = "membership_claims", EntityId = claim.Id,
        });
        await db.SaveChangesAsync(ct);
        return ServiceResult<MembershipClaimView>.Success(ToView(claim, org.Name, org.Slug));
    }

    public async Task<IReadOnlyList<MembershipClaimView>> ListMineAsync(Guid userId, CancellationToken ct = default)
        => await db.MembershipClaims.AsNoTracking()
            .Where(c => c.UserId == userId)
            .Join(db.Organizations, c => c.OrgId, o => o.Id, (c, o) => new { c, o.Name, o.Slug })
            .OrderByDescending(x => x.c.CreatedAt)
            .Select(x => ToView(x.c, x.Name, x.Slug))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PendingClaimView>> ListPendingAsync(int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        return await db.MembershipClaims.AsNoTracking()
            .Where(c => c.Status == MembershipClaimStatus.Submitted || c.Status == MembershipClaimStatus.UnderReview
                     || c.Status == MembershipClaimStatus.OfficialContactVerification)
            .Join(db.Users, c => c.UserId, u => u.Id, (c, u) => new { c, u.Name, u.Username })
            .Join(db.Organizations, x => x.c.OrgId, o => o.Id, (x, o) => new { x.c, x.Name, x.Username, OrgName = o.Name })
            // FastTrack (domain-matched) claims surface first, then oldest.
            .OrderByDescending(x => x.c.FastTrack).ThenBy(x => x.c.CreatedAt)
            .Take(limit)
            .Select(x => new PendingClaimView(x.c.Id, x.c.UserId, x.Name, x.Username, x.c.OrgId, x.OrgName,
                x.c.ClaimedRole.ToString(), x.c.Status.ToString(), x.c.FastTrack, x.c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<MembershipClaimView>> ReviewAsync(Guid reviewerId, Guid claimId, string decision,
        string? reasonCode, string? notes, CancellationToken ct = default)
    {
        var claim = await db.MembershipClaims.FirstOrDefaultAsync(c => c.Id == claimId, ct);
        if (claim is null) return Fail("not_found");
        if (claim.Status is not (MembershipClaimStatus.Submitted or MembershipClaimStatus.UnderReview
            or MembershipClaimStatus.OfficialContactVerification))
            return Fail("not_pending");

        VerificationDecision reviewDecision;
        switch (decision.Trim().ToLowerInvariant())
        {
            case "approve":
                claim.Status = MembershipClaimStatus.Approved;
                reviewDecision = VerificationDecision.Approve;
                // Mark the user's operational membership verified — creating a read-only Staff seat if
                // none. Organizer rights (Manager/Owner) remain an explicit org-Owner act (D-015).
                var membership = await db.Memberships.FirstOrDefaultAsync(m => m.OrgId == claim.OrgId && m.UserId == claim.UserId, ct);
                if (membership is null)
                {
                    membership = new Membership { OrgId = claim.OrgId, UserId = claim.UserId, Role = OrgRole.Staff };
                    db.Memberships.Add(membership);
                }
                membership.IsVerified = true;
                membership.VerifiedAt = DateTime.UtcNow;
                membership.SourceClaimId = claim.Id;
                membership.ValidUntil = claim.ValidUntil;
                // D-304 case 8. Staged even though this grants Staff and therefore resolves to Member: the
                // producer does not decide the outcome, the handler does. `IsVerified` is also what
                // `CanRepresentOrg` reads, so an approval can change what an existing Representative seat is
                // worth — deciding here that "verification is not authority, so skip it" would be a producer
                // predicting a resolution it does not own, which is how case 1 went missing.
                OrgAuthorityOutbox.Stage(db, claim.OrgId, claim.UserId);
                break;
            case "reject":
                claim.Status = MembershipClaimStatus.Rejected;
                reviewDecision = VerificationDecision.Reject;
                break;
            default:
                return Fail("invalid_decision");
        }

        claim.ReviewedBy = reviewerId;
        claim.ReviewedAt = DateTime.UtcNow;
        claim.Notes = notes;
        claim.UpdatedAt = DateTime.UtcNow;

        db.VerificationReviews.Add(new VerificationReview
        {
            SubjectType = VerificationSubjectType.Membership,
            SubjectId = claim.Id,
            Decision = reviewDecision,
            ReviewerId = reviewerId,
            ReasonCode = reasonCode,
            Notes = notes,
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "admin", ActorId = reviewerId,
            Action = $"membership.claim.{decision.Trim().ToLowerInvariant()}", Entity = "membership_claims", EntityId = claim.Id,
        });
        await db.SaveChangesAsync(ct);

        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == claim.OrgId, ct);
        return ServiceResult<MembershipClaimView>.Success(ToView(claim, org.Name, org.Slug));
    }

    private static MembershipClaimView ToView(MembershipClaim c, string orgName, string orgSlug) => new(
        c.Id, c.OrgId, orgName, orgSlug, c.ClaimedRole.ToString(), c.Status.ToString(),
        c.FastTrack, c.ValidUntil, c.ReviewedAt, c.Notes, c.CreatedAt);

    private static string? DomainOf(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.LastIndexOf('@');
        return at >= 0 && at < email.Length - 1 ? email[(at + 1)..].Trim().ToLowerInvariant() : null;
    }

    private static ServiceResult<MembershipClaimView> Fail(string error) => ServiceResult<MembershipClaimView>.Fail(error);
}
