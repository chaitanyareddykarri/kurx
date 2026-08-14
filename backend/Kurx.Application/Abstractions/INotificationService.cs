using System.Text.Json.Serialization;

namespace Kurx.Application.Abstractions;

public record NotificationView(Guid Id, string Kind, string Title, string Body, string? DataJson, DateTime? ReadAt, DateTime CreatedAt);

/// <summary>Wire names are declared on the record (D-259 addendum). SnakeCaseResponseConverter applies the
/// platform's snake_case convention to responses at the edge, but a caller that deserializes one of these
/// back — the integration suite does, via <c>ReadFromJsonAsync&lt;List&lt;DeviceView&gt;&gt;()</c> — uses
/// default options and never sees that converter, so it silently bound every property to its default.
/// An attribute is honoured by the default serializer in BOTH directions, which is what makes the name
/// intrinsic to the type instead of a property of the pipeline it happens to travel through.
///
/// <para>Only multi-word names need one: a single word is identical in camelCase and snake_case.</para></summary>
public record DeviceView(
    Guid Id,
    [property: JsonPropertyName("fcm_token")] string FcmToken,
    string Platform,
    [property: JsonPropertyName("device_name")] string? DeviceName,
    [property: JsonPropertyName("app_version")] string? AppVersion,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("last_seen")] DateTime LastSeen,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);

public interface INotificationService
{
    Task<IReadOnlyList<NotificationView>> ListAsync(Guid userId, int page, int pageSize, bool unreadOnly = false, string? type = null, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    Task<ServiceResult<bool>> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    Task<ServiceResult<bool>> MarkAllReadAsync(Guid userId, CancellationToken ct = default);

    // Alias of GetUnreadCountAsync kept for the /v1/me notification endpoints (D-064).
    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default);

    Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    Task RegisterDeviceAsync(Guid userId, string fcmToken, string platform, string? deviceName = null, string? appVersion = null, CancellationToken ct = default);

    Task<ServiceResult<bool>> UpdateDeviceAsync(Guid userId, Guid deviceId, string? deviceName, string? appVersion, bool? isActive, CancellationToken ct = default);

    Task<ServiceResult<bool>> DeleteDeviceAsync(Guid userId, Guid deviceId, CancellationToken ct = default);

    Task<IReadOnlyList<DeviceView>> ListDevicesAsync(Guid userId, CancellationToken ct = default);

    Task<bool> RemoveDeviceAsync(Guid userId, Guid deviceId, CancellationToken ct = default);

    /// <summary>Writes the notification row and best-effort pushes to every registered device (console/mock in dev).</summary>
    /// <param name="dedupKey">
    /// Optional idempotency identity (DB-3). When supplied, at most one notification with this
    /// <c>(userId, dedupKey)</c> pair can ever exist — enforced by a partial unique index, so two replicas
    /// racing the same fan-out converge on one row rather than both passing a pre-check. A duplicate is a
    /// silent no-op: no row, no push, no SignalR broadcast.
    /// <para>Build it with <see cref="NotificationDedup"/>; never hand-format the string. Leave it null
    /// unless a second such notification to the same user is <i>always</i> wrong — most kinds repeat
    /// legitimately, and a wrongly-deduplicated notification is one the user simply never receives.</para>
    /// </param>
    Task NotifyAsync(Guid userId, string kind, string title, string body, object? data = null, CancellationToken ct = default, string? dedupKey = null);
}
