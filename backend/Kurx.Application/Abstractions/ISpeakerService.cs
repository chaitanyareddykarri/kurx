namespace Kurx.Application.Abstractions;

public record SpeakerInput(string Name, string? Bio, string? PhotoKey, string? Company, string? Role, string? SocialLinksJson,
    // Optional link to a real Kurx account (D-20x) — null (the common case) means this speaker is
    // curated content with no account; "Connect" correctly does not apply. Pass a UserId found via
    // /v1/public/users?q= when the speaker happens to be a registered user.
    Guid? UserId = null);

public record SpeakerView(Guid Id, Guid OrgId, string Name, string Bio, string? PhotoKey, string Company, string Role,
    string? SocialLinksJson, Guid? UserId, string? Username, string? AvatarKey,
    /// <summary>Presigned companions (D-302): the speaker's own uploaded photo, and the linked Kurx
    /// account's avatar. Two different pictures, so two URLs — a speaker may have either, both or neither.</summary>
    string? PhotoUrl = null, string? AvatarUrl = null);

/// <summary>Org-scoped speaker profiles, reusable across events. Owner/Manager manage; any org member reads.</summary>
public interface ISpeakerService
{
    Task<ServiceResult<SpeakerView>> CreateAsync(Guid userId, Guid orgId, bool isAdmin, SpeakerInput input, CancellationToken ct = default);

    Task<ServiceResult<SpeakerView>> UpdateAsync(Guid userId, Guid speakerId, bool isAdmin, SpeakerInput input, CancellationToken ct = default);

    Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid speakerId, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<SpeakerView>>> ListForOrgAsync(Guid userId, Guid orgId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Assigns a speaker to an event (general listing) and, optionally, one of its schedule sessions.</summary>
    Task<ServiceResult<bool>> AssignToEventAsync(Guid userId, Guid eventId, bool isAdmin, Guid speakerId, Guid? sessionId, CancellationToken ct = default);

    Task<ServiceResult<bool>> RemoveFromEventAsync(Guid userId, Guid eventId, bool isAdmin, Guid speakerId, CancellationToken ct = default);

    Task<IReadOnlyList<SpeakerView>> ListForEventAsync(Guid eventId, CancellationToken ct = default);
}
