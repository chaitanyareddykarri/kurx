namespace Kurx.Application.Abstractions;

public record AdminUserView(Guid Id, string Phone, string Name, string? Username, string? Email,
    bool Suspended, bool Banned, string? ModerationReason, DateTime CreatedAt);

/// <summary>Platform user administration (D-060). Search users and moderate accounts (suspend / ban /
/// unban). Moderation is enforced in <c>AuthService</c> — a suspended/banned user can't log in or refresh.
/// Gated at the endpoint by the Moderation policy.</summary>
public interface IUserAdminService
{
    /// <param name="q">Phone / name / username substring. When empty, returns the currently-moderated
    /// accounts (suspended or banned) rather than dumping the whole user table.</param>
    Task<IReadOnlyList<AdminUserView>> ListAsync(string? q, int limit, CancellationToken ct = default);

    /// <param name="action">"suspend" | "ban" | "unban". A staff member can't moderate themselves or a SuperAdmin.</param>
    Task<ServiceResult<AdminUserView>> ModerateAsync(Guid actorId, Guid userId, string action, string? reason, CancellationToken ct = default);
}
