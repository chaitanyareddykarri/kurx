using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-266 M6 (D9 Method B) — the invite-link seat claim under parallel redemption.
///
/// <para><b>This is the test the milestone exists for.</b> <c>UsedCount</c> is a shared counter, and a
/// read-modify-write would let N concurrent redeemers all read the same value and all succeed — the exact
/// shape of the coupon over-redemption bug (D-240/D-261). The claim is a conditional UPDATE carrying the
/// cap in its WHERE, so the database decides who gets the last seat; these cases prove it at 2, 10 and 100
/// simultaneous claimants.</para>
///
/// <para>Separate class from <see cref="InvitationFlowTests"/> because each case here needs many real
/// accounts, and mixing them would make the functional suite slow for no gain.</para></summary>
public class InviteLinkConcurrencyTests(KurxApiFactory factory) : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory = factory;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var c = _factory.CreateClient();
        await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var t = await Json(await c.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return c;
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == "hackathon")
            .Select(c => new { c.Id, c.ParentId }).FirstAsync();

        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Seats " + Guid.NewGuid().ToString("N")[..6], description = "a real description",
            categoryId = t.ParentId!.Value, typeId = t.Id, venueName = "Hall", city = "C",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(3),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task<string> CreateLinkAsync(HttpClient owner, Guid eventId, int? maxSeats,
        bool singleUse = false, string? passcode = null)
    {
        var res = await owner.PostAsJsonAsync($"/v1/events/{eventId}/invite-links",
            new { maxSeats, singleUse, expiresAt = (DateTime?)null, passcode });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("token").GetString()!;
    }

    private async Task<int> UsedCountAsync(string token)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<KurxDbContext>()
            .EventInviteLinks.AsNoTracking().Where(l => l.Token == token).Select(l => l.UsedCount).FirstAsync();
    }

    /// <summary>Logs in <paramref name="n"/> distinct accounts, then has them all redeem the same link
    /// simultaneously. Login is sequential on purpose — the OTP capture is keyed by phone and the race under
    /// test is the seat claim, not the auth flow.</summary>
    /// <param name="phonePrefix">Six digits. With the four-digit counter this makes a ten-digit Indian
    /// mobile, which is what the OTP endpoint now requires: an unplaceable number is refused before a code
    /// is minted (D-317), and these prefixes used to produce twelve-digit numbers that only worked because
    /// the normalizer fell back to raw digits.</param>
    private async Task<(int Ok, int Refused)> StampedeAsync(string token, int n, string phonePrefix)
    {
        var clients = new List<HttpClient>();
        for (var i = 0; i < n; i++) clients.Add(await LoginAsync($"{phonePrefix}{i:D4}"));

        var responses = await Task.WhenAll(clients.Select(c =>
            c.PostAsJsonAsync($"/v1/invite-links/{token}/redeem", new { passcode = (string?)null })));

        var ok = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        return (ok, responses.Length - ok);
    }

    // ── The race, at three magnitudes ────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_users_claiming_one_seat_yield_exactly_one_winner()
    {
        var owner = await LoginAsync("9703100001");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Seat Race Two");
        var id = await CreateEventAsync(owner, orgId);
        var token = await CreateLinkAsync(owner, id, maxSeats: 1);

        var (ok, refused) = await StampedeAsync(token, 2, "970310");

        Assert.Equal(1, ok);
        Assert.Equal(1, refused);
        Assert.Equal(1, await UsedCountAsync(token));
    }

    [Fact]
    public async Task Ten_users_claiming_three_seats_never_exceed_the_cap()
    {
        var owner = await LoginAsync("9703200001");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Seat Race Ten");
        var id = await CreateEventAsync(owner, orgId);
        var token = await CreateLinkAsync(owner, id, maxSeats: 3);

        var (ok, refused) = await StampedeAsync(token, 10, "970320");

        Assert.Equal(3, ok);
        Assert.Equal(7, refused);
        Assert.Equal(3, await UsedCountAsync(token));
    }

    /// <summary>The magnitude that would expose a read-modify-write beyond doubt: with 100 simultaneous
    /// claimants on 25 seats, a non-atomic increment overshoots essentially every run.</summary>
    [Fact]
    public async Task A_hundred_users_claiming_twenty_five_seats_never_exceed_the_cap()
    {
        var owner = await LoginAsync("9703300001");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Seat Race Hundred");
        var id = await CreateEventAsync(owner, orgId);
        var token = await CreateLinkAsync(owner, id, maxSeats: 25);

        // 6-digit prefix + a 4-digit index = a 10-digit number. LastOtpFor matches on the last 10 chars,
        // so a shorter prefix throws before the seat claim is ever exercised.
        var (ok, _) = await StampedeAsync(token, 100, "970330");

        Assert.Equal(25, ok);
        Assert.Equal(25, await UsedCountAsync(token));
    }

    [Fact]
    public async Task A_single_use_link_admits_exactly_one_of_many_simultaneous_claimants()
    {
        var owner = await LoginAsync("9703400001");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Seat Race Single");
        var id = await CreateEventAsync(owner, orgId);
        // No seat cap at all: SingleUse alone must stop the second claimant, or the two controls would
        // only work in combination and a cap-less single-use link would be unlimited.
        var token = await CreateLinkAsync(owner, id, maxSeats: null, singleUse: true);

        var (ok, _) = await StampedeAsync(token, 8, "970340");

        Assert.Equal(1, ok);
        Assert.Equal(1, await UsedCountAsync(token));
    }

    /// <summary>D9 rule 7 — redeeming twice is idempotent and consumes no second seat. Asserted under
    /// parallelism because the guard is a unique index, not a read-then-write, and a sequential test would
    /// pass against either.</summary>
    [Fact]
    public async Task One_user_redeeming_repeatedly_consumes_exactly_one_seat()
    {
        var owner = await LoginAsync("9703500001");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Seat Idempotent");
        var id = await CreateEventAsync(owner, orgId);
        var token = await CreateLinkAsync(owner, id, maxSeats: 10);

        var guest = await LoginAsync("9703500002");
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            guest.PostAsJsonAsync($"/v1/invite-links/{token}/redeem", new { passcode = (string?)null })));

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1, await UsedCountAsync(token));
    }

    /// <summary>A wrong passcode must be refused BEFORE the seat is claimed. Otherwise a guessing attacker
    /// drains the link without ever getting in.</summary>
    [Fact]
    public async Task A_wrong_passcode_consumes_no_seat()
    {
        var owner = await LoginAsync("9703600001");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Seat Passcode");
        var id = await CreateEventAsync(owner, orgId);
        var token = await CreateLinkAsync(owner, id, maxSeats: 5, passcode: "correct-horse");

        var guest = await LoginAsync("9703600002");
        var bad = await guest.PostAsJsonAsync($"/v1/invite-links/{token}/redeem", new { passcode = "wrong" });
        Assert.Equal(HttpStatusCode.Forbidden, bad.StatusCode);
        Assert.Equal(0, await UsedCountAsync(token));

        var missing = await guest.PostAsJsonAsync($"/v1/invite-links/{token}/redeem", new { passcode = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.Equal(0, await UsedCountAsync(token));

        var good = await guest.PostAsJsonAsync($"/v1/invite-links/{token}/redeem", new { passcode = "correct-horse" });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        Assert.Equal(1, await UsedCountAsync(token));
    }
}
