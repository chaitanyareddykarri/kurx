using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

public record OrgInvitationView(Guid Id, Guid OrgId, string? OrgName, string? InvitedPhone, string Role,
    string Status, DateTime ExpiresAt, DateTime CreatedAt);

/// <summary>Org invitations (D-064): invite a teammate to your org (GitHub-style). Membership is created
/// ONLY when the invitee explicitly accepts the token. Finishes the scaffolded <c>org_invitations</c>
/// table (D-027). Owner invites any role; Manager invites Staff only (mirrors AddMemberAsync, D-015).</summary>
public interface IOrgInvitationService
{
    Task<ServiceResult<OrgInvitationView>> InviteAsync(Guid actorId, Guid orgId, string phone, OrgRole role, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<OrgInvitationView>>> ListForOrgAsync(Guid actorId, Guid orgId, CancellationToken ct = default);
    Task<ServiceResult<bool>> CancelAsync(Guid actorId, Guid orgId, Guid invitationId, CancellationToken ct = default);
    Task<IReadOnlyList<OrgInvitationView>> ListMineAsync(Guid userId, CancellationToken ct = default);
    Task<ServiceResult<OrgInvitationView>> AcceptAsync(Guid userId, string token, CancellationToken ct = default);
    Task<ServiceResult<bool>> DeclineAsync(Guid userId, string token, CancellationToken ct = default);
}
