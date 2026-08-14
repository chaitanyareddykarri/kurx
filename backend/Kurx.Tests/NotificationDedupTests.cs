using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Kurx.Tests;

/// <summary>DB-3: notification deduplication and bounded cleanup. Real HTTP/Postgres per the suite's
/// integration-first rule — the guarantees under test are a partial unique index and a set-based DELETE,
/// neither of which exists against a mocked store.</summary>
public class NotificationDedupTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public NotificationDedupTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private async Task<Guid> NewUserAsync(string suffix)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = new User
        {
            Phone = $"9199{suffix}", PhoneE164 = $"+9199{suffix}",
            Name = $"Dedup {suffix}", Username = $"dedup{suffix}",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static Notification Row(Guid userId, string kind, string? dedupKey, DateTime? createdAt = null) => new()
    {
        UserId = userId, Kind = kind, Title = "t", Body = "b", DedupKey = dedupKey,
        CreatedAt = createdAt ?? DateTime.UtcNow,
    };

    // ── The uniqueness guarantee ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task First_notification_with_a_dedup_key_is_stored()
    {
        var userId = await NewUserAsync("100001");
        var key = NotificationDedup.ForAnnouncement(Guid.NewGuid());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.Notifications.Add(Row(userId, NotificationDedup.EventAnnouncementKind, key));
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Notifications.CountAsync(n => n.DedupKey == key));
    }

    [Fact]
    public async Task Duplicate_dedup_key_for_the_same_user_is_refused_by_the_database()
    {
        var userId = await NewUserAsync("100002");
        var key = NotificationDedup.ForAnnouncement(Guid.NewGuid());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.Notifications.Add(Row(userId, NotificationDedup.EventAnnouncementKind, key));
        await db.SaveChangesAsync();

        db.Notifications.Add(Row(userId, NotificationDedup.EventAnnouncementKind, key));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Named index, not merely SQLSTATE 23505: NotificationService only swallows a conflict on THIS
        // index, so if the name ever changes the swallow silently stops working and this test is what says so.
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
        Assert.Equal("ix_notifications_dedup", pg.ConstraintName);
    }

    [Fact]
    public async Task Different_recipients_of_the_same_announcement_are_independent()
    {
        var a = await NewUserAsync("100003");
        var b = await NewUserAsync("100004");
        var key = NotificationDedup.ForAnnouncement(Guid.NewGuid());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.Notifications.Add(Row(a, NotificationDedup.EventAnnouncementKind, key));
        db.Notifications.Add(Row(b, NotificationDedup.EventAnnouncementKind, key));
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Notifications.CountAsync(n => n.DedupKey == key));
    }

    [Fact]
    public async Task Different_announcements_to_one_recipient_are_independent()
    {
        var userId = await NewUserAsync("100005");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.Notifications.Add(Row(userId, NotificationDedup.EventAnnouncementKind, NotificationDedup.ForAnnouncement(Guid.NewGuid())));
        db.Notifications.Add(Row(userId, NotificationDedup.EventAnnouncementKind, NotificationDedup.ForAnnouncement(Guid.NewGuid())));
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Notifications.CountAsync(n => n.UserId == userId));
    }

    /// <summary>THE constraint-scope test. Every kind other than the two that dedup must remain repeatable —
    /// an event changes materially more than once, an invitation is re-sent after a decline, an ally request
    /// may be re-made after D-230's 30-day cooldown, an authorization is resubmitted and re-reviewed. A
    /// unique key over (UserId, Kind) would have swallowed all four, which is why DedupKey is nullable and
    /// the index is partial. If someone later makes it non-partial, this test fails.</summary>
    /// <para>The suffix is written down per case rather than derived from the kind, because it used to be
    /// <c>Math.Abs(kind.GetHashCode()) % 100</c> — and <c>string.GetHashCode()</c> is <b>randomized per
    /// process</b> in .NET Core, so the number changed every run and roughly 6% of processes mapped two of
    /// these four cases onto one phone. That surfaced as <c>23505</c> on <c>IX_users_Phone</c> inside
    /// <c>NewUserAsync</c> — a failure that reads like a real regression in whatever change happened to be
    /// in the tree, and that passes 4/4 on re-run in isolation because the next process reseeds the hash.
    /// Every other test in this class already used a literal; this one is now consistent with them.</para>
    [Theory]
    [InlineData("event_material_change", "100101")]
    [InlineData("invitation_received", "100102")]
    [InlineData("ally.requested", "100103")]
    [InlineData("event_authorization_rejected", "100104")]
    public async Task Kinds_that_legitimately_repeat_are_not_constrained(string kind, string phoneSuffix)
    {
        var userId = await NewUserAsync(phoneSuffix);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.Notifications.Add(Row(userId, kind, dedupKey: null));
        db.Notifications.Add(Row(userId, kind, dedupKey: null));
        db.Notifications.Add(Row(userId, kind, dedupKey: null));
        await db.SaveChangesAsync();   // must not throw

        Assert.Equal(3, await db.Notifications.CountAsync(n => n.UserId == userId && n.Kind == kind));
    }

    /// <summary>Concurrency: N parallel writers of the same (user, key) leave exactly one row. The index is
    /// the authority, so this holds without any coordination between the writers.</summary>
    [Fact]
    public async Task Concurrent_duplicate_writers_produce_exactly_one_notification()
    {
        var userId = await NewUserAsync("100006");
        var key = NotificationDedup.ForEventReminder(Guid.NewGuid());

        var writes = Enumerable.Range(0, 6).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.Notifications.Add(Row(userId, NotificationDedup.EventReminderKind, key));
            try { await db.SaveChangesAsync(); return true; }
            catch (DbUpdateException) { return false; }   // the index refused it — the expected outcome
        });
        var results = await Task.WhenAll(writes);

        using var verify = _factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, await vdb.Notifications.CountAsync(n => n.DedupKey == key));
        Assert.Equal(1, results.Count(ok => ok));
    }

    /// <summary>NotifyAsync must treat the conflict as "already delivered", not as an error — and must leave
    /// the shared scoped DbContext usable afterwards. A failed insert left in the change tracker would make
    /// the NEXT SaveChangesAsync retry it and throw, turning one skipped duplicate into a failed batch of
    /// unrelated writes.</summary>
    [Fact]
    public async Task NotifyAsync_swallows_the_duplicate_and_leaves_the_context_usable()
    {
        var userId = await NewUserAsync("100007");
        var key = NotificationDedup.ForEventReminder(Guid.NewGuid());

        using var scope = _factory.Services.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        await notifications.NotifyAsync(userId, NotificationDedup.EventReminderKind, "t", "b", null, default, key);
        await notifications.NotifyAsync(userId, NotificationDedup.EventReminderKind, "t", "b", null, default, key);

        Assert.Equal(1, await db.Notifications.CountAsync(n => n.DedupKey == key));

        // The context still works: an unrelated write on the same scope must succeed.
        db.Notifications.Add(Row(userId, "unrelated_kind", dedupKey: null));
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == userId && n.Kind == "unrelated_kind"));
    }

    // ── Bounded cleanup ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cleanup_deletes_expired_and_keeps_current_notifications()
    {
        var userId = await NewUserAsync("100008");
        var now = DateTime.UtcNow;

        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<KurxDbContext>();
            for (var i = 0; i < 20; i++) db.Notifications.Add(Row(userId, "expired_kind", null, now.AddDays(-31)));
            for (var i = 0; i < 7; i++) db.Notifications.Add(Row(userId, "fresh_kind", null, now.AddDays(-29)));
            await db.SaveChangesAsync();
        }

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<NotificationCleanupJob>().RunAsync(default);

        var db2 = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(0, await db2.Notifications.CountAsync(n => n.UserId == userId && n.Kind == "expired_kind"));
        Assert.Equal(7, await db2.Notifications.CountAsync(n => n.UserId == userId && n.Kind == "fresh_kind"));
    }

    [Fact]
    public async Task Cleanup_is_idempotent()
    {
        var userId = await NewUserAsync("100009");
        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<KurxDbContext>();
            for (var i = 0; i < 5; i++) db.Notifications.Add(Row(userId, "idem_kind", null, DateTime.UtcNow.AddDays(-40)));
            await db.SaveChangesAsync();
        }

        for (var run = 0; run < 3; run++)
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<NotificationCleanupJob>().RunAsync(default);
        }

        using var verify = _factory.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(0, await db2.Notifications.CountAsync(n => n.Kind == "idem_kind"));
    }

    /// <summary>Larger than one batch, so the loop is genuinely exercised rather than short-circuiting on the
    /// first statement. Proves the multi-batch path terminates and deletes everything — the old
    /// ToListAsync/RemoveRange shape would have materialised all of these into the change tracker.</summary>
    [Fact]
    public async Task Cleanup_drains_a_dataset_larger_than_one_batch()
    {
        var userId = await NewUserAsync("100010");
        const int seedCount = 12_000;   // > BatchSize (5,000): forces three statements

        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<KurxDbContext>();
            var stale = DateTime.UtcNow.AddDays(-45);
            for (var i = 0; i < seedCount; i++) db.Notifications.Add(Row(userId, "bulk_kind", null, stale));
            await db.SaveChangesAsync();
        }

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<NotificationCleanupJob>().RunAsync(default);

        var db2 = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(0, await db2.Notifications.CountAsync(n => n.Kind == "bulk_kind"));
    }

    [Fact]
    public async Task Cleanup_honours_cancellation_without_corrupting_unrelated_rows()
    {
        var userId = await NewUserAsync("100011");
        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<KurxDbContext>();
            for (var i = 0; i < 3; i++) db.Notifications.Add(Row(userId, "cancel_expired", null, DateTime.UtcNow.AddDays(-40)));
            for (var i = 0; i < 3; i++) db.Notifications.Add(Row(userId, "cancel_fresh", null, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<NotificationCleanupJob>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.RunAsync(cancelled.Token));

        // Cancelled before the first statement, so nothing was deleted — and critically, the non-expired
        // rows are untouched either way. Each batch is its own transaction, so a cancelled run leaves
        // committed batches committed and no half-state behind.
        var db2 = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(3, await db2.Notifications.CountAsync(n => n.Kind == "cancel_fresh"));
    }
}
