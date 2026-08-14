using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;
using Kurx.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Kurx.Infrastructure.Notifications;

public class NotificationService(
    KurxDbContext db,
    IPushSender push,
    IRealtimeBroadcaster broadcaster,
    ILogger<NotificationService> log) : INotificationService
{
    /// <summary>Was this failure our dedup index refusing a duplicate, rather than any other write error?
    ///
    /// <para>Matched on the index NAME, not merely on SQLSTATE 23505. <c>notifications</c> could gain
    /// another unique index later, and swallowing an unrelated uniqueness violation as "already delivered"
    /// would turn a real defect into silence. A conflict on a different constraint must still throw.</para></summary>
    private static bool IsDedupConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName == "ix_notifications_dedup";

    /// <summary>What this user has left switched on for this category (D-263).
    ///
    /// <para><b>Security is never suppressed</b>, whatever the stored row says — checked here as well as
    /// on the write path, because either check alone is one bug away from silencing the messages that
    /// tell someone their account is being taken over.</para>
    ///
    /// <para>No row means the pre-existing behaviour: in-app and push on. That default is why shipping
    /// this table needed no backfill.</para>
    ///
    /// <para><b>Scope, stated plainly:</b> this method gates the two channels <c>NotifyAsync</c> actually
    /// drives — in-app and push. The <c>email</c> and <c>whatsapp</c> switches are stored and returned by
    /// the settings API but are not yet consulted by the senders that own those channels
    /// (<c>AnnouncementService</c> and the WhatsApp log), which live outside this module. Named in D-263
    /// rather than left as a silent half-enforcement.</para></summary>
    private async Task<(bool InApp, bool Push)> ResolveChannelsAsync(
        Guid userId, Kurx.Domain.Enums.NotificationCategory category, CancellationToken ct)
    {
        if (NotificationCategories.IsAlwaysDelivered(category)) return (true, true);

        var row = await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId && p.Category == category)
            .Select(p => new { p.InApp, p.Push })
            .FirstOrDefaultAsync(ct);

        return row is null ? (true, true) : (row.InApp, row.Push);
    }

    public async Task<IReadOnlyList<NotificationView>> ListAsync(
        Guid userId, int page, int pageSize, bool unreadOnly = false, string? type = null, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Max(page, 1);
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(n => n.Kind == type);
        }
        return await query
            // DB-6: announcement fan-out writes one row per recipient in a single pass, so a user's bell is
            // full of same-tick rows — the exact shape an untied sort pages incorrectly.
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationView(n.Id, n.Kind, n.Title, n.Body, n.DataJson, n.ReadAt, n.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);
    }

    public async Task<ServiceResult<bool>> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct);
        if (n is null) return ServiceResult<bool>.Fail("not_found");
        if (n.ReadAt == null)
        {
            n.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            var unreadCount = await GetUnreadCountAsync(userId, ct);
            await broadcaster.BroadcastUnreadCountAsync(userId, unreadCount, ct);
        }
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ToListAsync(ct);
        if (unread.Any())
        {
            foreach (var n in unread) n.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await broadcaster.BroadcastUnreadCountAsync(userId, 0, ct);
        }
        return ServiceResult<bool>.Success(true);
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default)
        => GetUnreadCountAsync(userId, ct);

    public async Task<bool> RemoveDeviceAsync(Guid userId, Guid deviceId, CancellationToken ct = default)
        => await db.Devices.Where(d => d.Id == deviceId && d.UserId == userId).ExecuteDeleteAsync(ct) > 0;

    public async Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct);
        if (n is null) return ServiceResult<bool>.Fail("not_found");
        var wasUnread = n.ReadAt == null;
        db.Notifications.Remove(n);
        await db.SaveChangesAsync(ct);
        if (wasUnread)
        {
            var unreadCount = await GetUnreadCountAsync(userId, ct);
            await broadcaster.BroadcastUnreadCountAsync(userId, unreadCount, ct);
        }
        return ServiceResult<bool>.Success(true);
    }

    public async Task RegisterDeviceAsync(Guid userId, string fcmToken, string platform, string? deviceName = null, string? appVersion = null, CancellationToken ct = default)
    {
        var existing = await db.Devices.FirstOrDefaultAsync(d => d.FcmToken == fcmToken, ct);
        if (existing is null)
        {
            db.Devices.Add(new Device
            {
                UserId = userId,
                FcmToken = fcmToken,
                Platform = platform,
                DeviceName = deviceName,
                AppVersion = appVersion,
                IsActive = true
            });
        }
        else
        {
            existing.UserId = userId;
            existing.Platform = platform;
            existing.DeviceName = deviceName;
            existing.AppVersion = appVersion;
            existing.IsActive = true;
            existing.LastSeen = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<ServiceResult<bool>> UpdateDeviceAsync(Guid userId, Guid deviceId, string? deviceName, string? appVersion, bool? isActive, CancellationToken ct = default)
    {
        var d = await db.Devices.FirstOrDefaultAsync(x => x.Id == deviceId && x.UserId == userId, ct);
        if (d is null) return ServiceResult<bool>.Fail("not_found");
        if (deviceName is not null) d.DeviceName = deviceName;
        if (appVersion is not null) d.AppVersion = appVersion;
        if (isActive.HasValue) d.IsActive = isActive.Value;
        d.LastSeen = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> DeleteDeviceAsync(Guid userId, Guid deviceId, CancellationToken ct = default)
    {
        var d = await db.Devices.FirstOrDefaultAsync(x => x.Id == deviceId && x.UserId == userId, ct);
        if (d is null) return ServiceResult<bool>.Fail("not_found");
        db.Devices.Remove(d);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<DeviceView>> ListDevicesAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Devices.AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DeviceView(d.Id, d.FcmToken, d.Platform, d.DeviceName, d.AppVersion, d.IsActive, d.LastSeen, d.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>
    /// The one dispatch point on the platform — 23 call sites, this method — which is why the D-263
    /// preference gate lives here rather than at each caller. Every existing and future emitter inherits
    /// it without knowing it exists.
    ///
    /// <para>Channels are decided independently: switching off push must not also swallow the in-app row,
    /// or the user loses the record of something they only muted a buzz for.</para>
    /// </summary>
    public async Task NotifyAsync(Guid userId, string kind, string title, string body, object? data = null, CancellationToken ct = default, string? dedupKey = null)
    {
        var category = NotificationCategories.For(kind);
        var channels = await ResolveChannelsAsync(userId, category, ct);

        // Every channel off is a no-op, not an empty row: persisting an in-app notification the user
        // asked not to receive would put it in the bell they said to leave alone.
        if (!channels.InApp && !channels.Push) return;

        var dataJson = data is null ? null : (data is string str ? str : JsonSerializer.Serialize(data));

        if (channels.InApp)
        {
            var notification = new Notification
            {
                UserId = userId, Kind = kind, Title = title, Body = body, DataJson = dataJson, DedupKey = dedupKey,
            };
            db.Notifications.Add(notification);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (dedupKey is not null && IsDedupConflict(ex))
            {
                // Someone already delivered this exact notification — a concurrent replica, a retried job,
                // or a resumed fan-out. The index is the authority, not the caller's pre-check, which is
                // what makes the race genuinely impossible rather than merely unlikely.
                //
                // Detaching matters: the DbContext is scoped and shared with the rest of this request or
                // job iteration, and leaving a failed insert in the change tracker makes the NEXT
                // SaveChangesAsync retry it and throw again — turning one skipped duplicate into a failed
                // batch of unrelated writes.
                db.Entry(notification).State = EntityState.Detached;
                log.LogDebug("Notification {DedupKey} for user {UserId} already exists; skipping.", dedupKey, userId);
                return;
            }

            var unreadCount = await GetUnreadCountAsync(userId, ct);
            var view = new NotificationView(notification.Id, kind, title, body, dataJson, null, notification.CreatedAt);

            try
            {
                await broadcaster.BroadcastNotificationAsync(userId, view, ct);
                await broadcaster.BroadcastUnreadCountAsync(userId, unreadCount, ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to broadcast notification via SignalR to user {UserId}", userId);
            }
        }

        if (!channels.Push) return;

        var tokens = await db.Devices.AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => d.FcmToken)
            .ToListAsync(ct);

        Dictionary<string, string>? dataDict = null;
        if (dataJson is not null)
        {
            try
            {
                var elements = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson)!;
                dataDict = elements.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to parse notification DataJson as dictionary. Skipping push data payload.");
            }
        }

        foreach (var token in tokens)
        {
            try
            {
                await push.SendAsync(token, title, body, dataDict, ct);
            }
            catch (InvalidFcmTokenException)
            {
                try
                {
                    var dev = await db.Devices.FirstOrDefaultAsync(d => d.FcmToken == token, ct);
                    if (dev is not null)
                    {
                        dev.IsActive = false;
                        await db.SaveChangesAsync(ct);
                    }
                }
                catch (Exception dbEx)
                {
                    log.LogError(dbEx, "Failed to deactivate invalid FCM token {Token}", token);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Push send failed for token {Token} / user {UserId}", token, userId);
            }
        }
    }
}
