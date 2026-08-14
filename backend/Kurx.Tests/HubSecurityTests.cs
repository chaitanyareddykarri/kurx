using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Covers SignalR hub security: authentication is required to connect, and group joins are
/// rejected unless the caller is actually a member of the org (SalesHub) / the org owning the event (ScanHub).</summary>
public class HubSecurityTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public HubSecurityTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(string AccessToken, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        var tokens = await Json(verify);
        return (tokens.GetProperty("access_token").GetString()!, tokens.GetProperty("user_id").GetGuid());
    }

    private HubConnection BuildConnection(string path, string? accessToken)
    {
        var url = accessToken is null
            ? new Uri(_factory.Server.BaseAddress, path)
            : new Uri(_factory.Server.BaseAddress, $"{path}?access_token={accessToken}");
        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();
    }

    // ── D-294: hub methods are throttled ───────────────────────────────────────

    /// <summary>Hub invocations are rate limited per connection.
    ///
    /// <para>Before D-294 they were not limited at all: <c>UseRateLimiter</c> is HTTP middleware and
    /// only ever sees <c>/negotiate</c>, so once the socket was up every method could be called in a
    /// loop. <c>LoginHub.Watch</c> is the sharpest case — anonymous, and a database read per call.</para>
    ///
    /// <para>Driven through a real <c>HubConnection</c> rather than by constructing a filter context,
    /// because the thing under test is that the filter is actually WIRED, which a unit test of the
    /// filter class would pass without.</para></summary>
    [Fact]
    public async Task Hub_invocations_are_rate_limited_per_connection()
    {
        await using var connection = BuildConnection("/hubs/login", accessToken: null);
        await connection.StartAsync();

        // Every call is refused on its merits (a bogus poll token), so the only thing that can change
        // the error is the throttle in front of it.
        var errors = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            var ex = await Assert.ThrowsAsync<HubException>(
                () => connection.InvokeAsync("Watch", Guid.NewGuid(), "not-a-real-poll-token"));
            errors.Add(ex.Message);
        }

        Assert.Contains(errors, e => e.Contains("not_authorized"));   // the limit is not the FIRST answer
        Assert.Contains(errors, e => e.Contains("rate_limited"));     // and it does arrive
        await connection.StopAsync();
    }

    /// <summary>The limit is per connection, not per process: one abusive client must not throttle
    /// everyone else on the same instance.</summary>
    [Fact]
    public async Task One_connection_hitting_the_limit_does_not_throttle_another()
    {
        await using var noisy = BuildConnection("/hubs/login", accessToken: null);
        await noisy.StartAsync();
        for (var i = 0; i < 12; i++)
            await Assert.ThrowsAsync<HubException>(
                () => noisy.InvokeAsync("Watch", Guid.NewGuid(), "not-a-real-poll-token"));

        await using var quiet = BuildConnection("/hubs/login", accessToken: null);
        await quiet.StartAsync();
        var ex = await Assert.ThrowsAsync<HubException>(
            () => quiet.InvokeAsync("Watch", Guid.NewGuid(), "not-a-real-poll-token"));

        // Refused for the right reason — its own first call, not the other connection's spending.
        Assert.Contains("not_authorized", ex.Message);

        await noisy.StopAsync();
        await quiet.StopAsync();
    }

    [Fact]
    public async Task Anonymous_connection_to_sales_hub_is_rejected()
    {
        await using var connection = BuildConnection("/hubs/sales", accessToken: null);
        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task Non_member_cannot_join_org_group()
    {
        var (ownerToken, _) = await LoginAsync("9500000001");
        Guid orgId;
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);
            orgId = _factory.SeedVerifiedOrgForClient(client, "Hub Security Org");
        }

        var (outsiderToken, _) = await LoginAsync("9500000002");
        await using var connection = BuildConnection("/hubs/sales", outsiderToken);
        await connection.StartAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("JoinOrg", orgId));
        Assert.Contains("forbidden", ex.Message);
        await connection.StopAsync();
    }

    [Fact]
    public async Task Member_can_join_org_group()
    {
        var (ownerToken, _) = await LoginAsync("9500000003");
        Guid orgId;
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);
            orgId = _factory.SeedVerifiedOrgForClient(client, "Hub Member Org");
        }

        await using var connection = BuildConnection("/hubs/sales", ownerToken);
        await connection.StartAsync();
        await connection.InvokeAsync("JoinOrg", orgId); // no throw
        await connection.StopAsync();
    }

    [Fact]
    public async Task Non_member_cannot_join_event_group()
    {
        var (ownerToken, ownerId) = await LoginAsync("9500000004");
        Guid orgId;
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);
            orgId = _factory.SeedVerifiedOrgForClient(client, "Hub Scan Org");
        }

        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            // Seed a real category so the events.CategoryId FK is satisfied; ShortCode is NOT NULL
            // (D-036) and CategoryId/AudienceLevelId are FKs to event_categories — a random Guid
            // would violate them, so this direct insert must use valid values.
            var category = new EventCategory
            {
                Level = CategoryLevel.Category, Name = "Hub Cat", Slug = $"hub-cat-{Guid.NewGuid():N}",
            };
            db.EventCategories.Add(category);
            var ev = new Event
            {
                RepresentingOrgId = orgId,
                Title = "Hub Test Event",
                Slug = $"hub-test-event-{Guid.NewGuid():N}",
                ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                CategoryId = category.Id,
                StartsAt = DateTime.UtcNow.AddDays(1),
                EndsAt = DateTime.UtcNow.AddDays(1).AddHours(2),
                CreatedBy = ownerId,
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            eventId = ev.Id;
        }

        var (outsiderToken, _) = await LoginAsync("9500000005");
        await using var connection = BuildConnection("/hubs/scan", outsiderToken);
        await connection.StartAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("JoinEvent", eventId));
        Assert.Contains("forbidden", ex.Message);
        await connection.StopAsync();
    }
}
