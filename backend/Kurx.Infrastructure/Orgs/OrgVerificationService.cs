using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Orgs;

/// <summary>Organization verification lifecycle (M5, D-044). Submit → PendingReview → reviewer decision.
/// Approval enforces the hard name-dedup M4 deferred here (a second org can't be *verified* under a
/// name/normalized-name already held by a verified org — the reviewer merges instead, M12). Every
/// decision writes a <c>verification_reviews</c> row; evidence lives in <c>verification_documents</c>.</summary>
public class OrgVerificationService(KurxDbContext db, IAuditWriter audit,
    Providers.UploadScanGate scanGate) : IOrgVerificationService
{
    public const int MaxEvidenceDocs = 25;

    /// <summary>D-101 (M7): how many genuine rejections an org may accrue before resubmission is blocked
    /// and an admin must intervene.</summary>
    public const int MaxResubmissions = 2;

    public async Task<ServiceResult<OrgVerificationView>> SubmitAsync(Guid userId, Guid orgId,
        IReadOnlyList<OrgVerificationEvidence> evidence, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId && o.DeletedAt == null, ct);
        if (org is null) return Fail("not_found");
        // An Owner or a Representative may submit; a non-member gets not_found (never leak existence, D-018).
        // D-101 (M7) fixes a D-075 regression: institutions created event-first carry a Representative and no
        // Owner, so the old Owner-only guard locked their submitter out of resubmitting after ChangesRequested.
        var role = await RoleAsync(userId, orgId, ct);
        if (role is null) return Fail("not_found");
        if (role is not (OrgRole.Owner or OrgRole.Representative)) return Fail("forbidden");

        if (org.VerificationStatus is OrgVerificationStatus.PendingReview)
            return Fail("already_pending");
        if (org.VerificationStatus is OrgVerificationStatus.Verified)
            return Fail("already_verified");
        // D-101: a suspended org must not re-enter review by self-submitting — escaping a suspension is an
        // admin decision, not a resubmission.
        if (org.VerificationStatus is OrgVerificationStatus.Blacklisted or OrgVerificationStatus.Suspended)
            return Fail("forbidden");

        // D-101: cap genuine rejections; past the limit an admin must intervene. Suspend/blacklist/merge also
        // write a Reject decision, so they are excluded by reason code and don't consume the allowance.
        var rejections = await db.VerificationReviews.CountAsync(r =>
            r.SubjectType == VerificationSubjectType.Organization && r.SubjectId == orgId
            && r.Decision == VerificationDecision.Reject
            && (r.ReasonCode == null || (r.ReasonCode != "suspended" && r.ReasonCode != "blacklisted" && r.ReasonCode != "merged")), ct);
        if (rejections >= MaxResubmissions) return Fail("resubmit_limit_reached");

        if (evidence.Count is 0 or > MaxEvidenceDocs) return Fail("invalid_evidence");
        foreach (var e in evidence)
            if (string.IsNullOrWhiteSpace(e.DocType) || string.IsNullOrWhiteSpace(e.StorageKey))
                return Fail("invalid_evidence");

        // D-338 — this evidence is what a VerificationReviewer downloads and opens in the admin console,
        // so it is scanned before it is persisted, not after a reviewer has already been handed it.
        if (await scanGate.RejectAnyAsync(evidence.Select(e => e.StorageKey), userId, "organizations", orgId, ct)
            is { } scanError)
            return Fail(scanError);

        foreach (var e in evidence)
            db.VerificationDocuments.Add(new VerificationDocument
            {
                SubjectType = VerificationSubjectType.Organization,
                SubjectId = orgId,
                DocType = e.DocType.Trim().ToLowerInvariant(),
                StorageKey = e.StorageKey.Trim(),
                UploadedBy = userId,
            });

        org.VerificationStatus = OrgVerificationStatus.PendingReview;
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId,
            Action = "org.verification.submit", Entity = "organizations", EntityId = orgId,
        });
        await db.SaveChangesAsync(ct);
        return await ViewAsync(org, ct);
    }

    public async Task<ServiceResult<OrgVerificationView>> GetAsync(Guid userId, Guid orgId, CancellationToken ct = default)
    {
        if (await RoleAsync(userId, orgId, ct) is null) return Fail("not_found");
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId && o.DeletedAt == null, ct);
        if (org is null) return Fail("not_found");
        return await ViewAsync(org, ct);
    }

    public async Task<IReadOnlyList<OrgPendingView>> ListPendingAsync(int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var pending = await db.Organizations.AsNoTracking()
            .Where(o => o.VerificationStatus == OrgVerificationStatus.PendingReview && o.DeletedAt == null)
            .Select(o => new { o.Id, o.Name, o.Slug, o.Type, o.PrimaryDomain })
            .Take(limit)
            .ToListAsync(ct);

        var ids = pending.Select(p => p.Id).ToList();
        var docStats = await db.VerificationDocuments.AsNoTracking()
            .Where(d => d.SubjectType == VerificationSubjectType.Organization && ids.Contains(d.SubjectId))
            .GroupBy(d => d.SubjectId)
            .Select(g => new { OrgId = g.Key, Count = g.Count(), Submitted = g.Max(d => d.CreatedAt) })
            .ToListAsync(ct);
        var byOrg = docStats.ToDictionary(s => s.OrgId);

        return pending
            .Select(p => new OrgPendingView(p.Id, p.Name, p.Slug, p.Type.ToString(), p.PrimaryDomain,
                byOrg.TryGetValue(p.Id, out var s) ? s.Count : 0,
                byOrg.TryGetValue(p.Id, out var s2) ? s2.Submitted : null))
            .OrderBy(v => v.SubmittedAt)
            .ToList();
    }

    public async Task<ServiceResult<OrgVerificationView>> ReviewAsync(Guid reviewerId, Guid orgId, string decision,
        string? reasonCode, string? notes, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId && o.DeletedAt == null, ct);
        if (org is null) return Fail("not_found");
        if (org.VerificationStatus is not (OrgVerificationStatus.PendingReview or OrgVerificationStatus.ChangesRequested))
            return Fail("not_pending");
        var previousStatus = org.VerificationStatus;

        VerificationDecision reviewDecision;
        switch (decision.Trim().ToLowerInvariant())
        {
            case "approve":
                // Hard name-dedup deferred from M4: can't verify a second org under a name already
                // held by a verified org — the reviewer must merge the duplicate instead (M12).
                if (await db.Organizations.AnyAsync(o => o.Id != orgId && o.DeletedAt == null
                        && o.VerificationStatus == OrgVerificationStatus.Verified
                        && o.NormalizedName == org.NormalizedName, ct))
                    return Fail("duplicate_verified_org");
                // Hard domain-dedup at verification too (D-075): two staged requests can carry the same domain
                // before either is verified (submit only dedups against *verified* orgs), so re-check here to
                // keep the registry's domains globally unique (D-043).
                if (org.PrimaryDomain is not null && await db.Organizations.AnyAsync(o => o.Id != orgId && o.DeletedAt == null
                        && o.VerificationStatus == OrgVerificationStatus.Verified
                        && o.PrimaryDomain == org.PrimaryDomain, ct))
                    return Fail("duplicate_verified_org");
                org.VerificationStatus = OrgVerificationStatus.Verified;
                reviewDecision = VerificationDecision.Approve;
                await MaterializeOnApprovalAsync(org, ct);
                break;
            case "reject":
                org.VerificationStatus = OrgVerificationStatus.Rejected;
                reviewDecision = VerificationDecision.Reject;
                break;
            case "request_changes":
                org.VerificationStatus = OrgVerificationStatus.ChangesRequested;
                reviewDecision = VerificationDecision.RequestChanges;
                break;
            default:
                return Fail("invalid_decision");
        }

        org.VerificationReviewedBy = reviewerId;
        org.VerificationReviewedAt = DateTime.UtcNow;
        org.VerificationNotes = notes;
        WriteReview(orgId, reviewDecision, reviewerId, reasonCode, notes);
        // D-102 (M3a): typed audit spine — reason codes are audit data, never PII.
        audit.Write(new AuditEvent($"org.verification.{decision.Trim().ToLowerInvariant()}", "organizations", orgId,
            ActorType: "admin",
            ActorId: reviewerId,
            Before: new { status = previousStatus.ToString() },
            After: new { status = org.VerificationStatus.ToString(), reason_code = reasonCode }));
        await db.SaveChangesAsync(ct);
        return await ViewAsync(org, ct);
    }

    public async Task<ServiceResult<OrgVerificationView>> SuspendAsync(Guid reviewerId, Guid orgId, string? reason,
        CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId && o.DeletedAt == null, ct);
        if (org is null) return Fail("not_found");
        if (org.VerificationStatus != OrgVerificationStatus.Verified) return Fail("not_verified");

        org.VerificationStatus = OrgVerificationStatus.Suspended;
        org.VerificationReviewedBy = reviewerId;
        org.VerificationReviewedAt = DateTime.UtcNow;
        org.VerificationNotes = reason;
        WriteReview(orgId, VerificationDecision.Reject, reviewerId, reasonCode: "suspended", notes: reason);
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "admin", ActorId = reviewerId,
            Action = "org.verification.suspend", Entity = "organizations", EntityId = orgId,
        });
        await db.SaveChangesAsync(ct);
        return await ViewAsync(org, ct);
    }

    public async Task<ServiceResult<OrgVerificationView>> BlacklistAsync(Guid reviewerId, Guid orgId, string? reason,
        CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId && o.DeletedAt == null, ct);
        if (org is null) return Fail("not_found");

        org.VerificationStatus = OrgVerificationStatus.Blacklisted;
        org.VerificationReviewedBy = reviewerId;
        org.VerificationReviewedAt = DateTime.UtcNow;
        org.VerificationNotes = reason;
        WriteReview(orgId, VerificationDecision.Reject, reviewerId, reasonCode: "blacklisted", notes: reason);
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "admin", ActorId = reviewerId,
            Action = "org.verification.blacklist", Entity = "organizations", EntityId = orgId,
        });
        await db.SaveChangesAsync(ct);
        return await ViewAsync(org, ct);
    }

    public async Task<ServiceResult<OrgVerificationView>> MergeAsync(Guid reviewerId, Guid duplicateOrgId,
        Guid canonicalOrgId, CancellationToken ct = default)
    {
        if (duplicateOrgId == canonicalOrgId) return Fail("invalid_merge");
        var dup = await db.Organizations.FirstOrDefaultAsync(o => o.Id == duplicateOrgId && o.DeletedAt == null, ct);
        var canonical = await db.Organizations.FirstOrDefaultAsync(o => o.Id == canonicalOrgId && o.DeletedAt == null, ct);
        if (dup is null || canonical is null) return Fail("not_found");
        if (dup.CanonicalOrgId != dup.Id) return Fail("already_merged");

        // Only a FRESH duplicate can be merged automatically — anything with events or money needs
        // manual ops (its history references the org id and can't be silently repointed).
        if (await db.Events.AnyAsync(e => e.RepresentingOrgId == duplicateOrgId, ct)) return Fail("cannot_merge_has_events");
        if (await db.LedgerEntries.AnyAsync(l => l.OrgId == duplicateOrgId, ct)) return Fail("cannot_merge_has_ledger");
        var wallet = await db.OrganizationWallets.AsNoTracking().FirstOrDefaultAsync(w => w.OrgId == duplicateOrgId, ct);
        if (wallet is not null && (wallet.CollectedPaise > 0 || wallet.AvailablePaise > 0 || wallet.LifetimeEarnedPaise > 0))
            return Fail("cannot_merge_has_funds");

        // Repoint memberships (drop a duplicate seat if the user is already in the canonical org).
        var canonicalUserIds = await db.Memberships.Where(m => m.OrgId == canonicalOrgId).Select(m => m.UserId).ToListAsync(ct);
        var dupMembers = await db.Memberships.Where(m => m.OrgId == duplicateOrgId).ToListAsync(ct);
        foreach (var m in dupMembers)
        {
            if (canonicalUserIds.Contains(m.UserId)) db.Memberships.Remove(m);
            else m.OrgId = canonicalOrgId;
        }

        // Repoint membership claims and aliases (skip aliases the canonical org already holds).
        foreach (var c in await db.MembershipClaims.Where(c => c.OrgId == duplicateOrgId).ToListAsync(ct))
            c.OrgId = canonicalOrgId;
        var canonicalAliases = await db.OrganizationAliases.Where(a => a.OrgId == canonicalOrgId)
            .Select(a => a.NormalizedAlias).ToListAsync(ct);
        foreach (var a in await db.OrganizationAliases.Where(a => a.OrgId == duplicateOrgId).ToListAsync(ct))
        {
            if (canonicalAliases.Contains(a.NormalizedAlias)) db.OrganizationAliases.Remove(a);
            else a.OrgId = canonicalOrgId;
        }

        // Record the duplicate's own name as an alias so it still resolves to the canonical org.
        var dupNorm = OrganizationRegistryService.Normalize(dup.Name);
        if (dupNorm != canonical.NormalizedName && !canonicalAliases.Contains(dupNorm))
            db.OrganizationAliases.Add(new OrganizationAlias
            {
                OrgId = canonicalOrgId, Alias = dup.Name, NormalizedAlias = dupNorm, Source = AliasSource.Import,
            });

        dup.CanonicalOrgId = canonicalOrgId;
        dup.DeletedAt = DateTime.UtcNow;
        WriteReview(duplicateOrgId, VerificationDecision.Reject, reviewerId, "merged", $"merged into {canonicalOrgId}");
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "admin", ActorId = reviewerId,
            Action = "org.merge", Entity = "organizations", EntityId = duplicateOrgId,
            DetailsJson = $"{{\"into\":\"{canonicalOrgId}\"}}",
        });
        await db.SaveChangesAsync(ct);
        return await ViewAsync(canonical, ct);
    }

    private void WriteReview(Guid orgId, VerificationDecision decision, Guid reviewerId, string? reasonCode, string? notes)
        => db.VerificationReviews.Add(new VerificationReview
        {
            SubjectType = VerificationSubjectType.Organization,
            SubjectId = orgId,
            Decision = decision,
            ReviewerId = reviewerId,
            ReasonCode = reasonCode,
            Notes = notes,
        });

    // D-075: approving an org into the registry turns its *pending* Representative(s) into verified reps and
    // provisions the financial rows an org needs once it can accept money. No-op for legacy Owner-submitted
    // orgs — they have no pending Representative and their wallet/payout schedule already exist from creation.
    private async Task MaterializeOnApprovalAsync(Organization org, CancellationToken ct)
    {
        var pendingReps = await db.Memberships
            .Where(m => m.OrgId == org.Id && m.Role == OrgRole.Representative && !m.IsVerified)
            .ToListAsync(ct);
        foreach (var m in pendingReps) { m.IsVerified = true; m.VerifiedAt = DateTime.UtcNow; }

        if (!await db.OrganizationWallets.AnyAsync(w => w.OrgId == org.Id, ct))
            db.OrganizationWallets.Add(new OrganizationWallet { OrgId = org.Id, Currency = org.SettlementCurrency });

        if (!await db.PayoutSchedules.AnyAsync(p => p.OrgId == org.Id, ct))
            db.PayoutSchedules.Add(new PayoutSchedule
            {
                OrgId = org.Id,
                Currency = org.SettlementCurrency,   // V3 §9.1
                Tier = 1,
                AdvancePct = OrgService.Tier1AdvancePct,
                CapPaise = OrgService.Tier1CapPaise,
                ReservePct = OrgService.Tier1ReservePct,
            });
    }

    private async Task<ServiceResult<OrgVerificationView>> ViewAsync(Organization org, CancellationToken ct)
    {
        var docs = await db.VerificationDocuments.AsNoTracking()
            .Where(d => d.SubjectType == VerificationSubjectType.Organization && d.SubjectId == org.Id)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new OrgVerificationDocView(d.Id, d.DocType, d.StorageKey, d.Status.ToString(), d.CreatedAt))
            .ToListAsync(ct);
        return ServiceResult<OrgVerificationView>.Success(new OrgVerificationView(
            org.Id, org.Name, org.Slug, org.VerificationStatus.ToString(),
            org.VerificationReviewedAt, org.VerificationNotes, docs));
    }

    private async Task<OrgRole?> RoleAsync(Guid userId, Guid orgId, CancellationToken ct)
        => await db.Memberships.AsNoTracking()
            .Where(m => m.OrgId == orgId && m.UserId == userId)
            .Select(m => (OrgRole?)m.Role)
            .FirstOrDefaultAsync(ct);

    private static ServiceResult<OrgVerificationView> Fail(string error) => ServiceResult<OrgVerificationView>.Fail(error);
}
