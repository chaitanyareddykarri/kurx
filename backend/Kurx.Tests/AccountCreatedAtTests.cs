using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>The account's creation stamp: server-generated once, never writable, and exposed at two
/// different precisions depending on who is asking.
///
/// <para><b>The distinction under test.</b> `GET /v1/me` serves the owner the exact UTC instant, because
/// it is their own account metadata. The public profile serves only <c>joined_at</c> at month precision
/// (D-312), because the exact moment someone signed up is a behavioural fact — it pins when they were at
/// a keyboard and correlates accounts registered in the same minute — and nothing on a public page needs
/// it to say how long they have been here. The truncation happens in the projection, so the precision is
/// never on the wire to leak.</para></summary>
public class AccountCreatedAtTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public AccountCreatedAtTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            _reset = true;
        }
    }

    private async Task<HttpClient> SignInAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await (await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    private static async Task<DateTime> CreatedAtAsync(HttpClient client)
    {
        var me = await (await client.GetAsync("/v1/me")).Content.ReadFromJsonAsync<JsonElement>();
        return me.GetProperty("created_at").GetDateTime();
    }

    [Fact]
    public async Task A_new_account_is_stamped_by_the_server_at_creation()
    {
        var before = DateTime.UtcNow.AddSeconds(-5);
        var client = await SignInAsync("9200000001");
        var after = DateTime.UtcNow.AddSeconds(5);

        var createdAt = await CreatedAtAsync(client);

        Assert.InRange(createdAt.ToUniversalTime(), before, after);
    }

    /// <summary>Not merely "the field is ignored" — the whole point is that no request body can move it.
    /// `UpdateProfileBody` has no such property, so this asserts the endpoint does not quietly bind one
    /// through some future overload either.</summary>
    [Fact]
    public async Task A_client_cannot_set_or_move_the_creation_stamp()
    {
        var client = await SignInAsync("9200000002");
        var original = await CreatedAtAsync(client);

        var response = await client.PatchAsJsonAsync("/v1/me/profile", new
        {
            name = "Stamp Pusher",
            username = "stamppusher",
            dateOfBirth = "1995-05-05",
            createdAt = "2001-01-01T00:00:00Z",       // ignored
            created_at = "2001-01-01T00:00:00Z",      // ignored under either spelling
        });
        response.EnsureSuccessStatusCode();

        Assert.Equal(original, await CreatedAtAsync(client));
    }

    /// <summary>Walks the whole lifecycle the stamp has to survive: onboarding, every profile edit,
    /// a password change, and a fresh sign-in.</summary>
    [Fact]
    public async Task The_creation_stamp_survives_onboarding_edits_and_re_login()
    {
        const string phone = "9200000003";
        var client = await SignInAsync(phone);
        var original = await CreatedAtAsync(client);

        await client.PatchAsJsonAsync("/v1/me/profile",
            new { name = "Lifecycle User", username = "lifecycle", dateOfBirth = "1994-03-21" });
        Assert.Equal(original, await CreatedAtAsync(client));

        await client.PostAsJsonAsync("/v1/auth/password/set", new { password = "correct-horse-battery" });
        Assert.Equal(original, await CreatedAtAsync(client));

        await client.PatchAsJsonAsync("/v1/me/profile", new { bio = "Edited bio", headline = "Edited" });
        Assert.Equal(original, await CreatedAtAsync(client));

        await client.PatchAsJsonAsync("/v1/me/profile", new { username = "lifecycle_renamed" });
        Assert.Equal(original, await CreatedAtAsync(client));

        await client.PatchAsJsonAsync("/v1/me/privacy", new { profilePublic = true });
        Assert.Equal(original, await CreatedAtAsync(client));

        // A second sign-in is a login, not a registration — the row is found, never re-created.
        var second = await SignInAsync(phone);
        Assert.Equal(original, await CreatedAtAsync(second));
    }

    [Fact]
    public async Task The_public_profile_gives_the_month_and_withholds_the_instant()
    {
        var client = await SignInAsync("9200000004");
        (await client.PatchAsJsonAsync("/v1/me/profile",
            new { name = "Public Person", username = "publicperson", dateOfBirth = "1992-12-12" }))
            .EnsureSuccessStatusCode();

        var exact = await CreatedAtAsync(client);

        // Anonymous read — this is the public projection, so no token.
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync("/v1/public/users/publicperson");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var profile = JsonDocument.Parse(body).RootElement;

        Assert.Equal(exact.ToUniversalTime().ToString("yyyy-MM"),
            profile.GetProperty("joined_at").GetString());

        // The exact stamp must not be reachable here under any key. Asserted on the raw response
        // string: a typed read would only prove the properties we thought to ask about are absent.
        Assert.DoesNotContain("created_at", body);
        Assert.DoesNotContain(exact.ToUniversalTime().ToString("HH:mm"), body);
        Assert.Equal(7, profile.GetProperty("joined_at").GetString()!.Length);   // "YYYY-MM"
    }

    [Fact]
    public async Task Account_metadata_is_not_readable_without_a_token()
    {
        var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>An account that predates this work keeps its own historical stamp — nothing backfills or
    /// rewrites it, because the column has been written on insert since the beginning (D-312).</summary>
    [Fact]
    public async Task An_older_account_keeps_its_historical_stamp()
    {
        const string phone = "9200000005";
        var first = await SignInAsync(phone);
        var original = await CreatedAtAsync(first);

        // Simulate "an account from before": push its stamp into the past directly, the way an
        // existing production row already sits, then confirm nothing on the read path rewrites it.
        // A bare Indian mobile normalises to E.164 digits without the '+' — the legacy storage form.
        const string stored = "91" + phone;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = await db.Users.FirstAsync(u => u.Phone == stored);
            user.CreatedAt = new DateTime(2024, 3, 14, 9, 26, 53, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }

        var reread = await CreatedAtAsync(await SignInAsync(phone));

        Assert.NotEqual(original, reread);
        Assert.Equal(new DateTime(2024, 3, 14, 9, 26, 53, DateTimeKind.Utc), reread.ToUniversalTime());
    }
}
