namespace Kurx.Application.Abstractions;

public record OrgFollowView(Guid OrgId, string Name, string Slug, string? LogoKey);

/// <summary>Attendee engagement (D-064): bookmark events and follow organizations. Finishes the
/// scaffolded <c>saved_events</c> / <c>organization_followers</c> tables (D-018). No provider needed.</summary>
public interface ISocialService
{
    /// <returns>Ok(true) newly saved, Ok(false) already saved; Fail("not_found") if the event isn't a visible published event.</returns>
    Task<ServiceResult<bool>> SaveEventAsync(Guid userId, Guid eventId, CancellationToken ct = default);
    Task<bool> UnsaveEventAsync(Guid userId, Guid eventId, CancellationToken ct = default);
    Task<IReadOnlyList<EventSummary>> ListSavedAsync(Guid userId, int limit, CancellationToken ct = default);

    Task<ServiceResult<bool>> FollowOrgAsync(Guid userId, Guid orgId, CancellationToken ct = default);
    Task<bool> UnfollowOrgAsync(Guid userId, Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<OrgFollowView>> ListFollowingAsync(Guid userId, int limit, CancellationToken ct = default);
}
