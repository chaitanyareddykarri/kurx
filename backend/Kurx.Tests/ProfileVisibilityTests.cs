using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-221 — the central visibility resolver, which is the profile's <b>only</b> authorization
/// boundary. These are the highest-value tests in the profile suite: a section that bypasses the
/// resolver, or a tier that resolves the wrong way, is a data leak rather than a cosmetic bug.
///
/// <para>Covers the exhaustiveness invariant (every <see cref="ProfileSection"/> has a decision), the
/// full tier × viewer-relationship matrix, and the migration guarantee that every pre-D-221 boolean
/// combination still resolves identically.</para></summary>
public class ProfileVisibilityTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public ProfileVisibilityTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9199{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<string> ClaimAsync(Guid userId)
    {
        var username = "vis" + Guid.NewGuid().ToString("N")[..15];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.Username = username;
        user.ProfilePublic = true;
        await db.SaveChangesAsync();
        return username;
    }

    /// <summary>Makes two users co-participants on one public event — the EventParticipants tier.</summary>
    private async Task SeedSharedEventAsync(Guid a, Guid b)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var org = new Organization { Name = "Vis Org " + Guid.NewGuid().ToString("N")[..6], Slug = "visorg" + Guid.NewGuid().ToString("N")[..10] };
        db.Organizations.Add(org);
        var cat = await db.EventCategories.FirstOrDefaultAsync(c => c.Slug == "vis-cat");
        if (cat is null)
        {
            cat = new EventCategory { Level = CategoryLevel.Category, Name = "Vis Cat", Slug = "vis-cat" };
            db.EventCategories.Add(cat);
        }
        await db.SaveChangesAsync();

