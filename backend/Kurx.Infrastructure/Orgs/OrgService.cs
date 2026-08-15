using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Orgs;

/// <summary>
/// Orgs, memberships, KYC. Role matrix: D-015. Payout activation via penny drop
/// + Route linked account: D-016. T1 schedule defaults: D-007/D-016.
/// </summary>
public partial class OrgService(KurxDbContext db, IKycProvider kyc, IRouteClient route, IFraudService fraud,
    Providers.UploadScanGate scanGate, Configuration.IdentityVerificationOptions identityOptions,
    ILogger<OrgService> log)
    : IOrgService
{
    public const int Tier1AdvancePct = 50;
    public const long Tier1CapPaise = 5_000_000;    // ₹50,000 (D-007)
    public const int Tier1ReservePct = 12;

    /// <summary>D-304 — staged on the same unit of work as the membership change. See
    /// <see cref="OrgAuthorityOutbox"/> for why this is an outbox row and not an inline chat call.</summary>
    private void EnqueueAuthorityChanged(Guid orgId, Guid userId) => OrgAuthorityOutbox.Stage(db, orgId, userId);

    public async Task<ServiceResult<OrgDetail>> CreateAsync(Guid userId, string name, OrganizationType type,
        string? legalName, string? primaryDomain, string? bio, string? linksJson, bool isPersonal = false, CancellationToken ct = default)
    {
        name = name.Trim();
        if (name.Length is < 2 or > 120)
            return ServiceResult<OrgDetail>.Fail("invalid_name");

        // A blacklisted org name (impersonation/fraud, M13) can't be (re)created.
        if (await fraud.IsBlacklistedAsync(nameof(BlacklistKind.OrgName), name, ct))
            return ServiceResult<OrgDetail>.Fail("org_blacklisted");

        legalName = string.IsNullOrWhiteSpace(legalName) ? null : legalName.Trim();
        primaryDomain = NormalizeDomain(primaryDomain);
        if (primaryDomain is not null && !DomainRegex().IsMatch(primaryDomain))
            return ServiceResult<OrgDetail>.Fail("invalid_domain");

        // Hard dedup only on primary domain (a globally-unique identifier). Name dedup is a soft
        // "did you mean?" surfaced by registry search; hard name-dedup is enforced at verification
        // time (M5), so unverified same-named orgs may coexist until a reviewer picks the canonical one.
        if (primaryDomain is not null &&
            await db.Organizations.AnyAsync(o => o.PrimaryDomain == primaryDomain && o.DeletedAt == null, ct))
            return ServiceResult<OrgDetail>.Fail("organization_domain_taken");

        var org = new Organization
        {
            Name = name,
            Slug = await UniqueSlugAsync(name, ct),
            Type = type,
            LegalName = legalName,
            PrimaryDomain = primaryDomain,
            NormalizedName = OrganizationRegistryService.Normalize(name),
            Bio = bio,
            LinksJson = linksJson,
            IsPersonal = isPersonal,
        };
        org.CanonicalOrgId = org.Id;                    // a new org is its own canonical
        db.Organizations.Add(org);
        db.Memberships.Add(new Membership { OrgId = org.Id, UserId = userId, Role = OrgRole.Owner });
        db.PayoutSchedules.Add(new PayoutSchedule
        {
            OrgId = org.Id,
            Currency = org.SettlementCurrency,   // V3 §9.1
            Tier = 1,
            AdvancePct = Tier1AdvancePct,
            CapPaise = Tier1CapPaise,
            ReservePct = Tier1ReservePct,
        });
        // Seed a zero-balance wallet so financial reads never get "wallet_not_found".
        db.OrganizationWallets.Add(new OrganizationWallet { OrgId = org.Id, Currency = org.SettlementCurrency });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId,
            Action = "org.create", Entity = "organizations", EntityId = org.Id,
            DetailsJson = $"{{\"name\":\"{JsonEncodedText.Encode(name)}\",\"slug\":\"{org.Slug}\"}}",
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Two concurrent requests generated the same slug; caller may retry.
            return ServiceResult<OrgDetail>.Fail("slug_conflict");
        }

        log.LogInformation("Org {OrgId} ({Slug}) created by {UserId}", org.Id, org.Slug, userId);
        return ServiceResult<OrgDetail>.Success(ToDetail(org, 1, OrgRole.Owner));
    }

    public async Task<ServiceResult<OrgDetail>> SubmitRepresentationRequestAsync(Guid userId, string name, OrganizationType type,
        string? legalName, string? primaryDomain, string? bio, string? linksJson,
        IReadOnlyList<OrgVerificationEvidence> evidence, CancellationToken ct = default)
    {
        name = name.Trim();
        if (name.Length is < 2 or > 120)
            return ServiceResult<OrgDetail>.Fail("invalid_name");

        if (await fraud.IsBlacklistedAsync(nameof(BlacklistKind.OrgName), name, ct))
            return ServiceResult<OrgDetail>.Fail("org_blacklisted");

        legalName = string.IsNullOrWhiteSpace(legalName) ? null : legalName.Trim();
        primaryDomain = NormalizeDomain(primaryDomain);
        if (primaryDomain is not null && !DomainRegex().IsMatch(primaryDomain))
            return ServiceResult<OrgDetail>.Fail("invalid_domain");

        // Hard dedup only against a domain already held by a *verified* org — that institution already
        // exists in the registry and must be selected + claimed (D-074), not re-requested.
        if (primaryDomain is not null &&
            await db.Organizations.AnyAsync(o => o.PrimaryDomain == primaryDomain && o.DeletedAt == null
                && o.VerificationStatus == OrgVerificationStatus.Verified, ct))
            return ServiceResult<OrgDetail>.Fail("organization_domain_taken");

        if (evidence.Count is 0 or > OrgVerificationService.MaxEvidenceDocs)
            return ServiceResult<OrgDetail>.Fail("invalid_evidence");
        foreach (var e in evidence)
            if (string.IsNullOrWhiteSpace(e.DocType) || string.IsNullOrWhiteSpace(e.StorageKey))
                return ServiceResult<OrgDetail>.Fail("invalid_evidence");

        // D-338 — scanned before the placeholder org is created, so a refused submission leaves no
        // half-staged institution behind. SubjectId is Guid.Empty because the org this evidence belongs to
        // does not exist yet; the audit row still carries the actor and the key.
        if (await scanGate.RejectAnyAsync(evidence.Select(e => e.StorageKey), userId, "organizations", Guid.Empty, ct)
            is { } scanError)
            return ServiceResult<OrgDetail>.Fail(scanError);

        // A hidden placeholder org (D-075): PendingReview, not in the registry, no Owner and no wallet/payout
        // (those are minted at approval, when money can move). It exists only to satisfy the non-null
        // Event.RepresentingOrgId FK and to reuse the M5 verification lifecycle as the staging record.
        var org = new Organization
        {
            Name = name,
            Slug = await UniqueSlugAsync(name, ct),
            Type = type,
            LegalName = legalName,
            PrimaryDomain = primaryDomain,
            NormalizedName = OrganizationRegistryService.Normalize(name),
            Bio = bio,
            LinksJson = linksJson,
            IsPersonal = false,
            VerificationStatus = OrgVerificationStatus.PendingReview,
        };
        org.CanonicalOrgId = org.Id;
        db.Organizations.Add(org);

        // The submitter is a *pending* Representative: enough to manage their own draft event, but IsVerified
        // stays false so M7 grants zero trust capability (CanRepresentOrg reads IsVerified) until approval.
        db.Memberships.Add(new Membership { OrgId = org.Id, UserId = userId, Role = OrgRole.Representative, IsVerified = false });

        foreach (var e in evidence)
            db.VerificationDocuments.Add(new VerificationDocument
            {
                SubjectType = VerificationSubjectType.Organization,
                SubjectId = org.Id,
                DocType = e.DocType.Trim().ToLowerInvariant(),
                StorageKey = e.StorageKey.Trim(),
                UploadedBy = userId,
            });

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId,
            Action = "org.representation_request.submit", Entity = "organizations", EntityId = org.Id,
            DetailsJson = $"{{\"name\":\"{JsonEncodedText.Encode(name)}\",\"slug\":\"{org.Slug}\"}}",
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return ServiceResult<OrgDetail>.Fail("slug_conflict");
        }

        log.LogInformation("Representation request {OrgId} ({Slug}) submitted by {UserId}", org.Id, org.Slug, userId);
        return ServiceResult<OrgDetail>.Success(ToDetail(org, 1, OrgRole.Representative));
    }

    public async Task<IReadOnlyList<RepresentableOrganization>> ListRepresentableAsync(Guid userId, CancellationToken ct = default)
        => await db.Memberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            // `!o.IsPersonal` excludes the self-representation persistence row (D-268). Representing
            // yourself is not an organization, so it must never surface as one — filtering here rather
            // than in each client is what keeps the concept out of the API entirely.
            .Join(db.Organizations.Where(o => o.DeletedAt == null && !o.IsPersonal), m => m.OrgId, o => o.Id,
                // IsVerified is the ORG's registry status, not the caller's standing (that is Role). A
                // staged representation request is a PendingReview row that belongs in this list — the
                // caller may well represent it once approved — but a paid event may only represent a
                // verified one (D-350), so both facts travel or the client guesses.
                (m, o) => new RepresentableOrganization(o.Id, o.Name, o.Slug, o.LogoKey, m.Role.ToString(),
                    o.VerificationStatus == OrgVerificationStatus.Verified,
                    // D-352 — the capability, which the bypass may open; the fact above never moves.
                    identityOptions.Bypass || o.VerificationStatus == OrgVerificationStatus.Verified))
            .ToListAsync(ct);

    public async Task<ServiceResult<OrgDetail>> GetAsync(Guid userId, Guid orgId, bool isAdmin = false, CancellationToken ct = default)
    {
        OrgRole? role = null;
        if (!isAdmin)
        {
            role = await RoleAsync(userId, orgId, ct);
            // D-018: non-member (or non-existent org) both return not_found — never leak org existence.
            if (role is null) return ServiceResult<OrgDetail>.Fail("not_found");
        }
        var org = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orgId && o.DeletedAt == null, ct);
        if (org is null) return ServiceResult<OrgDetail>.Fail("not_found");
        return ServiceResult<OrgDetail>.Success(ToDetail(org, await TierAsync(orgId, ct), role));
    }

    // D-194: platform-wide org list — no admin-scoped org read existed before this. Search matches
    // name/slug/domain; paginated in SQL (small row counts here — orgs, unlike events, don't need the
    // stats-derived-sort in-memory-window trade-off D-187 needed); member/event counts are two grouped
    // queries over the current page only, never per-row (same discipline as D-186/D-188's batch counts).
    public async Task<(IReadOnlyList<AdminOrgView> Items, int Total)> ListForAdminAsync(AdminOrgListFilter filter, CancellationToken ct = default)
    {
        // D-353 — the self-representation row is not an organization and is excluded here as it already is
        // from `/v1/me/representations`, registry search and the public profile. Admin was the one surface
        // still listing it, labelled "Self-representation" — which was defensible while Personal was a
        // product concept and is not now: a reviewer scanning the registry sees a row named after a person,
        // with an Unverified status they cannot action and a wallet that can never receive money.
        //
        // Excluded rather than filterable: there is no reviewer task it belongs to. It stays reachable by
        // id for support (`GetAsync` with isAdmin), so nothing becomes un-debuggable — it just stops
        // appearing in a registry it was never a member of.
        var q = db.Organizations.AsNoTracking().Where(o => o.DeletedAt == null && !o.IsPersonal);
        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var like = $"%{filter.Q.Trim()}%";
            q = q.Where(o => EF.Functions.ILike(o.Name, like) || EF.Functions.ILike(o.Slug, like)
                || (o.PrimaryDomain != null && EF.Functions.ILike(o.PrimaryDomain, like)));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<OrgVerificationStatus>(filter.Status, true, out var status))
            q = q.Where(o => o.VerificationStatus == status);
        if (!string.IsNullOrWhiteSpace(filter.Type) && Enum.TryParse<OrganizationType>(filter.Type, true, out var type))
            q = q.Where(o => o.Type == type);

        var total = await q.CountAsync(ct);
        var page = Math.Max(filter.Page, 1);
        var limit = Math.Clamp(filter.Limit, 1, 100);
        // DB-6: the admin organization list — seeded/imported orgs share a CreatedAt tick.
        var rows = await q.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * limit).Take(limit)
            .Select(o => new { o.Id, o.Name, o.Slug, o.LogoKey, o.Type, o.VerificationStatus, o.PrimaryDomain, o.IsPersonal, o.CreatedAt })
            .ToListAsync(ct);

        var orgIds = rows.Select(r => r.Id).ToList();
        var memberCounts = await db.Memberships.AsNoTracking().Where(m => orgIds.Contains(m.OrgId))
            .GroupBy(m => m.OrgId).Select(g => new { OrgId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrgId, x => x.Count, ct);
        var eventCounts = await db.Events.AsNoTracking().Where(e => orgIds.Contains(e.RepresentingOrgId) && e.DeletedAt == null)
            .GroupBy(e => e.RepresentingOrgId).Select(g => new { OrgId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrgId, x => x.Count, ct);

        var items = rows.Select(r => new AdminOrgView(
            r.Id, r.Name, r.Slug, r.LogoKey, r.Type.ToString(), r.VerificationStatus.ToString(),
            r.PrimaryDomain, r.IsPersonal, memberCounts.GetValueOrDefault(r.Id), eventCounts.GetValueOrDefault(r.Id), r.CreatedAt
        )).ToList();

        return (items, total);
    }

    public async Task<ServiceResult<OrgDetail>> UpdateAsync(Guid userId, Guid orgId, string? name, string? bio,
        string? linksJson, string? logoKey, CancellationToken ct = default)
    {
        var role = await RoleAsync(userId, orgId, ct);
        if (role is not (OrgRole.Owner or OrgRole.Manager))
            return ServiceResult<OrgDetail>.Fail(role is null ? "not_found" : "forbidden");

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId && o.DeletedAt == null, ct);
        if (org is null) return ServiceResult<OrgDetail>.Fail("not_found");
        var nameChanged = false;
        if (name is not null)
        {
            name = name.Trim();
            if (name.Length is < 2 or > 120) return ServiceResult<OrgDetail>.Fail("invalid_name");
            if (org.Name != name) { org.Name = name; nameChanged = true; }   // slug intentionally untouched (D-015)
        }
        if (bio is not null) org.Bio = bio;
        if (linksJson is not null) org.LinksJson = linksJson;
        if (logoKey is not null) org.LogoKey = logoKey;
        // V3 §15 (Phase 16): the org name is indexed in every event's discovery document — reindex the org's indexed
        // (Published + Public) events on a real rename so search reflects the new name (bounded; renames are rare).
        if (nameChanged)
            foreach (var eid in await db.Events.Where(e => e.RepresentingOrgId == orgId && e.Status == EventStatus.Published
                         && e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed && e.DeletedAt == null).Select(e => e.Id).ToListAsync(ct))
                db.OutboxMessages.Add(Search.SearchReindex.Message(eid));
        await db.SaveChangesAsync(ct);
        return ServiceResult<OrgDetail>.Success(ToDetail(org, await TierAsync(orgId, ct), role.Value));
    }

    public async Task<ServiceResult<IReadOnlyList<OrgMember>>> ListMembersAsync(Guid userId, Guid orgId,
        CancellationToken ct = default)
    {
        // D-018: non-members cannot distinguish "org exists but I'm not a member" from "org not found".
        if (await RoleAsync(userId, orgId, ct) is null)
            return ServiceResult<IReadOnlyList<OrgMember>>.Fail("not_found");

        var rows = await db.Memberships.AsNoTracking()
            .Where(m => m.OrgId == orgId)
            .Join(db.Users, m => m.UserId, u => u.Id,
                (m, u) => new { u.Id, u.Phone, u.Name, u.Username, m.Role, m.CreatedAt, u.AvatarKey, m.IsVerified })
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
        return ServiceResult<IReadOnlyList<OrgMember>>.Success(rows
            .Select(x => new OrgMember(x.Id, x.Phone, x.Name, x.Username, x.Role.ToString(), x.CreatedAt, x.AvatarKey, x.IsVerified))
            .ToList());
    }

    public async Task<ServiceResult<OrgMember>> AddMemberAsync(Guid actorId, Guid orgId, string phone, OrgRole role,
        CancellationToken ct = default)
    {
        var actorRole = await RoleAsync(actorId, orgId, ct);
        var allowed = actorRole == OrgRole.Owner || (actorRole == OrgRole.Manager && role == OrgRole.Staff);
        if (!allowed) return ServiceResult<OrgMember>.Fail("forbidden");

        phone = AuthService.NormalizePhone(phone);
        if (phone.Length is < 8 or > 16)
            return ServiceResult<OrgMember>.Fail("invalid_phone");

        // Unknown phones get a provisional user row, same as first OTP login (D-012/D-015).
        var user = await db.Users.FirstOrDefaultAsync(u => u.Phone == phone, ct);
        if (user is null)
        {
            user = new User { Phone = phone, Name = "" };
            db.Users.Add(user);
        }
        else if (await db.Memberships.AnyAsync(m => m.OrgId == orgId && m.UserId == user.Id, ct))
        {
            return ServiceResult<OrgMember>.Fail("already_member");
        }

        var membership = new Membership { OrgId = orgId, UserId = user.Id, Role = role };
        db.Memberships.Add(membership);
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = actorId,
            Action = "membership.add", Entity = "memberships", EntityId = membership.Id,
            DetailsJson = $"{{\"org_id\":\"{orgId}\",\"user_id\":\"{user.Id}\",\"role\":\"{role}\"}}",
        });
        EnqueueAuthorityChanged(orgId, user.Id);
        await db.SaveChangesAsync(ct);
        return ServiceResult<OrgMember>.Success(
            new OrgMember(user.Id, user.Phone, user.Name, user.Username, role.ToString(), membership.CreatedAt,
                user.AvatarKey, membership.IsVerified));
    }

    public async Task<ServiceResult<OrgMember>> ChangeRoleAsync(Guid actorId, Guid orgId, Guid userId, OrgRole role,
        CancellationToken ct = default)
    {
        if (await RoleAsync(actorId, orgId, ct) != OrgRole.Owner)
            return ServiceResult<OrgMember>.Fail("forbidden");

        var membership = await db.Memberships.FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == userId, ct);
        if (membership is null) return ServiceResult<OrgMember>.Fail("not_found");
        if (membership.Role == OrgRole.Owner && role != OrgRole.Owner && await IsLastOwnerAsync(orgId, ct))
            return ServiceResult<OrgMember>.Fail("last_owner");

        var previousRole = membership.Role;
        membership.Role = role;
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = actorId,
            Action = "membership.role_change", Entity = "memberships", EntityId = membership.Id,
            DetailsJson = $"{{\"org_id\":\"{orgId}\",\"user_id\":\"{userId}\",\"from\":\"{previousRole}\",\"to\":\"{role}\"}}",
        });
        // Staged unconditionally, including Manager ⇄ Representative where both sides are Host-level: the
        // handler resolves to the same role and does nothing, and a producer that tried to predict "this one
        // cannot matter" is exactly how a case gets missed (D-304 case 4).
        EnqueueAuthorityChanged(orgId, userId);
        await db.SaveChangesAsync(ct);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        return ServiceResult<OrgMember>.Success(
            new OrgMember(user.Id, user.Phone, user.Name, user.Username, role.ToString(), membership.CreatedAt,
                user.AvatarKey, membership.IsVerified));
    }

    public async Task<ServiceResult<bool>> RemoveMemberAsync(Guid actorId, Guid orgId, Guid userId,
        CancellationToken ct = default)
    {
        var actorRole = await RoleAsync(actorId, orgId, ct);
        var membership = await db.Memberships.FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == userId, ct);
        if (membership is null)
            return actorRole is null ? ServiceResult<bool>.Fail("forbidden") : ServiceResult<bool>.Fail("not_found");

        var allowed = actorId == userId
            || actorRole == OrgRole.Owner
            || (actorRole == OrgRole.Manager && membership.Role == OrgRole.Staff);
        if (!allowed) return ServiceResult<bool>.Fail("forbidden");
        if (membership.Role == OrgRole.Owner && await IsLastOwnerAsync(orgId, ct))
            return ServiceResult<bool>.Fail("last_owner");

        var removedMembershipId = membership.Id;
        var removedRole = membership.Role;
        db.Memberships.Remove(membership);
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = actorId,
            Action = "membership.remove", Entity = "memberships", EntityId = removedMembershipId,
            DetailsJson = $"{{\"org_id\":\"{orgId}\",\"user_id\":\"{userId}\",\"role\":\"{removedRole}\"}}",
        });
        EnqueueAuthorityChanged(orgId, userId);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<KycView>> GetKycAsync(Guid actorId, Guid orgId, CancellationToken ct = default)
    {
        if (await RoleAsync(actorId, orgId, ct) is not (OrgRole.Owner or OrgRole.Finance))
            return ServiceResult<KycView>.Fail("forbidden");

        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId && o.DeletedAt == null, ct);
        if (org is null) return ServiceResult<KycView>.Fail("not_found");
        var records = await db.OrgBankVerifications.AsNoTracking()
            .Where(k => k.OrgId == orgId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new KycRecordView(k.Id, k.Kind.ToString(), k.Status.ToString(), k.PayloadJson, k.CreatedAt, k.ReviewedAt))
            .ToListAsync(ct);
        return ServiceResult<KycView>.Success(new KycView(org.PayoutAccountStatus.ToString(), org.BankLast4, records));
    }

    public async Task<ServiceResult<KycOutcome>> SubmitBankKycAsync(Guid actorId, Guid orgId, string legalName,
        string accountNumber, string ifsc, string holderName, CancellationToken ct = default)
    {
        if (await RoleAsync(actorId, orgId, ct) is not (OrgRole.Owner or OrgRole.Finance))
            return ServiceResult<KycOutcome>.Fail("forbidden");

        legalName = legalName.Trim();
        holderName = holderName.Trim();
        ifsc = ifsc.Trim().ToUpperInvariant();
        accountNumber = new string(accountNumber.Where(char.IsAsciiDigit).ToArray());
        if (legalName.Length < 2 || holderName.Length < 2) return ServiceResult<KycOutcome>.Fail("invalid_name");
        if (accountNumber.Length is < 6 or > 20) return ServiceResult<KycOutcome>.Fail("invalid_account_number");
        if (!IfscRegex().IsMatch(ifsc)) return ServiceResult<KycOutcome>.Fail("invalid_ifsc");

        var result = await kyc.PennyDropAsync(accountNumber, ifsc, holderName, ct);
        var org = await db.Organizations.FirstAsync(o => o.Id == orgId, ct);

        // Full account number is never persisted — masked payload only (D-016).
        var last4 = accountNumber[^4..];
        var kycRecord = new OrgBankVerification
        {
            OrgId = orgId,
            Kind = KycKind.PennyDrop,
            Status = result.Approved ? KycStatus.Approved : KycStatus.Rejected,
            ReviewedAt = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(new
            {
                legal_name = legalName,
                holder_name = holderName,
                ifsc,
                account_last4 = last4,
                provider_detail = result.Detail,
            }),
        };
        db.OrgBankVerifications.Add(kycRecord);

        if (result.Approved)
        {
            var linked = await route.CreateLinkedAccountAsync(orgId, legalName, accountNumber, ifsc, ct);
            org.RazorpayLinkedAccountId = linked.LinkedAccountId;
            org.PayoutAccountStatus = PayoutAccountStatus.Active;
            org.BankLast4 = last4;
            log.LogInformation("Org {OrgId} payouts activated (linked account {LinkedAccountId})", orgId, linked.LinkedAccountId);
        }
        // Rejection never regresses an already-Active org (D-016).

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = actorId,
            Action = "kyc.submit", Entity = "kyc_records", EntityId = kycRecord.Id,
            DetailsJson = $"{{\"org_id\":\"{orgId}\",\"kind\":\"bank\",\"approved\":{result.Approved.ToString().ToLower()}}}",
        });

        await db.SaveChangesAsync(ct);
        return ServiceResult<KycOutcome>.Success(new KycOutcome(
            result.Approved ? "approved" : "rejected", result.Detail, org.PayoutAccountStatus.ToString()));
    }

    public async Task<ServiceResult<KycOutcome>> SubmitPanKycAsync(Guid actorId, Guid orgId, string pan, string name,
        CancellationToken ct = default)
    {
        if (await RoleAsync(actorId, orgId, ct) is not (OrgRole.Owner or OrgRole.Finance))
            return ServiceResult<KycOutcome>.Fail("forbidden");

        pan = pan.Trim().ToUpperInvariant();
        name = name.Trim();
        if (!PanRegex().IsMatch(pan)) return ServiceResult<KycOutcome>.Fail("invalid_pan");
        if (name.Length < 2) return ServiceResult<KycOutcome>.Fail("invalid_name");

        var result = await kyc.PanMatchAsync(pan, name, ct);
        var kycRecord = new OrgBankVerification
        {
            OrgId = orgId,
            Kind = KycKind.PanMatch,
            Status = result.Approved ? KycStatus.Approved : KycStatus.Rejected,
            ReviewedAt = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(new
            {
                name,
                pan_last4 = pan[^4..],
                provider_detail = result.Detail,
            }),
        };
        db.OrgBankVerifications.Add(kycRecord);
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = actorId,
            Action = "kyc.submit", Entity = "kyc_records", EntityId = kycRecord.Id,
            DetailsJson = $"{{\"org_id\":\"{orgId}\",\"kind\":\"pan\",\"approved\":{result.Approved.ToString().ToLower()}}}",
        });
        await db.SaveChangesAsync(ct);

        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == orgId, ct);
        return ServiceResult<KycOutcome>.Success(new KycOutcome(
            result.Approved ? "approved" : "rejected", result.Detail, org.PayoutAccountStatus.ToString()));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid actorId, Guid orgId, CancellationToken ct = default)
    {
        if (await RoleAsync(actorId, orgId, ct) != OrgRole.Owner)
            return ServiceResult<bool>.Fail("forbidden");

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId && o.DeletedAt == null, ct);
        if (org is null) return ServiceResult<bool>.Fail("not_found");

        // Guard: published or closed events carry attendee and financial obligations.
        var hasActiveEvents = await db.Events.AnyAsync(
            e => e.RepresentingOrgId == orgId
                 && (e.Status == EventStatus.Published || e.Status == EventStatus.Closed), ct);
        if (hasActiveEvents)
            return ServiceResult<bool>.Fail("has_active_events");

        org.DeletedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = actorId,
            Action = "org.delete", Entity = "organizations", EntityId = orgId,
            DetailsJson = $"{{\"name\":\"{JsonEncodedText.Encode(org.Name)}\",\"slug\":\"{org.Slug}\"}}",
        });
        await db.SaveChangesAsync(ct);
        log.LogInformation("Org {OrgId} ({Slug}) soft-deleted by {UserId}", orgId, org.Slug, actorId);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<PublicOrgView>> GetPublicAsync(string slug, CancellationToken ct = default)
    {
        slug = slug.ToLowerInvariant();
        // D-075: only a Verified org has a public profile. A staged representation-request placeholder
        // (PendingReview) or a rejected/personal org returns not_found — never leak its existence (D-018).
        var org = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Slug == slug && o.DeletedAt == null
                && o.VerificationStatus == OrgVerificationStatus.Verified, ct);
        if (org is null) return ServiceResult<PublicOrgView>.Fail("not_found");

        var eventsCount = await db.Events.AsNoTracking().CountAsync(
            e => e.RepresentingOrgId == org.Id
                && (e.Status == EventStatus.Published || e.Status == EventStatus.Closed)
                && e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed, ct);

        var membersCount = await db.Memberships.AsNoTracking()
            .CountAsync(m => m.OrgId == org.Id, ct);

        var tier = await db.PayoutSchedules.AsNoTracking()
            .Where(p => p.OrgId == org.Id).Select(p => p.Tier).FirstOrDefaultAsync(ct);

        return ServiceResult<PublicOrgView>.Success(
            new PublicOrgView(org.Id, org.Name, org.Slug, org.LogoKey, org.Bio,
                tier == 0 ? 1 : tier, eventsCount, membersCount));
    }

    private async Task<OrgRole?> RoleAsync(Guid userId, Guid orgId, CancellationToken ct)
        => await db.Memberships.AsNoTracking()
            .Where(m => m.OrgId == orgId && m.UserId == userId)
            .Select(m => (OrgRole?)m.Role)
            .FirstOrDefaultAsync(ct);

    private async Task<int> TierAsync(Guid orgId, CancellationToken ct)
        => await db.PayoutSchedules.AsNoTracking()
            .Where(p => p.OrgId == orgId).Select(p => p.Tier).FirstOrDefaultAsync(ct);

    private async Task<bool> IsLastOwnerAsync(Guid orgId, CancellationToken ct)
        => await db.Memberships.CountAsync(m => m.OrgId == orgId && m.Role == OrgRole.Owner, ct) <= 1;

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var slug = Slugify(name);
        // Uniquify with a short random suffix on collision (D-010).
        while (await db.Organizations.AnyAsync(o => o.Slug == slug, ct))
            slug = $"{Slugify(name)}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant()}";
        return slug;
    }

    private static string Slugify(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "org" : slug[..Math.Min(slug.Length, 60)];
    }

    // D-194: role is null for an admin viewer who isn't a member — surfaced as "admin", never fabricated
    // as a real membership role.
    private static OrgDetail ToDetail(Organization org, int tier, OrgRole? role) => new(
        org.Id, org.Name, org.Slug, org.LogoKey, org.Bio, org.LinksJson,
        org.PayoutAccountStatus.ToString(), org.BankLast4, tier, role?.ToString() ?? "admin",
        org.Type.ToString(), org.PrimaryDomain, org.VerificationStatus.ToString());

    /// <summary>Strips scheme/www/trailing-slash and lowercases; returns null for blank input.</summary>
    private static string? NormalizeDomain(string? d)
    {
        if (string.IsNullOrWhiteSpace(d)) return null;
        d = d.Trim().ToLowerInvariant();
        if (d.StartsWith("http://")) d = d[7..];
        else if (d.StartsWith("https://")) d = d[8..];
        if (d.StartsWith("www.")) d = d[4..];
        d = d.TrimEnd('/');
        return d.Length == 0 ? null : d;
    }

    // Postgres unique-constraint violation (SQLSTATE 23505).
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException?.Message?.Contains("23505") == true;

    [GeneratedRegex("^[A-Z]{4}0[A-Z0-9]{6}$")]
    private static partial Regex IfscRegex();

    [GeneratedRegex("^[A-Z]{5}[0-9]{4}[A-Z]$")]
    private static partial Regex PanRegex();

    // Basic hostname: labels of a-z/0-9/hyphen, at least one dot (e.g. nsrit.edu.in).
    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$")]
    private static partial Regex DomainRegex();
}
