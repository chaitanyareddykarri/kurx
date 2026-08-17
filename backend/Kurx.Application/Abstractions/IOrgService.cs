using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

public record ServiceResult<T>(bool Ok, string? Error = null, T? Value = default)
{
    public static ServiceResult<T> Success(T value) => new(true, Value: value);
    public static ServiceResult<T> Fail(string error) => new(false, error);
}

/// <summary>An organization the caller may represent, and the authority they hold over it. Named for
/// the question it answers — "who may I represent?" — rather than for the table behind it.
/// <c>Authority</c>, not "role": a role over an event would imply ownership, and representation never
/// confers that. There is no "personal" variant: representing yourself is not an organization, so it is
/// not in this list at all (D-268).</summary>
/// <param name="IsVerified">Whether the organization itself is <c>Verified</c> in the registry (M5) — not
/// whether the caller's standing over it is verified, which is <paramref name="Authority"/>. Carried
/// because a representation request stages a <c>PendingReview</c> placeholder org that legitimately
/// appears in this list, and a paid event may only represent a verified one (D-350). Without it the
/// client cannot tell the two apart and offers a choice the server refuses at submission.</param>
/// <param name="CanBackPaidEvent">Whether this organization may be represented by a PAID event — the
/// CAPABILITY, where <paramref name="IsVerified"/> is the FACT. They differ only under D-352's bypass,
/// which opens the gate outside Production while leaving the reported status truthful. The client gates
/// on this and displays that; deriving the gate from the fact is what made a dev account unable to
/// create the paid event the server would have accepted.</param>
public record RepresentableOrganization(Guid Id, string Name, string Slug, string? LogoKey, string Authority,
    bool IsVerified = false, bool CanBackPaidEvent = false);

public record PublicOrgView(
    Guid Id, string Name, string Slug, string? LogoKey, string? Bio,
    int Tier, int EventsCount, int MembersCount);

public record OrgDetail(Guid Id, string Name, string Slug, string? LogoKey, string? Bio, string? LinksJson,
    string PayoutAccountStatus, string? BankLast4, int Tier, string Role,
    string Type, string? PrimaryDomain, string VerificationStatus);

/// <summary>Admin org-list filter (D-194) — mirrors AdminEventListFilter's (Q/Status/Limit/Page) shape (D-186/187).</summary>
public record AdminOrgListFilter(string? Q, string? Status, string? Type, int Limit = 50, int Page = 1);

/// <summary>One row of the admin org list (D-194) — mirrors AdminEventView's "everything the table needs,
/// nothing the detail workspace doesn't already fetch itself" shape.</summary>
public record AdminOrgView(
    Guid OrgId, string Name, string Slug, string? LogoKey, string Type, string VerificationStatus,
    string? PrimaryDomain, bool IsPersonal, int MemberCount, int EventCount, DateTime CreatedAt);

public record OrgMember(Guid UserId, string Phone, string Name, string? Username, string Role, DateTime JoinedAt,
    string? AvatarKey, bool IsVerified,
    /// <summary>Presigned companion to <c>AvatarKey</c> (D-302). Null when there is no key.</summary>
    string? AvatarUrl = null);

public record KycRecordView(Guid Id, string Kind, string Status, string? PayloadJson, DateTime CreatedAt, DateTime? ReviewedAt);

public record KycView(string PayoutAccountStatus, string? BankLast4, IReadOnlyList<KycRecordView> Records);

/// <summary>Outcome of a synchronous KYC submission (mock/real provider decides immediately or stays pending).</summary>
public record KycOutcome(string Status, string? Detail, string PayoutAccountStatus);

/// <summary>
/// Organizations, memberships, and KYC. Role matrix per docs/DECISIONS.md D-015;
/// KYC/payout-account flow per D-016.
/// </summary>
public interface IOrgService
{
    /// <summary>Creates the org (registry: type + normalized name + canonical=self, M4), makes the caller
    /// Owner, and seeds the T1 payout schedule (D-007, D-016). Hard-blocks a duplicate primary domain.</summary>
    Task<ServiceResult<OrgDetail>> CreateAsync(Guid userId, string name, OrganizationType type, string? legalName,
        string? primaryDomain, string? bio, string? linksJson, bool isPersonal = false, CancellationToken ct = default);

