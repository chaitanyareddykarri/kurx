namespace Kurx.Application.Abstractions;

/// <summary>A (room, user) pair whose last connection just went away.</summary>
public record PresenceDeparture(Guid RoomId, Guid UserId);

/// <summary>
/// Who is currently connected to a chat room (D-114).
///
/// **Shared state by definition, so it is never process-local.** No static collections, no singleton
/// registry, no per-server cache: the store is Redis, which is what lets the same code be correct on
/// one server and on ten behind a backplane.
///
/// Availability is decided **once**, here, via <see cref="IsEnabled"/>. Callers ask this rather than
/// checking for Redis themselves, so there is exactly one place that knows presence can be off — and
/// enabling it later is a deployment change, not a code change.
///
/// When disabled: chat, attachments and the persisted read pointer all keep working; only live
/// presence and typing go quiet. That is an operational dependency, not an application error.
/// </summary>
public interface IPresenceService
{
    /// <summary>False when no shared store is configured. Callers must degrade, never fall back to
    /// process-local state.</summary>
    bool IsEnabled { get; }

    /// <summary>Registers a connection against a room. Idempotent — a reconnect or a duplicate join
    /// re-registers the same id without corrupting the count.
    ///
    /// Returns true only when this is the user's <b>first</b> live connection to the room, which is
    /// the moment they actually came online. A second tab or device returns false.</summary>
    Task<bool> TrackAsync(Guid roomId, Guid userId, string connectionId, CancellationToken ct = default);

    /// <summary>Drops a connection from every room it joined and reports the (room, user) pairs that
    /// now have <b>no</b> remaining connections. Idempotent: a repeated release finds nothing and
    /// reports nothing, so a retried disconnect cannot emit a spurious offline event.</summary>
    Task<IReadOnlyList<PresenceDeparture>> ReleaseAsync(string connectionId, CancellationToken ct = default);

    /// <summary>Extends the TTL on everything this connection owns. The TTL exists only so a process
    /// that dies without running disconnect cleanup eventually stops reporting phantom users — it is
    /// not the normal cleanup path.</summary>
    Task HeartbeatAsync(string connectionId, CancellationToken ct = default);

    /// <summary>User ids currently connected to the room. Empty when presence is disabled.</summary>
    Task<IReadOnlySet<Guid>> OnlineUsersAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>Live connection ids for one (room, user) — used to evict a banned member's sockets.</summary>
    Task<IReadOnlyList<string>> ConnectionsAsync(Guid roomId, Guid userId, CancellationToken ct = default);

    /// <summary>Forgets a user entirely in one room. Used when a ban evicts them, so presence does not
    /// keep reporting someone who is no longer allowed in.</summary>
    Task ForgetAsync(Guid roomId, Guid userId, CancellationToken ct = default);
}
