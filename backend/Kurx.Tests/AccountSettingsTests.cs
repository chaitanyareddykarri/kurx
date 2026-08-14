using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Kurx.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// Account settings (D-263).
///
/// <para>Two of these carry the weight. <b>Preferences must actually suppress dispatch</b> — a
/// preferences table no dispatcher reads is decoration, and this repo has shipped that shape before.
/// And <b>a block must actually hide things in both directions</b>, because a block that only hides a
/// name is a lie told to the person who asked for protection.</para>
/// </summary>
public class AccountSettingsTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public AccountSettingsTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9186{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync()
    {
        var phone = NextPhone();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private static async Task<Guid> CreatePostAsync(HttpClient client, string body)
    {
        var res = await client.PostAsJsonAsync("/v1/posts", new { body, visibility = "public" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    /// <summary>Runs work inside a DI scope and awaits it BEFORE the scope disposes. Returning the
    /// resolved service instead would hand back a live object whose DbContext is already gone.</summary>
    private async Task<T> ScopedAsync<T>(Func<IServiceProvider, Task<T>> f)
    {
        using var scope = _factory.Services.CreateScope();
        return await f(scope.ServiceProvider);
    }

    // ── Notification preferences ───────────────────────────────────────────────

    [Fact]
    public async Task Preferences_default_to_everything_on_except_whatsapp()
    {
        var (client, _) = await LoginAsync();
        var view = await Json(await client.GetAsync("/v1/me/notification-preferences"));
        var categories = view.GetProperty("categories").EnumerateArray().ToList();

        Assert.Equal(12, categories.Count);
        foreach (var c in categories)
        {
            Assert.True(c.GetProperty("in_app").GetBoolean(), $"{c.GetProperty("category")} in_app");
            Assert.True(c.GetProperty("push").GetBoolean());
            // WhatsApp costs money per message and reaches a surface users treat as personal, so it is
            // the one channel that is opt-IN.
            var whatsApp = c.GetProperty("whats_app").GetBoolean();
            var locked = c.GetProperty("locked").GetBoolean();
            Assert.True(locked ? whatsApp : !whatsApp);
        }

        var security = categories.Single(c => c.GetProperty("category").GetString() == "security");
        Assert.True(security.GetProperty("locked").GetBoolean());
    }

    /// <summary>The whole point of the table. A preference nothing reads is decoration.</summary>
    [Fact]
    public async Task Turning_a_category_off_actually_suppresses_the_notification()
    {
        var (author, _) = await LoginAsync();
        var (reader, readerId) = await LoginAsync();
        var postId = await CreatePostAsync(author, "comment on me");

        // Baseline: with no preference row, the author is notified.
        await reader.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "before" });
        Assert.Equal(1, await CountNotificationsAsync(await AuthorIdOf(postId), "post_comment"));

        // The AUTHOR switches Posts notifications off.
        var patch = await author.PatchAsJsonAsync("/v1/me/notification-preferences", new
        {
            categories = new[] { new { category = "posts", inApp = false, push = false } },
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        await reader.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "after" });

        // Still one — the second comment produced no row at all.
        Assert.Equal(1, await CountNotificationsAsync(await AuthorIdOf(postId), "post_comment"));
        Assert.NotEqual(Guid.Empty, readerId);
    }

    /// <summary>Channels are independent: muting the buzz must not also swallow the record.</summary>
    [Fact]
    public async Task Disabling_push_alone_still_records_the_in_app_notification()
    {
        var (author, authorId) = await LoginAsync();
        var (reader, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "half muted");

        await author.PatchAsJsonAsync("/v1/me/notification-preferences", new
        {
            categories = new[] { new { category = "posts", push = false } },
        });

        await reader.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "hi" });
        Assert.Equal(1, await CountNotificationsAsync(authorId, "post_comment"));
    }

    /// <summary>A PATCH naming one channel must not silently switch the other three off for a user who
    /// had no stored row — the row has to materialise from the DEFAULT, not from all-false.</summary>
    [Fact]
    public async Task Patching_one_channel_leaves_the_others_at_their_defaults()
    {
        var (client, _) = await LoginAsync();
        await client.PatchAsJsonAsync("/v1/me/notification-preferences", new
        {
            categories = new[] { new { category = "payments", whatsApp = true } },
        });

        var payments = (await Json(await client.GetAsync("/v1/me/notification-preferences")))
            .GetProperty("categories").EnumerateArray()
            .Single(c => c.GetProperty("category").GetString() == "payments");

        Assert.True(payments.GetProperty("whats_app").GetBoolean());
        Assert.True(payments.GetProperty("in_app").GetBoolean());
        Assert.True(payments.GetProperty("push").GetBoolean());
        Assert.True(payments.GetProperty("email").GetBoolean());
    }

