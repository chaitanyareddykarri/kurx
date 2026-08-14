using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kurx.Infrastructure.Events;

public class AnnouncementService(
    KurxDbContext db,
    IEventAuthority authority,
    IEmailSender emailSender,
    IWhatsAppLogService waLog,
    IPushSender push) : IAnnouncementService
{
    private const int MaxPer24h = 10;
    private const int BatchSize = 500;

    public async Task<ServiceResult<AnnouncementView>> CreateAsync(Guid createdBy, Guid eventId,
        string title, string body, string audience, string[] channels,
        DateTime? scheduledAt, bool includeChildEvents, CancellationToken ct = default)
    {
        if (!await IsOwnerOrManagerAsync(createdBy, eventId, ct))
            return ServiceResult<AnnouncementView>.Fail("forbidden");

        var since = DateTime.UtcNow.AddHours(-24);
        var recentCount = await db.EventAnnouncements
            .CountAsync(a => a.EventId == eventId && a.CreatedAt >= since
                && a.Status != AnnouncementStatus.Cancelled, ct);
        if (recentCount >= MaxPer24h)
            return ServiceResult<AnnouncementView>.Fail("rate_limited");

        if (!Enum.TryParse<AnnouncementAudience>(audience, true, out var audienceEnum))
            return ServiceResult<AnnouncementView>.Fail("invalid_audience");

        var ann = new EventAnnouncement
        {
            EventId = eventId, CreatedBy = createdBy,
            Title = title[..Math.Min(title.Length, 120)],
            Body = body[..Math.Min(body.Length, 2000)],
            Audience = audienceEnum,
            IncludeChildEvents = includeChildEvents,
            Channels = channels.Where(c => c is "push" or "email" or "whatsapp").ToArray(),
            ScheduledAt = scheduledAt,
            Status = AnnouncementStatus.Queued,
        };
        db.EventAnnouncements.Add(ann);
        await db.SaveChangesAsync(ct);

        if (scheduledAt is null || scheduledAt <= DateTime.UtcNow)
            _ = Task.Run(() => SendAsync(ann.Id, CancellationToken.None), CancellationToken.None);

        return ServiceResult<AnnouncementView>.Success(ToView(ann));
    }

    public async Task<ServiceResult<List<AnnouncementView>>> ListAsync(Guid userId, Guid eventId, CancellationToken ct = default)
    {
        if (!await IsOwnerOrManagerAsync(userId, eventId, ct))
            return ServiceResult<List<AnnouncementView>>.Fail("forbidden");
        var list = await db.EventAnnouncements
            .Where(a => a.EventId == eventId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        return ServiceResult<List<AnnouncementView>>.Success(list.Select(ToView).ToList());
    }

    public async Task<ServiceResult<AnnouncementView>> GetAsync(Guid userId, Guid announcementId, CancellationToken ct = default)
    {
        var ann = await db.EventAnnouncements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == announcementId, ct);
        if (ann is null) return ServiceResult<AnnouncementView>.Fail("not_found");
        if (!await IsOwnerOrManagerAsync(userId, ann.EventId, ct)) return ServiceResult<AnnouncementView>.Fail("forbidden");
        return ServiceResult<AnnouncementView>.Success(ToView(ann));
    }

    public async Task<ServiceResult<bool>> CancelAsync(Guid userId, Guid announcementId, CancellationToken ct = default)
    {
        var ann = await db.EventAnnouncements.FirstOrDefaultAsync(a => a.Id == announcementId, ct);
        if (ann is null) return ServiceResult<bool>.Fail("not_found");
        if (!await IsOwnerOrManagerAsync(userId, ann.EventId, ct)) return ServiceResult<bool>.Fail("forbidden");
        if (ann.Status != AnnouncementStatus.Queued) return ServiceResult<bool>.Fail("cannot_cancel");
        ann.Status = AnnouncementStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<AnnouncementView>> UpdateAsync(Guid userId, Guid announcementId, string? title, string? body, DateTime? scheduledAt, CancellationToken ct = default)
    {
        var ann = await db.EventAnnouncements.FirstOrDefaultAsync(a => a.Id == announcementId, ct);
        if (ann is null) return ServiceResult<AnnouncementView>.Fail("not_found");
        if (!await IsOwnerOrManagerAsync(userId, ann.EventId, ct)) return ServiceResult<AnnouncementView>.Fail("forbidden");
        if (ann.Status != AnnouncementStatus.Queued) return ServiceResult<AnnouncementView>.Fail("cannot_update");

        if (title is not null) ann.Title = title[..Math.Min(title.Length, 120)];
        if (body is not null) ann.Body = body[..Math.Min(body.Length, 2000)];
        ann.ScheduledAt = scheduledAt;
        await db.SaveChangesAsync(ct);

        if (scheduledAt is null || scheduledAt <= DateTime.UtcNow)
            _ = Task.Run(() => SendAsync(ann.Id, CancellationToken.None), CancellationToken.None);

        return ServiceResult<AnnouncementView>.Success(ToView(ann));
    }

    public async Task SendAsync(Guid announcementId, CancellationToken ct = default)
    {
        var ann = await db.EventAnnouncements.FirstOrDefaultAsync(a => a.Id == announcementId, ct);
        if (ann is null || ann.Status != AnnouncementStatus.Queued) return;

        ann.Status = AnnouncementStatus.Sending;
        await db.SaveChangesAsync(ct);

        try
        {
            var eventIds = new List<Guid> { ann.EventId };
            if (ann.IncludeChildEvents)
            {
                var childIds = await db.Events.Where(e => e.ParentEventId == ann.EventId)
                    .Select(e => e.Id).ToListAsync(ct);
                eventIds.AddRange(childIds);
            }

            var ticketQuery = db.Tickets.Where(t => eventIds.Contains(t.EventId) && t.UserId != null);
            ticketQuery = ann.Audience switch
            {
                AnnouncementAudience.CheckedIn => ticketQuery.Where(t => t.State == TicketState.CheckedIn),
                AnnouncementAudience.NotCheckedIn => ticketQuery.Where(t => t.State == TicketState.Issued),
                _ => ticketQuery,
            };

            var recipientIds = await ticketQuery.Select(t => t.UserId!.Value).Distinct().ToListAsync(ct);
            ann.TotalRecipients = recipientIds.Count;
            await db.SaveChangesAsync(ct);

            int sentPush = 0, sentEmail = 0, sentWa = 0, failed = 0;
            var bodyExcerpt = ann.Body.Length > 100 ? ann.Body[..100] + "…" : ann.Body;

            // Pre-load already-sent email addresses for idempotency
            var alreadySentEmails = (await db.EmailLogs
                .Where(l => l.RelatedType == "announcement" && l.RelatedId == ann.Id && l.Status != EmailStatus.Failed)
                .Select(l => l.ToEmail).ToListAsync(ct)).ToHashSet();

            // Pre-load already-sent WA phones for idempotency
            // Both sides of this comparison are canonicalized, because they have different provenance:
            // rows written before the send path canonicalized hold bare digits, newer ones hold E.164.
            // Comparing the two forms directly would find no match and re-send the announcement to
            // everyone who had already received it.
            var alreadySentPhones = (await db.WhatsAppMessages
                .Where(m => m.RelatedType == "announcement" && m.RelatedId == ann.Id
                    && m.Status != WhatsAppMessageStatus.Failed)
                .Select(m => m.ToPhone).ToListAsync(ct))
                .Select(PhoneCanonicalizer.ToE164OrUnchanged).ToHashSet();

            // Pre-load users who already received the in-app notification (DB-3).
            //
            // This was `n.Kind == "event_announcement" && n.DataJson.Contains(annIdStr)` — a substring
            // match against a jsonb column, on a table with no index on Kind. EXPLAIN confirmed a Seq Scan
            // over the whole notifications table on every send, and no index could have helped: a
            // leading-wildcard LIKE against jsonb::text is not indexable by any btree.
            //
            // The dedup key replaces it with an equality probe on ix_notifications_dedup. Semantics are
            // unchanged — one in-app notification per recipient per announcement — but the guarantee is
            // now the index's rather than this pre-check's, so a resumed or concurrent send cannot
            // double-deliver even if two runs read this set at the same moment.
            var dedupKey = NotificationDedup.ForAnnouncement(ann.Id);
            var alreadyNotified = (await db.Notifications.AsNoTracking()
                .Where(n => n.DedupKey == dedupKey)
                .Select(n => n.UserId).ToListAsync(ct)).ToHashSet();

            for (int offset = 0; offset < recipientIds.Count; offset += BatchSize)
            {
                var batch = recipientIds.Skip(offset).Take(BatchSize).ToList();

                // In-app notification (always, idempotent)
                foreach (var uid in batch.Where(uid => !alreadyNotified.Contains(uid)))
                {
                    db.Notifications.Add(new Notification
                    {
                        UserId = uid, Kind = NotificationDedup.EventAnnouncementKind,
                        Title = ann.Title, Body = ann.Body,
                        // DataJson keeps its exact shape: web and mobile both parse announcement_id and
                        // event_id out of it. DedupKey is ADDITIVE alongside it, never a replacement —
                        // changing this payload would break clients that read it.
                        DataJson = $"{{\"announcement_id\":\"{ann.Id}\",\"event_id\":\"{ann.EventId}\"}}",
                        DedupKey = dedupKey,
                    });
                    alreadyNotified.Add(uid);
                }
                await SaveNotificationBatchAsync(dedupKey, alreadyNotified, ct);

                // Push
                if (ann.Channels.Contains("push"))
                {
                    var fcmTokens = await db.Devices
                        .Where(d => batch.Contains(d.UserId))
                        .Select(d => d.FcmToken).ToListAsync(ct);
                    foreach (var token in fcmTokens)
                    {
                        try { await push.SendAsync(token, ann.Title, bodyExcerpt, null, ct); sentPush++; }
                        catch { failed++; }
                    }
                }

                // Email
                if (ann.Channels.Contains("email"))
                {
                    var userEmails = await db.Users
                        .Where(u => batch.Contains(u.Id) && u.Email != null)
                        .Select(u => u.Email!).ToListAsync(ct);

                    foreach (var toEmail in userEmails.Where(e => !alreadySentEmails.Contains(e)))
                    {
                        try
                        {
                            await emailSender.SendAsync(toEmail, ann.Title,
                                $"<p><strong>{ann.Title}</strong></p><p>{ann.Body}</p>", null, ct);
                            db.EmailLogs.Add(new EmailLog
                            {
                                ToEmail = toEmail, Template = "event_update_email",
                                Subject = ann.Title, Status = EmailStatus.Sent,
                                RelatedType = "announcement", RelatedId = ann.Id,
                            });
                            alreadySentEmails.Add(toEmail);
                            sentEmail++;
                        }
                        catch
                        {
                            db.EmailLogs.Add(new EmailLog
                            {
                                ToEmail = toEmail, Template = "event_update_email",
                                Subject = ann.Title, Status = EmailStatus.Failed,
                                RelatedType = "announcement", RelatedId = ann.Id,
                            });
                            failed++;
                        }
                    }
                    await db.SaveChangesAsync(ct);
                }

                // WhatsApp
                if (ann.Channels.Contains("whatsapp"))
                {
                    var phones = (await db.Users
                        .Where(u => batch.Contains(u.Id))
                        .Select(u => u.PhoneE164 ?? u.Phone).ToListAsync(ct))
                        .Select(PhoneCanonicalizer.ToE164OrUnchanged).ToList();

                    foreach (var phone in phones.Where(p => !alreadySentPhones.Contains(p)))
                    {
                        try
                        {
                            await waLog.SendAndLogAsync(phone, $"{ann.Title}\n{bodyExcerpt}",
                                WhatsAppMessageKind.Generic, "announcement", ann.Id, ct: ct);
                            alreadySentPhones.Add(phone);
                            sentWa++;
                        }
                        catch { failed++; }
                    }
                }

                ann.SentPush = sentPush;
                ann.SentEmail = sentEmail;
                ann.SentWhatsapp = sentWa;
                ann.FailedCount = failed;
                await db.SaveChangesAsync(ct);
            }

            var failedRatio = ann.TotalRecipients > 0 ? (double)failed / ann.TotalRecipients : 0;
            ann.Status = failedRatio > 0.5 ? AnnouncementStatus.Failed : AnnouncementStatus.Sent;
            ann.SentAt = DateTime.UtcNow;

            db.AuditLogs.Add(new AuditLog
            {
                ActorType = "user", ActorId = ann.CreatedBy, Action = "announcement.send",
                Entity = "event_announcements", EntityId = ann.Id,
                DetailsJson = $"{{\"recipients\":{ann.TotalRecipients},\"push\":{sentPush},\"email\":{sentEmail},\"whatsapp\":{sentWa},\"failed\":{failed}}}",
            });
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            if (db.Database.CurrentTransaction is null)
            {
                ann.Status = AnnouncementStatus.Failed;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>D-269: delegates to the single event authority. **No admin bypass** — preserved exactly,
    /// because these endpoints never passed one.</summary>
    /// <summary>Saves a batch of announcement notifications, tolerating the dedup index (DB-3).
    ///
    /// <para><b>Why this is needed.</b> <c>SendAsync</c> claims the announcement with a read-then-write
    /// (<c>if (ann.Status != Queued)</c> … then assign), not an atomic claim, and it is invoked
    /// fire-and-forget via <c>Task.Run</c>. Two sends of one announcement are therefore reachable — two
    /// clicks, or a scheduled send racing a manual one. Before the dedup index that produced duplicate
    /// notifications: bad, but each insert still succeeded. With the index, one conflicting row would abort
    /// the ENTIRE batch of up to 500, leaving the announcement stuck in <c>Sending</c> with partial
    /// delivery. Turning a cosmetic duplicate into a failed send would be a regression caused by the fix.</para>
    ///
    /// <para>The retry re-reads the delivered set rather than trusting the in-memory one, because the whole
    /// reason it is here is that another writer landed rows this process has not seen. The underlying
    /// read-then-write claim is a pre-existing weakness and is deliberately NOT changed here — that is a
    /// concurrency fix with its own semantics, not part of a query-performance phase.</para></summary>
    private async Task SaveNotificationBatchAsync(string dedupKey, HashSet<Guid> alreadyNotified, CancellationToken ct)
    {
        var pending = db.ChangeTracker.Entries<Notification>()
            .Where(e => e.State == EntityState.Added).ToList();
        try
        {
            await db.SaveChangesAsync(ct);
            return;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_notifications_dedup" })
        {
            foreach (var entry in pending) entry.State = EntityState.Detached;
        }

        // Another sender delivered some of these. Re-establish the truth from the database and re-add only
        // what is genuinely still missing.
        var delivered = await db.Notifications.AsNoTracking()
            .Where(n => n.DedupKey == dedupKey).Select(n => n.UserId).ToListAsync(ct);
        foreach (var uid in delivered) alreadyNotified.Add(uid);

        var remaining = pending
            .Select(e => e.Entity)
            .Where(n => !delivered.Contains(n.UserId))
            .ToList();
        if (remaining.Count == 0) return;

        db.Notifications.AddRange(remaining);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_notifications_dedup" })
        {
            // A third writer landed between the re-read and this save. Every row here is one someone else
            // has now delivered, which is the desired end state — drop them rather than loop.
            foreach (var n in remaining) db.Entry(n).State = EntityState.Detached;
        }
    }

    private async Task<bool> IsOwnerOrManagerAsync(Guid userId, Guid eventId, CancellationToken ct)
        => (await authority.ResolveAsync(userId, eventId, isAdmin: false, ct)).Can(EventPermission.ManageContent);

    private static AnnouncementView ToView(EventAnnouncement a) => new(
        a.Id, a.EventId, a.Title, a.Body, a.Audience.ToString(),
        a.IncludeChildEvents, a.Channels, a.Status.ToString(),
        a.ScheduledAt, a.TotalRecipients,
        a.SentPush, a.SentEmail, a.SentWhatsapp, a.FailedCount,
        a.SentAt, a.CreatedAt);
}
