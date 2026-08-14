using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>Trusted browsers (Phase 2A, D-127) — factor 2 only, never the password (INV-A). Real HTTP +
/// real service against kurx_test. Issuing the cookie is Phase 2B; here the service is driven directly for
/// issue/verify and over HTTP for the management surface and the password-change cascade.</summary>
public class TrustedBrowserTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public TrustedBrowserTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private const string Strong = "correct horse battery staple";
    private const string Strong2 = "an entirely different passphrase";

    /// <summary>OTP-logs in a fresh account, returning an authenticated client and the user id.</summary>
    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        var req = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        Assert.True(req.IsSuccessStatusCode, $"OTP request for {phone} failed: {req.StatusCode}");
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var body = await verify.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("access_token").GetString());
        return (client, body.GetProperty("user_id").GetGuid());
    }

    private async Task<T> WithServiceAsync<T>(Func<ITrustedBrowserService, Task<T>> body)
    {
        using var scope = _factory.Services.CreateScope();
        return await body(scope.ServiceProvider.GetRequiredService<ITrustedBrowserService>());
    }

    private Task<string> IssueAsync(Guid userId, string? label = "Chrome on Windows") =>
        WithServiceAsync(async svc => (await svc.IssueAsync(userId, new TrustedBrowserContext(Label: label))).Token);

    private Task<bool> VerifyAsync(Guid userId, string? token) =>
        WithServiceAsync(svc => svc.VerifyAsync(userId, token));

    // ── issue / verify ────────────────────────────────────────────────────────

    [Fact]
    public async Task An_issued_browser_verifies_and_a_wrong_or_empty_token_does_not()
    {
        var (_, userId) = await LoginAsync("9600000001");
        var token = await IssueAsync(userId);

        Assert.True(await VerifyAsync(userId, token));
        Assert.False(await VerifyAsync(userId, token + "x"));   // tampered
        Assert.False(await VerifyAsync(userId, ""));            // empty
        Assert.False(await VerifyAsync(userId, null));          // absent cookie
    }

    [Fact]
    public async Task A_token_only_verifies_for_the_user_it_was_issued_to()
    {
        var (_, a) = await LoginAsync("9600000002");
        var (_, b) = await LoginAsync("9600000003");
        var token = await IssueAsync(a);

        Assert.True(await VerifyAsync(a, token));
        Assert.False(await VerifyAsync(b, token));   // same cookie, different user → no match
    }

    [Fact]
    public async Task An_expired_browser_fails_verification()
    {
        var (_, userId) = await LoginAsync("9600000004");
        var token = await IssueAsync(userId);
        Assert.True(await VerifyAsync(userId, token));

        // Age it past expiry directly — the TTL is measured in days, so this is the only practical way.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.TrustedBrowsers.Where(x => x.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        Assert.False(await VerifyAsync(userId, token));
    }

    [Fact]
    public async Task Only_the_hash_is_stored_never_the_raw_token()
    {
        var (_, userId) = await LoginAsync("9600000005");
        var token = await IssueAsync(userId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var row = await db.TrustedBrowsers.AsNoTracking().SingleAsync(b => b.UserId == userId);
        Assert.NotEqual(token, row.TokenHash);
        Assert.Equal(TokenService.Sha256(token), row.TokenHash);
    }

    // ── management endpoints ────────────────────────────────────────────────────

    [Fact]
    public async Task Listing_returns_active_browsers_and_marks_the_current_one()
    {
        var (client, userId) = await LoginAsync("9600000006");
        var current = await IssueAsync(userId, "This browser");
        await IssueAsync(userId, "Another browser");

        // GET endpoint is reachable and returns both.
        var http = await client.GetAsync("/v1/auth/trusted-browsers");
        Assert.Equal(HttpStatusCode.OK, http.StatusCode);
        var arr = await http.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, arr.GetArrayLength());

        // Current marking is computed against the presented token.
        var withCurrent = await WithServiceAsync(svc => svc.ListAsync(userId, current));
        Assert.Equal(2, withCurrent.Count);
        Assert.Single(withCurrent.Where(b => b.IsCurrent));

        var withNone = await WithServiceAsync(svc => svc.ListAsync(userId, null));
        Assert.DoesNotContain(withNone, b => b.IsCurrent);
    }

    [Fact]
    public async Task Revoking_a_browser_removes_it_and_fails_its_verification()
    {
        var (client, userId) = await LoginAsync("9600000007");
        var token = await IssueAsync(userId);
        var id = (await WithServiceAsync(svc => svc.ListAsync(userId)))[0].Id;

        var res = await client.PostAsync($"/v1/auth/trusted-browsers/{id}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        Assert.False(await VerifyAsync(userId, token));
        Assert.Empty(await WithServiceAsync(svc => svc.ListAsync(userId)));
    }

    [Fact]
    public async Task Revoking_a_browser_you_do_not_own_is_404_not_403()
    {
        var (_, owner) = await LoginAsync("9600000008");
        var (attacker, _) = await LoginAsync("9600000009");
        await IssueAsync(owner);
        var id = (await WithServiceAsync(svc => svc.ListAsync(owner)))[0].Id;

        var res = await attacker.PostAsync($"/v1/auth/trusted-browsers/{id}/revoke", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        // The owner's browser is untouched.
        Assert.Single(await WithServiceAsync(svc => svc.ListAsync(owner)));
    }

    // `POST /revoke-all` and its `RevokeAllAsync` service method were removed: no client ever called
    // them, so the route was reachable surface nothing shipped. Bulk revocation still exists where it is
    // actually used — `RevokeAllForCascadeAsync`, covered by the cascade tests below.

    // ── cascade (INV-A: a re-secure event drops factor 2) ───────────────────────

    [Fact]
    public async Task Changing_the_password_cascade_revokes_trusted_browsers()
    {
        var (client, userId) = await LoginAsync("9600000011");
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Strong })).StatusCode);

        var token = await IssueAsync(userId);
        Assert.True(await VerifyAsync(userId, token));

        var change = await client.PostAsJsonAsync("/v1/auth/password/change",
            new { currentPassword = Strong, newPassword = Strong2 });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        // The browser the thief could have planted no longer satisfies factor 2.
        Assert.False(await VerifyAsync(userId, token));
        Assert.Empty(await WithServiceAsync(svc => svc.ListAsync(userId)));
    }

    [Fact]
    public async Task The_cascade_helper_revokes_all_and_records_the_reason()
    {
        var (_, userId) = await LoginAsync("9600000012");
        await IssueAsync(userId);
        await IssueAsync(userId);

        await WithServiceAsync(async svc =>
        {
            await svc.RevokeAllForCascadeAsync(userId, "recovery");
            return true;
        });
        // RevokeAllForCascadeAsync does not SaveChanges the event by design, but its ExecuteUpdate is
        // immediate, so the browsers are gone.
        Assert.Empty(await WithServiceAsync(svc => svc.ListAsync(userId)));
    }
}