    /// <summary>Security notices are what tell a user they are being attacked, so consent to silence
    /// them is not consent the server accepts — and it refuses rather than silently ignoring, because a
    /// screen that appears to accept a change it discards is worse than one that says no.</summary>
    [Fact]
    public async Task Security_notifications_cannot_be_switched_off()
    {
        var (client, _) = await LoginAsync();
        var res = await client.PatchAsJsonAsync("/v1/me/notification-preferences", new
        {
            categories = new[] { new { category = "security", inApp = false, push = false } },
        });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("category_not_optional", (await Json(res)).GetProperty("error").GetString());
    }

    /// <summary>Belt and braces: even a row written straight to the database — bypassing the API guard
    /// entirely — must not silence a security notice, because dispatch checks independently.</summary>
    [Fact]
    public async Task A_forged_security_preference_row_still_does_not_suppress_dispatch()
    {
        var (_, userId) = await LoginAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.NotificationPreferences.Add(new NotificationPreference
            {
                UserId = userId, Category = NotificationCategory.Security,
                InApp = false, Push = false, Email = false, WhatsApp = false,
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifications.NotifyAsync(userId, "security.new_device", "New sign-in", "A new device signed in.");
        }

        Assert.Equal(1, await CountNotificationsAsync(userId, "security.new_device"));
    }

    [Fact]
    public async Task An_unknown_category_is_rejected()
    {
        var (client, _) = await LoginAsync();
        var res = await client.PatchAsJsonAsync("/v1/me/notification-preferences", new
        {
            categories = new[] { new { category = "telepathy", inApp = false } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_category", (await Json(res)).GetProperty("error").GetString());
    }

    /// <summary>An unmapped kind falls back to System rather than to "send anyway" — a notification
    /// nobody mapped is one the user was never offered a choice about.</summary>
    [Fact]
    public void Kind_to_category_mapping_covers_the_platform_and_defaults_safely()
    {
        Assert.Equal(NotificationCategory.Posts, NotificationCategories.For("post_like"));
        Assert.Equal(NotificationCategory.Messages, NotificationCategories.For("chat_mention"));
        Assert.Equal(NotificationCategory.ConnectionRequests, NotificationCategories.For("ally.requested"));
        Assert.Equal(NotificationCategory.Announcements, NotificationCategories.For("event_announcement"));
        // Longest-prefix wins: event_announcement must not be swallowed by the shorter `event_`.
        Assert.Equal(NotificationCategory.EventUpdates, NotificationCategories.For("event_reminder"));
        Assert.Equal(NotificationCategory.Security, NotificationCategories.For("phone_change_requested"));
        Assert.Equal(NotificationCategory.System, NotificationCategories.For("something_nobody_mapped"));
        Assert.Equal(NotificationCategory.System, NotificationCategories.For(null));
    }

    // ── Blocks ─────────────────────────────────────────────────────────────────

    /// <summary>A one-way row, enforced symmetrically: the blocker stops seeing the blocked, AND the
    /// blocked stops seeing the blocker. Hiding only one direction would leave the blocked party still
    /// reading and still able to reach out.</summary>
    [Fact]
    public async Task A_block_hides_posts_in_both_directions()
    {
        var (alice, aliceId) = await LoginAsync();
        var (bob, bobId) = await LoginAsync();

        var alicePost = await CreatePostAsync(alice, "alice speaks");
        var bobPost = await CreatePostAsync(bob, "bob speaks");

        // Both visible before the block.
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync($"/v1/posts/{alicePost}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync($"/v1/posts/{bobPost}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync($"/v1/me/blocks/{bobId}", new { })).StatusCode);

        // 404 both ways (D-018) — the block does not announce itself.
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/v1/posts/{alicePost}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/v1/posts/{bobPost}")).StatusCode);
        Assert.NotEqual(Guid.Empty, aliceId);
    }

    [Fact]
    public async Task A_block_hides_the_blocked_users_comments()
    {
        var (author, _) = await LoginAsync();
        var (blocked, blockedId) = await LoginAsync();
        var postId = await CreatePostAsync(author, "thread");

        await blocked.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "visible for now" });
        Assert.Single((await Json(await author.GetAsync($"/v1/posts/{postId}/comments")))
            .GetProperty("items").EnumerateArray().ToList());

        await author.PostAsJsonAsync($"/v1/me/blocks/{blockedId}", new { });

        Assert.Empty((await Json(await author.GetAsync($"/v1/posts/{postId}/comments")))
            .GetProperty("items").EnumerateArray().ToList());
    }

    [Fact]
    public async Task Blocking_is_idempotent_and_listed_and_reversible()
    {
        var (alice, _) = await LoginAsync();
        var (_, bobId) = await LoginAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync($"/v1/me/blocks/{bobId}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync($"/v1/me/blocks/{bobId}", new { })).StatusCode);

        var listed = (await Json(await alice.GetAsync("/v1/me/blocks"))).EnumerateArray().ToList();
        Assert.Single(listed);
        Assert.Equal(bobId, listed[0].GetProperty("user_id").GetGuid());

        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/v1/me/blocks/{bobId}")).StatusCode);
        Assert.Empty((await Json(await alice.GetAsync("/v1/me/blocks"))).EnumerateArray().ToList());
        // Unblocking someone who was never blocked is the state the caller asked for.
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/v1/me/blocks/{bobId}")).StatusCode);
    }

    [Fact]
    public async Task Blocking_yourself_is_refused()
    {
        var (alice, aliceId) = await LoginAsync();
        var res = await alice.PostAsJsonAsync($"/v1/me/blocks/{aliceId}", new { });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("cannot_block_self", (await Json(res)).GetProperty("error").GetString());
    }

    /// <summary>A pending ally request from either side is declined by the block — leaving it outstanding
    /// would keep the blocked party sitting in the other's inbox, which is the thing being stopped.</summary>
    [Fact]
    public async Task Blocking_declines_a_pending_ally_request()
    {
        var (alice, aliceId) = await LoginAsync();
        var (bob, bobId) = await LoginAsync();

        var req = await bob.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = aliceId });
        Assert.Equal(HttpStatusCode.OK, req.StatusCode);

        await alice.PostAsJsonAsync($"/v1/me/blocks/{bobId}", new { });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var (low, high) = aliceId.CompareTo(bobId) < 0 ? (aliceId, bobId) : (bobId, aliceId);
        var row = await db.AllyConnections.AsNoTracking().FirstAsync(a => a.UserLowId == low && a.UserHighId == high);
        Assert.Equal(AllyStatus.Declined, row.Status);
    }

