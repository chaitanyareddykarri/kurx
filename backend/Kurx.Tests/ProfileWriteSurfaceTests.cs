using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-219 (Phase 0 of Professional Identity V2) — the profile <b>write</b> surface. Every flag
/// exercised here was already enforced on the public read paths but settable by nothing, so these are
/// regression tests for real defects rather than coverage of new features:
/// <list type="bullet">
/// <item>the four visibility flags had no writer at all — <c>ShowAttended</c> defaults to false, so the
/// attended-events lane was dark for every user with no way to turn it on;</item>
/// <item><c>GET /v1/me</c> returned none of the editable display fields, which is what forced the web
/// settings page to prefill from the <i>public</i> profile and silently wipe headline/bio/skills when
/// that read failed;</item>
/// <item><c>AllyConnection.Visibility</c> shipped with read-side enforcement and no writer;</item>
/// <item>avatar/cover keys were accepted by the API with no way to upload the image they name.</item>
/// </list>
/// Real HTTP against real Postgres, per the repo testing standard.</summary>
public class ProfileWriteSurfaceTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public ProfileWriteSurfaceTests(KurxApiFactory factory)
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

    /// <summary>Gives the caller a claimed username + public profile so the public read paths resolve.</summary>
    private async Task<string> ClaimProfileAsync(Guid userId)
    {
        var username = "pw" + Guid.NewGuid().ToString("N")[..16];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.Username = username;
        user.ProfilePublic = true;
        await db.SaveChangesAsync();
        return username;
    }

    // ── Privacy write surface (defect A) ─────────────────────────────────────

    [Fact]
    public async Task Privacy_flags_round_trip_through_patch_and_me()
    {
        var (client, _) = await LoginAsync(NextPhone());

        var patched = await Json(await client.PatchAsJsonAsync("/v1/me/privacy", new
        {
            profilePublic = false, showAttended = true, showCertificates = false, showAllies = false,
        }));
        Assert.False(patched.GetProperty("profile_public").GetBoolean());
        Assert.True(patched.GetProperty("show_attended").GetBoolean());
        Assert.False(patched.GetProperty("show_certificates").GetBoolean());
        Assert.False(patched.GetProperty("show_allies").GetBoolean());

        // GET /v1/me must agree with what PATCH just returned — they share one serializer precisely so
        // these two can never drift apart.
        var me = await Json(await client.GetAsync("/v1/me"));
        var privacy = me.GetProperty("privacy");
        Assert.False(privacy.GetProperty("profile_public").GetBoolean());
        Assert.True(privacy.GetProperty("show_attended").GetBoolean());
        Assert.False(privacy.GetProperty("show_certificates").GetBoolean());
        Assert.False(privacy.GetProperty("show_allies").GetBoolean());
    }

    [Fact]
    public async Task Privacy_patch_is_partial_and_leaves_omitted_flags_untouched()
    {
        var (client, _) = await LoginAsync(NextPhone());
        await client.PatchAsJsonAsync("/v1/me/privacy", new { showCertificates = false, showAllies = false });

        // Only show_attended is sent here; the two flags turned off above must survive.
        var patched = await Json(await client.PatchAsJsonAsync("/v1/me/privacy", new { showAttended = true }));
        Assert.True(patched.GetProperty("show_attended").GetBoolean());
        Assert.False(patched.GetProperty("show_certificates").GetBoolean());
        Assert.False(patched.GetProperty("show_allies").GetBoolean());
        Assert.True(patched.GetProperty("profile_public").GetBoolean());   // never sent, still the default
    }

    /// <summary>The headline defect: <c>ShowAttended</c> defaults to false and nothing could set it, so
    /// <c>?type=attended</c> was a permanent 403 for every user on the platform.</summary>
    [Fact]
    public async Task Show_attended_can_now_be_turned_on_and_unlocks_the_attended_events_lane()
    {
        var (client, userId) = await LoginAsync(NextPhone());
        var username = await ClaimProfileAsync(userId);
        var anon = _factory.CreateClient();

        var before = await anon.GetAsync($"/v1/public/users/{username}/events?type=attended");
        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);

        await client.PatchAsJsonAsync("/v1/me/privacy", new { showAttended = true });

        var after = await anon.GetAsync($"/v1/public/users/{username}/events?type=attended");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task Profile_public_false_hides_the_whole_profile()
    {
        var (client, userId) = await LoginAsync(NextPhone());
        var username = await ClaimProfileAsync(userId);
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/v1/public/users/{username}")).StatusCode);

        await client.PatchAsJsonAsync("/v1/me/privacy", new { profilePublic = false });

        // Hidden resource → 404, never 403 (D-018) — the profile must not be distinguishable from one
        // that never existed.
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/v1/public/users/{username}")).StatusCode);
    }

    [Fact]
    public async Task Privacy_patch_requires_authentication()
    {
        var anon = _factory.CreateClient();
        var res = await anon.PatchAsJsonAsync("/v1/me/privacy", new { showAttended = true });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── /v1/me now carries the editable fields (defect C — the silent wipe) ───

    /// <summary>Root cause of the web silent-wipe bug: the owner's own headline/bio/skills were readable
    /// only through the public profile endpoint, which fails for an unclaimed username or a non-public
    /// profile. A form prefilled from that failed read submitted blanks and overwrote real data.</summary>
    [Fact]
    public async Task Me_returns_the_editable_display_fields_without_a_public_profile()
    {
        var (client, _) = await LoginAsync(NextPhone());

        await client.PatchAsJsonAsync("/v1/me/profile", new
        {
            name = "Prefill Tester",
            headline = "Backend engineer",
            bio = "Builds things.",
            skills = new[] { "dotnet", "postgres" },
            linksJson = "{\"github\":\"kurx\"}",
        });

        // No username claimed and no public profile — exactly the state in which the old prefill path
        // returned nothing and the edit form rendered empty.
        var me = await Json(await client.GetAsync("/v1/me"));
        Assert.Equal("Backend engineer", me.GetProperty("headline").GetString());
        Assert.Equal("Builds things.", me.GetProperty("bio").GetString());
        Assert.Equal(2, me.GetProperty("skills").GetArrayLength());
        // links_json is a jsonb column, so Postgres reparses and reserializes it — asserting on the exact
        // string would be asserting on Postgres's whitespace, not on the round-trip. Compare the value.
        using (var links = JsonDocument.Parse(me.GetProperty("links_json").GetString()!))
            Assert.Equal("kurx", links.RootElement.GetProperty("github").GetString());
        Assert.True(me.TryGetProperty("avatar_key", out _));
        Assert.True(me.TryGetProperty("cover_key", out _));
        Assert.True(me.TryGetProperty("education_json", out _));
    }

    // ── Per-connection ally visibility (defect B) ────────────────────────────

    [Fact]
    public async Task Hiding_one_connection_removes_it_from_the_public_allies_list()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var username = await ClaimProfileAsync(aId);
        await ClaimProfileAsync(bId);

        var connectionId = (await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId })))
            .GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        var anon = _factory.CreateClient();
        Assert.Single((await Json(await anon.GetAsync($"/v1/public/users/{username}/allies"))).EnumerateArray());

        var hidden = await Json(await a.PatchAsJsonAsync($"/v1/allies/{connectionId}/visibility", new { visibility = "hidden" }));
        Assert.Equal("Hidden", hidden.GetProperty("visibility").GetString());

        Assert.Empty((await Json(await anon.GetAsync($"/v1/public/users/{username}/allies"))).EnumerateArray());
    }

    /// <summary>Either party may hide the pair and the flag is shared — the more private choice wins,
    /// matching the existing rule that either party's ProfilePublic=false hides the connection (D-201).</summary>
    [Fact]
    public async Task Either_party_can_hide_the_connection()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var username = await ClaimProfileAsync(aId);
        await ClaimProfileAsync(bId);

        var connectionId = (await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId })))
            .GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        // B — the addressee, not the requester — hides it, and it disappears from A's public profile.
        var res = await b.PatchAsJsonAsync($"/v1/allies/{connectionId}/visibility", new { visibility = "hidden" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var anon = _factory.CreateClient();
        Assert.Empty((await Json(await anon.GetAsync($"/v1/public/users/{username}/allies"))).EnumerateArray());
    }

    [Fact]
    public async Task Visibility_is_reversible()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var username = await ClaimProfileAsync(aId);
        await ClaimProfileAsync(bId);

        var connectionId = (await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId })))
            .GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        await a.PatchAsJsonAsync($"/v1/allies/{connectionId}/visibility", new { visibility = "hidden" });
        await a.PatchAsJsonAsync($"/v1/allies/{connectionId}/visibility", new { visibility = "public" });

        var anon = _factory.CreateClient();
        Assert.Single((await Json(await anon.GetAsync($"/v1/public/users/{username}/allies"))).EnumerateArray());
    }

    /// <summary>A non-party gets 404, never 403 — the hidden-resource convention (D-018). Confirming the
    /// row exists to a stranger would leak that two specific people are connected.</summary>
    [Fact]
    public async Task Non_party_cannot_change_visibility_and_gets_404()
    {
        var (a, _) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var (stranger, _) = await LoginAsync(NextPhone());

        var connectionId = (await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId })))
            .GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        var res = await stranger.PatchAsJsonAsync($"/v1/allies/{connectionId}/visibility", new { visibility = "hidden" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task An_unknown_visibility_value_is_rejected()
    {
        var (a, _) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var connectionId = (await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId })))
            .GetProperty("id").GetGuid();

        var res = await a.PatchAsJsonAsync($"/v1/allies/{connectionId}/visibility", new { visibility = "invisible" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ── Profile image upload (defect D) ──────────────────────────────────────

    [Fact]
    public async Task Profile_image_presign_returns_a_key_scoped_to_the_caller()
    {
        var (client, userId) = await LoginAsync(NextPhone());

        var body = await Json(await client.PostAsJsonAsync("/v1/me/profile-image/presign", new
        {
            slot = "avatar", contentType = "image/png", maxBytes = 1024 * 1024,
        }));

        var key = body.GetProperty("key").GetString()!;
        // Per-user, per-slot prefix — a caller can never presign into someone else's namespace.
        Assert.StartsWith($"users/{userId}/avatar/", key);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("url").GetString()));
    }

    [Fact]
    public async Task Cover_slot_presigns_to_its_own_prefix()
    {
        var (client, userId) = await LoginAsync(NextPhone());
        var body = await Json(await client.PostAsJsonAsync("/v1/me/profile-image/presign", new
        {
            slot = "cover", contentType = "image/jpeg", maxBytes = 1024,
        }));
        Assert.StartsWith($"users/{userId}/cover/", body.GetProperty("key").GetString());
    }

    [Fact]
    public async Task Profile_image_presign_rejects_an_unknown_slot()
    {
        var (client, _) = await LoginAsync(NextPhone());
        var res = await client.PostAsJsonAsync("/v1/me/profile-image/presign", new
        {
            slot = "../../etc", contentType = "image/png", maxBytes = 1024,
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>The key is rendered directly in an img tag on a public page, so a non-image content type
    /// is refused at the boundary rather than stored and served later.</summary>
    [Fact]
    public async Task Profile_image_presign_rejects_a_non_image_content_type()
    {
        var (client, _) = await LoginAsync(NextPhone());
        var res = await client.PostAsJsonAsync("/v1/me/profile-image/presign", new
        {
            slot = "avatar", contentType = "text/html", maxBytes = 1024,
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Profile_image_presign_requires_authentication()
    {
        var anon = _factory.CreateClient();
        var res = await anon.PostAsJsonAsync("/v1/me/profile-image/presign", new
        {
            slot = "avatar", contentType = "image/png", maxBytes = 1024,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    /// <summary>The presigned key is persisted through the existing profile PATCH — no second write path,
    /// mirroring how event media confirms a key after upload.</summary>
    [Fact]
    public async Task A_presigned_avatar_key_persists_through_the_profile_patch()
    {
        var (client, userId) = await LoginAsync(NextPhone());
        var presigned = await Json(await client.PostAsJsonAsync("/v1/me/profile-image/presign", new
        {
            slot = "avatar", contentType = "image/webp", maxBytes = 1024,
        }));
        var key = presigned.GetProperty("key").GetString();

        var patched = await Json(await client.PatchAsJsonAsync("/v1/me/profile", new { avatarKey = key }));
        Assert.Equal(key, patched.GetProperty("avatar_key").GetString());

        var me = await Json(await client.GetAsync("/v1/me"));
        Assert.Equal(key, me.GetProperty("avatar_key").GetString());
        Assert.Equal(userId, me.GetProperty("id").GetGuid());
    }
}