    /// <summary>D-075: event-first "who are you representing?" for a not-yet-registered institution.
    /// Creates a hidden placeholder org (PendingReview, no Owner/wallet/payout) + evidence, and makes the
    /// caller a *pending* Representative (IsVerified=false → zero trust capability until admin approval).
    /// The org is invisible to registry search and the public profile until an admin approves it.</summary>
    Task<ServiceResult<OrgDetail>> SubmitRepresentationRequestAsync(Guid userId, string name, OrganizationType type,
        string? legalName, string? primaryDomain, string? bio, string? linksJson,
        IReadOnlyList<OrgVerificationEvidence> evidence, CancellationToken ct = default);

    /// <summary>The organizations this user may represent. Never includes the self-representation
    /// persistence row — representing yourself is <c>Representing = Personal</c>, not an organization in a
    /// list (D-268).</summary>
    Task<IReadOnlyList<RepresentableOrganization>> ListRepresentableAsync(Guid userId, CancellationToken ct = default);

    /// <summary>D-194: isAdmin bypasses the D-018 membership check below (VerificationReviewer-gated at the
    /// endpoint), the same one-parameter shape every other org-scoped service already has (D-186).</summary>
    Task<ServiceResult<OrgDetail>> GetAsync(Guid userId, Guid orgId, bool isAdmin = false, CancellationToken ct = default);

    /// <summary>D-194: platform-wide, paginated, searchable org list — no admin-scoped org read existed
    /// before this (admin/STATUS.md §5.4). Mirrors IEventService.ListForAdminAsync's (Items, Total) shape.</summary>
    Task<(IReadOnlyList<AdminOrgView> Items, int Total)> ListForAdminAsync(AdminOrgListFilter filter, CancellationToken ct = default);

    /// <summary>Owner/Manager. Null arguments leave the field unchanged; the slug never changes (D-015).</summary>
    Task<ServiceResult<OrgDetail>> UpdateAsync(Guid userId, Guid orgId, string? name, string? bio, string? linksJson,
        string? logoKey, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<OrgMember>>> ListMembersAsync(Guid userId, Guid orgId, CancellationToken ct = default);

    /// <summary>Owner adds any role; Manager adds Staff only. Unknown phones are provisioned as users (D-015).</summary>
    Task<ServiceResult<OrgMember>> AddMemberAsync(Guid actorId, Guid orgId, string phone, OrgRole role, CancellationToken ct = default);

    /// <summary>Owner only. The last Owner cannot be demoted.</summary>
    Task<ServiceResult<OrgMember>> ChangeRoleAsync(Guid actorId, Guid orgId, Guid userId, OrgRole role, CancellationToken ct = default);

    /// <summary>Owner removes anyone; Manager removes Staff; anyone may remove themselves. The last Owner cannot leave.</summary>
    Task<ServiceResult<bool>> RemoveMemberAsync(Guid actorId, Guid orgId, Guid userId, CancellationToken ct = default);

    /// <summary>Owner/Finance.</summary>
    Task<ServiceResult<KycView>> GetKycAsync(Guid actorId, Guid orgId, CancellationToken ct = default);

    /// <summary>Owner/Finance. Penny drop; on approval creates the Route linked account and activates payouts (D-016).</summary>
    Task<ServiceResult<KycOutcome>> SubmitBankKycAsync(Guid actorId, Guid orgId, string legalName, string accountNumber,
        string ifsc, string holderName, CancellationToken ct = default);

    /// <summary>Owner/Finance. PAN-name match; recorded for review, does not gate payouts by itself.</summary>
    Task<ServiceResult<KycOutcome>> SubmitPanKycAsync(Guid actorId, Guid orgId, string pan, string name, CancellationToken ct = default);

    /// <summary>Owner only. Soft-deletes the org (D-025). Blocked when published or closed events exist (D-030).</summary>
    Task<ServiceResult<bool>> DeleteAsync(Guid actorId, Guid orgId, CancellationToken ct = default);

    /// <summary>Public — no authentication required. Returns 404 for unknown or deleted orgs.</summary>
    Task<ServiceResult<PublicOrgView>> GetPublicAsync(string slug, CancellationToken ct = default);
}
