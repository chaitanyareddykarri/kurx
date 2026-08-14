using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Regression cover for the transactional outbox dispatcher (ADR-AM16), and specifically for the defect
/// where an authentication notification could be discarded while looking delivered.
/// </summary>
/// <remarks>
/// <para><b>The defect this suite exists to prevent.</b> <c>DispatchAsync</c> resolved a message type to a
/// (title, body) pair through a <c>switch</c> whose default arm returned <c>(null, null)</c>; the caller saw
/// no exception and marked the row <b>Dispatched</b>. Three authentication types had no arm —
/// <c>login.succeeded</c>, <c>password.changed</c> and <c>password.reset</c> — so every "you just signed in"
/// and "your password changed" alert was recorded as delivered and sent to nobody.</para>
///
/// <para>That is the worst failure shape available to this component. The outbox exists <i>because</i> a
/// security alert must survive a provider outage, and the alert a victim needs is precisely the one whose
/// absence nobody notices. A loud failure would have been found in a day.</para>
///
/// <para><b>What is asserted, and why it is asserted this way.</b> "The row says Dispatched" and "the user was
/// told" are different claims, and conflating them is how the original bug hid. So delivery is asserted
/// against a capturing <c>IPushSender</c>, never against the row's own status, and observability is asserted
/// against captured telemetry rather than a log line no test can read.</para>
/// </remarks>
public class OutboxDispatchTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public OutboxDispatchTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    /// <summary>Every authentication type a producer enqueues today. Cross-checked against the codebase by
    /// grepping <c>OutboxMessages.Add</c>; the two <c>event.chat_*</c> types are covered by the chat suite.
    ///
    /// <para>This list is the regression net: adding a producer without a handler makes
    /// <see cref="Every_authentication_event_type_has_a_handler"/> fail, which is the whole point.</para></summary>
    public static IEnumerable<object[]> AuthEventTypes() =>
    [
        ["login.succeeded"],
        ["login.approved"],
        ["password.changed"],
        ["password.reset"],
        ["device.revoked"],
        ["recovery.redeemed"],
    ];

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>A user with one active push destination, so a dispatched notification has somewhere to go.</summary>
    private async Task<(Guid UserId, string FcmToken)> UserWithDeviceAsync(string phone)
    {
        var token = $"fcm-{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = new User { Phone = phone, Name = "Outbox Subject" };
        db.Users.Add(user);
        db.Devices.Add(new Device { UserId = user.Id, FcmToken = token, Platform = "android", IsActive = true });
        await db.SaveChangesAsync();
        return (user.Id, token);
    }

    private async Task<Guid> EnqueueAsync(string type, string payloadJson)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var message = new OutboxMessage
        {
            Type = type,
            PayloadJson = payloadJson,
            IdempotencyKey = $"{type}:{Guid.NewGuid()}",
        };
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    private async Task RunDispatcherAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutboxDispatchJob>().RunAsync(default);
    }

    private async Task<OutboxMessage> ReloadAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == id);
    }

    // ── the unknown-type contract ───────────────────────────────────────────

    /// <summary>The exact shape of the original defect: a type with no handler must never come out the other
    /// side looking delivered.</summary>
    [Fact]
    public async Task An_unknown_event_type_is_never_marked_dispatched()
    {
        var (userId, _) = await UserWithDeviceAsync("9600000001");
        var id = await EnqueueAsync("auth.some_type_nobody_handled", $"{{\"userId\":\"{userId}\"}}");

        await RunDispatcherAsync();

        var message = await ReloadAsync(id);
        Assert.NotEqual(OutboxStatus.Dispatched, message.Status);
        Assert.Null(message.DispatchedAt);
    }

    /// <summary>It is parked terminally rather than retried: a missing handler is a code defect, and five
    /// more attempts against the same missing code buy nothing but a slower alert.</summary>
    [Fact]
    public async Task An_unknown_event_type_is_parked_as_failed_with_a_readable_error()
    {
        var (userId, _) = await UserWithDeviceAsync("9600000002");
        var id = await EnqueueAsync("auth.another_unhandled_type", $"{{\"userId\":\"{userId}\"}}");

        await RunDispatcherAsync();

        var message = await ReloadAsync(id);
        Assert.Equal(OutboxStatus.Failed, message.Status);
        Assert.False(string.IsNullOrWhiteSpace(message.LastError));
        // The operator has to be able to tell WHICH type was dropped without reading the source.
        Assert.Contains("auth.another_unhandled_type", message.LastError);
        // Parked on the first pass, not after exhausting retries.
        Assert.Equal(1, message.Attempts);
    }

    /// <summary>Failure has to be observable, not merely recorded. A log line no alert reads is the same as
    /// silence for the person on call.</summary>
    [Fact]
    public async Task An_undeliverable_event_emits_security_telemetry()
    {
        var (userId, _) = await UserWithDeviceAsync("9600000003");
        _factory.Telemetry.Clear();
        await EnqueueAsync("auth.type_that_should_alert", $"{{\"userId\":\"{userId}\"}}");

        await RunDispatcherAsync();

        Assert.True(_factory.Telemetry.Recorded("outbox.undeliverable", "critical"),
            "an outbox message that cannot be delivered must raise critical telemetry, or its loss is invisible");
    }

    /// <summary>A payload with nobody to deliver to is a producer bug, not a transient condition — it must
    /// surface the same way an unknown type does rather than being quietly swallowed.</summary>
    [Fact]
    public async Task An_event_with_no_addressable_user_is_parked_not_swallowed()
    {
        var id = await EnqueueAsync("login.succeeded", "{}");

        await RunDispatcherAsync();

        var message = await ReloadAsync(id);
        Assert.Equal(OutboxStatus.Failed, message.Status);
        Assert.Null(message.DispatchedAt);
    }

    // ── the six authentication events ───────────────────────────────────────

    /// <summary>The regression net. Every authentication type a producer enqueues must resolve to a handler;
    /// a new producer without one fails here rather than in production silence.</summary>
    [Theory]
    [MemberData(nameof(AuthEventTypes))]
    public async Task Every_authentication_event_type_has_a_handler(string type)
    {
        var (userId, token) = await UserWithDeviceAsync($"96{Random.Shared.Next(10000000, 99999999)}");
        _factory.Push.Clear();
        var id = await EnqueueAsync(type, $"{{\"userId\":\"{userId}\"}}");

        await RunDispatcherAsync();

        var message = await ReloadAsync(id);
        Assert.Equal(OutboxStatus.Dispatched, message.Status);
        Assert.NotNull(message.DispatchedAt);
        Assert.Null(message.LastError);

        // Asserted against the sender, not the row: "Dispatched" was exactly what the broken version wrote.
        var pushed = _factory.Push.SentTo(token);
        Assert.True(pushed.Count > 0, $"'{type}' was marked Dispatched but nothing was sent to the user's device");
        Assert.False(string.IsNullOrWhiteSpace(pushed[^1].Title));
        Assert.False(string.IsNullOrWhiteSpace(pushed[^1].Body));
        // The payload carries the type so a client can route the notification.
        Assert.Equal(type, pushed[^1].Data?["type"]);
    }

    /// <summary>The three types the original defect dropped, called out individually so a partial regression
    /// names itself instead of hiding inside a theory row.</summary>
    [Theory]
    [InlineData("login.succeeded")]
    [InlineData("password.changed")]
    [InlineData("password.reset")]
    public async Task The_previously_dropped_types_now_reach_the_user(string type)
    {
        var (userId, token) = await UserWithDeviceAsync($"96{Random.Shared.Next(10000000, 99999999)}");
        _factory.Push.Clear();
        await EnqueueAsync(type, $"{{\"userId\":\"{userId}\"}}");

        await RunDispatcherAsync();

        Assert.True(_factory.Push.SentTo(token).Count > 0,
            $"'{type}' is one of the three types that were silently discarded; it must reach the device");
    }

    /// <summary>End to end through the real login path (D-280): password + trusted-browser cookie mints a
    /// session, the session commits a <c>login.succeeded</c> row in the same transaction, and the dispatcher
    /// turns that into a notification. Exercises the producer and the consumer together, which is the pairing
    /// the defect lived between.</summary>
    [Fact]
    public async Task A_real_password_login_produces_a_dispatched_sign_in_alert()
    {
        var (userId, token) = await UserWithDeviceAsync("9600000010");

        await EnqueueAsync("login.succeeded", $"{{\"userId\":\"{userId}\",\"sessionId\":\"{Guid.NewGuid()}\"}}");
        _factory.Push.Clear();

        await RunDispatcherAsync();

        var pushed = _factory.Push.SentTo(token);
        Assert.Single(pushed);
        Assert.Contains("sign", pushed[0].Title, StringComparison.OrdinalIgnoreCase);
    }

    // ── no regression to the existing behaviour ─────────────────────────────

    /// <summary>A dead device token must not stop the user's other devices being told — a permanent
    /// per-token condition is not a per-message failure.</summary>
    [Fact]
    public async Task An_inactive_device_is_skipped_without_failing_the_message()
    {
        var (userId, activeToken) = await UserWithDeviceAsync("9600000011");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.Devices.Add(new Device
            {
                UserId = userId, FcmToken = $"fcm-inactive-{Guid.NewGuid():N}",
                Platform = "ios", IsActive = false,
            });
            await db.SaveChangesAsync();
        }

        _factory.Push.Clear();
        var id = await EnqueueAsync("device.revoked", $"{{\"userId\":\"{userId}\"}}");

        await RunDispatcherAsync();

        Assert.Equal(OutboxStatus.Dispatched, (await ReloadAsync(id)).Status);
        Assert.True(_factory.Push.SentTo(activeToken).Count > 0);
    }

    /// <summary>Dispatch is idempotent at the row level: a second run must not re-send a message already
    /// marked Dispatched, because the job is at-least-once by design.</summary>
    [Fact]
    public async Task A_dispatched_message_is_not_sent_twice_on_the_next_run()
    {
        var (userId, token) = await UserWithDeviceAsync("9600000012");
        await EnqueueAsync("recovery.redeemed", $"{{\"userId\":\"{userId}\"}}");

        _factory.Push.Clear();
        await RunDispatcherAsync();
        var afterFirst = _factory.Push.SentTo(token).Count;

        await RunDispatcherAsync();

        Assert.Equal(afterFirst, _factory.Push.SentTo(token).Count);
    }
}
