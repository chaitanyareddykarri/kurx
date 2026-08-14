using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.Presence;

/// <summary>
/// Presence with no shared store configured (D-114).
///
/// A null object rather than an in-memory fallback, deliberately. A process-local registry would be
/// *wrong* the moment a second instance ran — reporting users offline who are connected elsewhere —
/// and dev would behave differently from production. Being cleanly absent is better than being
/// confidently incorrect.
///
/// Everything else keeps working: messages, attachments, moderation, and read receipts, which sit on
/// the persisted <c>LastReadMessageId</c> rather than on presence. Only live online status and typing
/// go quiet.
///
/// This is an operational dependency — configure REDIS_CONNECTION to enable presence — not an error.
/// </summary>
public class PresenceDisabledService : IPresenceService
{
    public bool IsEnabled => false;

    /// <summary>Never "first connection", so no online event is ever emitted.</summary>
    public Task<bool> TrackAsync(Guid roomId, Guid userId, string connectionId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<IReadOnlyList<PresenceDeparture>> ReleaseAsync(string connectionId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PresenceDeparture>>([]);

    public Task HeartbeatAsync(string connectionId, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>Nobody is known to be online. Callers that use this to suppress push therefore
    /// notify everyone — over-notifying rather than silently dropping.</summary>
    public Task<IReadOnlySet<Guid>> OnlineUsersAsync(Guid roomId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

    public Task<IReadOnlyList<string>> ConnectionsAsync(Guid roomId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task ForgetAsync(Guid roomId, Guid userId, CancellationToken ct = default) => Task.CompletedTask;
}