    // ── Username history ───────────────────────────────────────────────────────

    [Fact]
    public async Task Username_history_is_a_read_only_projection_of_released_handles()
    {
        var (client, userId) = await LoginAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.UsernameHistory.Add(new UsernameHistory
            {
                UserId = userId, Username = $"old{Guid.NewGuid():N}"[..10], ReleasedAt = DateTime.UtcNow.AddDays(-3),
            });
            await db.SaveChangesAsync();
        }

        var history = (await Json(await client.GetAsync("/v1/me/username-history"))).EnumerateArray().ToList();
        Assert.Single(history);
        Assert.False(string.IsNullOrEmpty(history[0].GetProperty("username").GetString()));
        Assert.NotEqual(JsonValueKind.Undefined, history[0].GetProperty("released_at").ValueKind);
    }

    // ── Language ───────────────────────────────────────────────────────────────

    /// <summary>Language extends the existing profile PATCH rather than getting its own endpoint, and
    /// the value is allow-listed — an unrecognised culture would silently fall back to English while
    /// the API reported success.</summary>
    [Fact]
    public async Task Language_is_set_through_the_profile_patch_and_only_accepts_known_cultures()
    {
        var (client, userId) = await LoginAsync();

        Assert.Equal(HttpStatusCode.OK,
            (await client.PatchAsJsonAsync("/v1/me/profile", new { language = "hi" })).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            Assert.Equal("hi", (await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId)).Language);
        }

        var bad = await client.PatchAsJsonAsync("/v1/me/profile", new { language = "klingon" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("invalid_language", (await Json(bad)).GetProperty("error").GetString());
    }

    // ── Deletion (India DPDP) ──────────────────────────────────────────────────

    [Fact]
    public async Task No_deletion_request_answers_404_rather_than_a_false_body()
    {
        var (client, _) = await LoginAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/v1/me/deletion")).StatusCode);
    }

    [Fact]
    public async Task Requesting_deletion_schedules_it_and_cancelling_during_grace_clears_it()
    {
        var (client, userId) = await LoginAsync();

        var requested = await Json(await client.PostAsJsonAsync("/v1/me/deletion", new { reason = "moving on" }));
        Assert.True(requested.GetProperty("pending").GetBoolean());
        var scheduled = requested.GetProperty("scheduled_for").GetDateTime();
        Assert.InRange(scheduled, DateTime.UtcNow.AddDays(AccountService.DeletionGraceDays - 1),
            DateTime.UtcNow.AddDays(AccountService.DeletionGraceDays + 1));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/v1/me/deletion")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/v1/me/deletion")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/v1/me/deletion")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        Assert.Null(user.DeletionScheduledFor);
        Assert.Null(user.AnonymizedAt);
    }

    /// <summary>The retention boundary, which is the part that is worse to get wrong than to not ship.
    /// PII goes; orders, tickets and audit logs stay, because their retention is a legal obligation that
    /// the right to erasure does not override.</summary>
    [Fact]
    public async Task Anonymisation_clears_pii_but_retains_orders_tickets_and_audit_logs()
    {
        var (client, userId) = await LoginAsync();
        var postId = await CreatePostAsync(client, "something I said");

        Guid orderId, ticketId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == userId);
            user.Email = $"gone{Guid.NewGuid():N}"[..16] + "@example.com";
            user.Username = $"handle{Guid.NewGuid():N}"[..14];
            user.Bio = "a bio";

            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Del", Slug = $"del-{Guid.NewGuid():N}"[..12] };
            db.EventCategories.Add(category);
            await db.SaveChangesAsync();

            var orgId = _factory.SeedVerifiedOrg(userId, $"Del Org {Guid.NewGuid():N}"[..18]);
            var ev = new Event
            {
                RepresentingOrgId = orgId, CreatedBy = userId, CategoryId = category.Id,
                Title = "Del Event", Slug = $"del-{Guid.NewGuid():N}"[..16], ShortCode = $"D{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
                Description = "d", VenueName = "v",
                StartsAt = DateTime.UtcNow.AddDays(2), EndsAt = DateTime.UtcNow.AddDays(3),
                Status = EventStatus.Published,
            };
            db.Events.Add(ev);
            var tt = new TicketType
            {
                EventId = ev.Id, Name = "General", PricePaise = 0, Quantity = 10,
                SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(20),
            };
            db.TicketTypes.Add(tt);
            var order = new Order { UserId = userId, EventId = ev.Id, TicketTypeId = tt.Id, Status = OrderStatus.Paid, AmountPaise = 0 };
            db.Orders.Add(order);
            var item = new OrderItem { OrderId = order.Id, TicketTypeId = tt.Id, Qty = 1, UnitPricePaise = 0 };
            db.OrderItems.Add(item);
            var ticket = new Ticket { OrderItemId = item.Id, EventId = ev.Id, UserId = userId, HmacSig = "seed", State = TicketState.Issued };
            db.Tickets.Add(ticket);
            db.AuditLogs.Add(new AuditLog { ActorType = "user", ActorId = userId, Action = "test.seed", Entity = "users", EntityId = userId });
            await db.SaveChangesAsync();
            orderId = order.Id;
            ticketId = ticket.Id;
        }

        // Request deletion, then drag the clock past the grace window and run the sweep.
        await client.PostAsJsonAsync("/v1/me/deletion", new { reason = "done" });
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletionScheduledFor, DateTime.UtcNow.AddMinutes(-1)));
        }

        Assert.True(await ScopedAsync(sp => sp.GetRequiredService<IAccountService>().RunScheduledDeletionsAsync()) >= 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);

            // Cleared.
            Assert.Equal("Deleted user", user.Name);
            Assert.Null(user.Email);
            Assert.Null(user.Username);
            Assert.Null(user.Bio);
            Assert.Null(user.PhoneE164);
            Assert.NotNull(user.AnonymizedAt);
            Assert.Null(user.DeletionScheduledFor);

            // The user's own speech goes with them.
            Assert.True((await db.Posts.AsNoTracking().FirstAsync(p => p.Id == postId)).IsDeleted);

            // Retained by design — statutory retention outlives the account.
            Assert.True(await db.Orders.AsNoTracking().AnyAsync(o => o.Id == orderId));
            Assert.True(await db.Tickets.AsNoTracking().AnyAsync(t => t.Id == ticketId));
            Assert.True(await db.AuditLogs.AsNoTracking().AnyAsync(a => a.ActorId == userId && a.Action == "test.seed"));
        }
    }

    /// <summary>A cancel racing the sweep must not resurrect a half-cleared account — the guard is in the
    /// WHERE, evaluated under the row lock.</summary>
    [Fact]
    public async Task Cancelling_after_anonymisation_does_not_resurrect_the_account()
    {
        var (client, userId) = await LoginAsync();
        await client.PostAsJsonAsync("/v1/me/deletion", new { reason = "x" });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletionScheduledFor, DateTime.UtcNow.AddMinutes(-1)));
        }
        await ScopedAsync(sp => sp.GetRequiredService<IAccountService>().RunScheduledDeletionsAsync());

        Assert.False(await ScopedAsync(sp => sp.GetRequiredService<IAccountService>().CancelDeletionAsync(userId)));

        using var check = _factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.NotNull((await db2.Users.AsNoTracking().FirstAsync(u => u.Id == userId)).AnonymizedAt);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task<int> CountNotificationsAsync(Guid userId, string kind)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && n.Kind == kind);
    }

    private async Task<Guid> AuthorIdOf(Guid postId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Posts.AsNoTracking().Where(p => p.Id == postId).Select(p => p.AuthorId).FirstAsync();
    }
}
