using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Concurrency contract for <c>AllyService.RequestAsync</c>. The D-201 suite covers the
/// SEQUENTIAL crossed request (A asks, then B asks and auto-accepts). Simultaneously, both callers can
/// observe no row and both INSERT, which `ix_ally_connections_pair` rejects for one of them — that used
/// to surface as an unhandled 23505 and an HTTP 500. These tests hold the line on: no 500, exactly one
/// row per pair, and the same mutual_request auto-accept the sequential path produces.</summary>
public class AllyConcurrencyProbeTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public AllyConcurrencyProbeTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9198{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private static (Guid Low, Guid High) Canonical(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    /// <summary>The core race, hammered. `reverseOrder` flips which client's task is started first so the
    /// canonical-pair loser is sometimes the low id and sometimes the high id — the branch that decides
    /// who wins the INSERT differs between the two, so a fix that only handled one would fail here.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Simultaneous_crossed_requests_never_500_and_converge_on_one_accepted_row(bool reverseOrder)
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var (a, aId) = await LoginAsync(NextPhone());
            var (b, bId) = await LoginAsync(NextPhone());

            Task<HttpResponseMessage> first = a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId });
            Task<HttpResponseMessage> second = b.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = aId });
            var responses = reverseOrder
                ? await Task.WhenAll(second, first)
                : await Task.WhenAll(first, second);

            foreach (var r in responses)
                Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
                    $"reverse={reverseOrder} attempt {attempt}: returned {(int)r.StatusCode} {r.StatusCode}: " +
                    await r.Content.ReadAsStringAsync());

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (low, high) = Canonical(aId, bId);
            var rows = await db.AllyConnections.AsNoTracking()
                .Where(x => x.UserLowId == low && x.UserHighId == high).ToListAsync();

            Assert.True(rows.Count == 1,
                $"reverse={reverseOrder} attempt {attempt}: expected 1 row for the pair, found {rows.Count}");

            // Whichever interleaving occurred, both parties asked, so the pair must end mutually accepted —
            // never stranded Pending, which would leave each side waiting on the other.
            var row = rows[0];
            Assert.True(row.Status == AllyStatus.Accepted,
                $"reverse={reverseOrder} attempt {attempt}: expected Accepted, got {row.Status}");
            Assert.Equal("mutual_request", row.ConnectedVia);
            Assert.NotNull(row.RespondedAt);
        }
    }

    /// <summary>Both sides must see the connection from their own perspective after the race — a row that
    /// exists but is invisible to one party would be just as broken as a 500.</summary>
    [Fact]
    public async Task After_a_simultaneous_race_both_parties_list_each_other_as_an_ally()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());

        await Task.WhenAll(
            a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId }),
            b.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = aId }));

        var aAllies = await Json(await a.GetAsync("/v1/me/allies"));
        var bAllies = await Json(await b.GetAsync("/v1/me/allies"));

        Assert.Contains(aAllies.EnumerateArray(), e => e.GetProperty("other_user_id").GetGuid() == bId);
        Assert.Contains(bAllies.EnumerateArray(), e => e.GetProperty("other_user_id").GetGuid() == aId);

        // And neither side is left with a dangling pending request pointing at the other.
        var aOutgoing = await Json(await a.GetAsync("/v1/allies/requests/outgoing"));
        var bOutgoing = await Json(await b.GetAsync("/v1/allies/requests/outgoing"));
        Assert.DoesNotContain(aOutgoing.EnumerateArray(), e => e.GetProperty("other_user_id").GetGuid() == bId);
        Assert.DoesNotContain(bOutgoing.EnumerateArray(), e => e.GetProperty("other_user_id").GetGuid() == aId);
    }

    /// <summary>BUG-4: the pending-outgoing cap must also cover reactivation of a Declined/Revoked pair,
    /// which creates the same obligation on the target as a brand-new request.</summary>
    [Fact]
    public async Task Re_requesting_a_declined_pair_is_still_subject_to_the_pending_cap()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());

        // Establish a Declined row for the pair, then drive the requester to the cap.
        var req = await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId }));
        var connectionId = req.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK,
            (await b.PostAsJsonAsync($"/v1/allies/requests/{connectionId}/decline", new { })).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            // Age the decline past the BUG-C cooldown so this test still exercises the PENDING CAP
            // rather than tripping the cooldown first — the cap is what it is about.
            var declinedRow = await db.AllyConnections.FirstAsync(x => x.Id == connectionId);
            declinedRow.RespondedAt = DateTimeOffset.UtcNow.AddDays(-31);

            // Seed the requester to exactly the cap with unrelated pending rows. Pair order is
            // DB-enforced (UserLowId < UserHighId), so each synthetic partner is canonicalised.
            for (var i = 0; i < 200; i++)
            {
                var other = Guid.NewGuid();
                db.Users.Add(new Domain.Entities.User
                {
                    Id = other, Phone = $"9199{i:D6}", Name = $"Filler {i}",
                });
                var (low, high) = Canonical(aId, other);
                db.AllyConnections.Add(new Domain.Entities.AllyConnection
                {
                    UserLowId = low, UserHighId = high,
                    RequesterId = aId, AddresseeId = other, Status = AllyStatus.Pending,
                });
            }
            await db.SaveChangesAsync();
        }

        var retry = await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId });
        Assert.Equal(HttpStatusCode.TooManyRequests, retry.StatusCode);
        var body = await Json(retry);
        Assert.Equal("too_many_pending_requests", body.GetProperty("error").GetString());
    }
}