        var ev = new Event
        {
            RepresentingOrgId = org.Id, CategoryId = cat.Id, CreatedBy = a,
            Title = "Shared Event", Slug = "shared-" + Guid.NewGuid().ToString("N")[..10],
            // ShortCode is NOT NULL with no default — seeding an Event directly must supply one, which
            // the service-created path (CreateAndPublishPastEventAsync) does for you.
            ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            City = "Hyderabad", Status = EventStatus.Published, Visibility = EventVisibility.Listed,
            StartsAt = DateTime.UtcNow.AddDays(-10), EndsAt = DateTime.UtcNow.AddDays(-9),
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        foreach (var uid in new[] { a, b })
        {
            db.EventParticipants.Add(new EventParticipant
            {
                EventId = ev.Id, SubjectType = ParticipantSubjectType.Person, SubjectId = uid,
                RoleSlug = "attendee", State = ParticipantState.Completed,
                Visibility = ParticipantVisibility.Public,
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task SetSectionAsync(HttpClient client, string section, string tier)
    {
        var res = await client.PatchAsJsonAsync("/v1/me/privacy", new
        {
            sections = new Dictionary<string, string> { [section] = tier },
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ── Exhaustiveness: the enum is the registry ─────────────────────────────

    /// <summary>Every section must resolve to an explicit decision. If someone adds a
    /// <see cref="ProfileSection"/> member without giving it a default, this fails — which is the point:
    /// a new section must not silently inherit "visible".</summary>
    [Fact]
    public async Task Every_profile_section_has_a_resolved_decision()
    {
        var (client, userId) = await LoginAsync(NextPhone());
        var me = await Json(await client.GetAsync("/v1/me"));
        var sections = me.GetProperty("privacy").GetProperty("sections");

        foreach (var section in Enum.GetValues<ProfileSection>())
        {
            var key = Kurx.Infrastructure.Users.ProfileVisibilityResolver.SectionKey(section);
            Assert.True(sections.TryGetProperty(key, out var tier),
                $"ProfileSection.{section} has no visibility setting — add it to the resolver's Defaults table.");
            Assert.False(string.IsNullOrWhiteSpace(tier.GetString()));
        }
        Assert.NotEqual(Guid.Empty, userId);
    }

    // ── The tier × viewer matrix ─────────────────────────────────────────────

    [Fact]
    public async Task Public_tier_is_visible_to_anonymous()
    {
        var (owner, ownerId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(ownerId);
        await SetSectionAsync(owner, "certificates", "public");

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
    }

    [Fact]
    public async Task Only_me_tier_is_hidden_from_everyone_but_the_owner()
    {
        var (owner, ownerId) = await LoginAsync(NextPhone());
        var (stranger, _) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(ownerId);
        await SetSectionAsync(owner, "certificates", "only_me");

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await anon.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
        // The owner still sees their own section — otherwise "only me" would mean "nobody".
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
    }

    [Fact]
    public async Task Connections_tier_is_visible_only_to_an_accepted_ally()
    {
        var (owner, ownerId) = await LoginAsync(NextPhone());
        var (ally, allyId) = await LoginAsync(NextPhone());
        var (stranger, _) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(ownerId);
        await ClaimAsync(allyId);
        await SetSectionAsync(owner, "certificates", "connections");

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await anon.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ally.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);

        // A *pending* request must not grant access — only an accepted connection does.
        var connectionId = (await Json(await ally.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = ownerId })))
            .GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await ally.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);

        await owner.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        Assert.Equal(HttpStatusCode.OK, (await ally.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
    }

    [Fact]
    public async Task Event_participants_tier_is_visible_only_to_someone_who_shares_an_event()
    {
        var (owner, ownerId) = await LoginAsync(NextPhone());
        var (coParticipant, coId) = await LoginAsync(NextPhone());
        var (stranger, _) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(ownerId);
        await SeedSharedEventAsync(ownerId, coId);
        await SetSectionAsync(owner, "certificates", "event_participants");

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await anon.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coParticipant.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
    }

    /// <summary>The whole-profile gate. Not visible ⇒ 404, never 403 (D-018) — a restricted profile must
    /// be indistinguishable from one that does not exist.</summary>
    [Fact]
    public async Task Profile_section_set_to_connections_returns_404_to_a_stranger_and_200_to_an_ally()
    {
        var (owner, ownerId) = await LoginAsync(NextPhone());
        var (ally, allyId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(ownerId);
        await ClaimAsync(allyId);

        var connectionId = (await Json(await ally.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = ownerId })))
            .GetProperty("id").GetGuid();
        await owner.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        await SetSectionAsync(owner, "profile", "connections");

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/v1/public/users/{username}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ally.GetAsync($"/v1/public/users/{username}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/v1/public/users/{username}")).StatusCode);
    }

    /// <summary>The allies route was the one endpoint in the public-profile group that never received
    /// the viewer, and it gated on the legacy <c>ShowAllies</c>/<c>ProfilePublic</c> columns rather than
    /// the resolver. A Network tier of Connections dual-writes <c>ShowAllies=false</c>, so it returned
    /// an empty list to <b>everyone — the owner and their accepted allies included</b>. Two of the four
    /// tiers were dead on that surface.</summary>
    [Fact]
    public async Task Allies_section_set_to_connections_is_visible_to_an_ally_and_to_the_owner()
    {
        var (owner, ownerId) = await LoginAsync(NextPhone());
        var (ally, allyId) = await LoginAsync(NextPhone());
        var (stranger, _) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(ownerId);
        await ClaimAsync(allyId);

        var connectionId = (await Json(await ally.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = ownerId })))
            .GetProperty("id").GetGuid();
        await owner.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        await SetSectionAsync(owner, "network", "connections");

        // The connection exists and is accepted, so an entitled viewer sees exactly one card. Asserting
        // the count — not merely a 200 — is the point: the defect returned 200 with an empty body.
        var seen = await Json(await ally.GetAsync($"/v1/public/users/{username}/allies"));
        Assert.Equal(1, seen.GetArrayLength());

        var mine = await Json(await owner.GetAsync($"/v1/public/users/{username}/allies"));
        Assert.Equal(1, mine.GetArrayLength());

        var anon = _factory.CreateClient();
        Assert.Empty((await Json(await anon.GetAsync($"/v1/public/users/{username}/allies"))).EnumerateArray());
        Assert.Empty((await Json(await stranger.GetAsync($"/v1/public/users/{username}/allies"))).EnumerateArray());
    }

    /// <summary>The profile-level gate reaches the allies route too: a profile restricted to connections
    /// is 404 there, not an empty 200 — same D-018 rule as the profile root.</summary>
    [Fact]
    public async Task Allies_route_404s_when_the_profile_section_is_hidden_from_the_viewer()
    {
        var (owner, ownerId) = await LoginAsync(NextPhone());
        var (ally, allyId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(ownerId);
        await ClaimAsync(allyId);

        var connectionId = (await Json(await ally.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = ownerId })))
            .GetProperty("id").GetGuid();
        await owner.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        await SetSectionAsync(owner, "profile", "connections");

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/v1/public/users/{username}/allies")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ally.GetAsync($"/v1/public/users/{username}/allies")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/v1/public/users/{username}/allies")).StatusCode);
    }

    // ── Migration guarantee: legacy booleans still decide when no override exists ──

    /// <summary>The reversibility contract. A user who never touches the new settings must resolve
    /// exactly as they did before D-221, driven by the four booleans alone.</summary>
    [Theory]
    [InlineData(true, false, true, true)]     // the shipped defaults
    [InlineData(true, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, true, true)]
    public async Task Legacy_booleans_still_decide_when_no_section_override_is_stored(
        bool profilePublic, bool showAttended, bool showCertificates, bool showAllies)
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == userId);
            user.ProfilePublic = profilePublic;
            user.ShowAttended = showAttended;
            user.ShowCertificates = showCertificates;
            user.ShowAllies = showAllies;
            user.SectionVisibilityJson = null;      // the pre-migration state
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var profile = await anon.GetAsync($"/v1/public/users/{username}");
        Assert.Equal(profilePublic ? HttpStatusCode.OK : HttpStatusCode.NotFound, profile.StatusCode);

        if (!profilePublic) return;   // every sub-resource is 404 behind a hidden profile

        Assert.Equal(showAttended ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
            (await anon.GetAsync($"/v1/public/users/{username}/events?type=attended")).StatusCode);
        Assert.Equal(showCertificates ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
            (await anon.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
    }

    /// <summary>Dual-write: setting a section writes the legacy boolean back, so a rollback to the
    /// pre-D-221 read path sees the same answer.</summary>
    [Fact]
    public async Task Setting_a_section_dual_writes_the_legacy_boolean()
    {
        var (client, userId) = await LoginAsync(NextPhone());
        await SetSectionAsync(client, "attended", "public");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        Assert.True(user.ShowAttended);
        Assert.Contains("attended", user.SectionVisibilityJson);
    }

    /// <summary>A tier the boolean cannot express is strictly more private than Public, so it must
    /// dual-write to false — a rollback then over-hides rather than over-shares.</summary>
    [Fact]
    public async Task A_restricted_tier_dual_writes_the_boolean_to_false()
    {
        var (client, userId) = await LoginAsync(NextPhone());
        await SetSectionAsync(client, "certificates", "connections");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        Assert.False(user.ShowCertificates);
    }

    [Fact]
    public async Task An_unknown_section_or_tier_is_rejected()
    {
        var (client, _) = await LoginAsync(NextPhone());

        var badSection = await client.PatchAsJsonAsync("/v1/me/privacy", new
        {
            sections = new Dictionary<string, string> { ["bank_details"] = "public" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, badSection.StatusCode);

        var badTier = await client.PatchAsJsonAsync("/v1/me/privacy", new
        {
            sections = new Dictionary<string, string> { ["certificates"] = "friends_of_friends" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, badTier.StatusCode);
    }

    /// <summary>A malformed stored value must not throw the profile away — it falls back to the boolean
    /// chain, which is the pre-D-221 answer.</summary>
    [Fact]
    public async Task A_corrupt_section_visibility_value_falls_back_instead_of_failing()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == userId);
            user.SectionVisibilityJson = "{\"certificates\":\"not_a_tier\",\"nonsense\":\"public\"}";
            user.ShowCertificates = true;
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/v1/public/users/{username}/certificates")).StatusCode);
    }

    // ── Trust signals + provenance ───────────────────────────────────────────

    /// <summary>Only positive signals are ever emitted, and none of the moderation-internal fields the
    /// architecture review excluded may appear on a public profile.</summary>
    [Fact]
    public async Task The_public_profile_exposes_positive_trust_signals_and_no_moderation_internals()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        var anon = _factory.CreateClient();
        var profile = await Json(await anon.GetAsync($"/v1/public/users/{username}"));
        var verification = profile.GetProperty("verification");

        foreach (var expected in new[]
                 {
                     "identity_verified", "verified_member", "organizer", "verified_certificates",
                     "years_on_platform", "phone_verified", "email_verified", "speaker_verified",
                     "community_verified",
                 })
        {
            Assert.True(verification.TryGetProperty(expected, out _), $"missing trust signal: {expected}");
        }

        // Phone is true by construction — the account only exists because an OTP login succeeded.
        Assert.True(verification.GetProperty("phone_verified").GetBoolean());
        // A fresh account has proven nothing else.
        Assert.False(verification.GetProperty("email_verified").GetBoolean());
        Assert.False(verification.GetProperty("speaker_verified").GetBoolean());
        Assert.False(verification.GetProperty("community_verified").GetBoolean());

        var raw = profile.GetRawText();
        foreach (var forbidden in new[] { "risk_score", "risk_level", "fraud", "report_count", "reports", "moderation", "suspended", "banned", "trusted_device" })
        {
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task The_public_profile_declares_provenance_for_every_field_category()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        var anon = _factory.CreateClient();
        var profile = await Json(await anon.GetAsync($"/v1/public/users/{username}"));
        var meta = profile.GetProperty("_meta");

        // Self-declared must never be labelled as proof, and derived must never be labelled verified.
        Assert.Equal("self_declared", meta.GetProperty("bio").GetString());
        Assert.Equal("self_declared", meta.GetProperty("headline").GetString());
        Assert.Equal("self_declared", meta.GetProperty("college").GetString());
        Assert.Equal("verified", meta.GetProperty("stats").GetString());
        Assert.Equal("verified", meta.GetProperty("verification").GetString());
        Assert.Equal("derived", meta.GetProperty("summary").GetString());
        Assert.Equal("derived", meta.GetProperty("identity_labels").GetString());

        // The frozen root keeps every pre-D-221 key alongside the new _meta object.
        foreach (var key in new[] { "id", "name", "username", "headline", "bio", "stats", "verification", "organizations" })
        {
            Assert.True(profile.TryGetProperty(key, out _), $"root response lost key: {key}");
        }
    }
}
