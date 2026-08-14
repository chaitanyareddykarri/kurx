using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Kurx.Tests;

/// <summary>V3 §13.1/§20 (Phase 15) — the activated Template system + generated workspace. Templates are scoped
/// (PLATFORM|ORG|UNIT|PERSONAL) + versioned; every ConfigJson is validated against the closed capability registry;
/// creating an event from a template snapshots its capability preset + declarative defaults + records the version;
/// the workspace is generated from workspace_tab declarations and carries the projected §14.2 publish checklist.
/// Real HTTP / real kurx_test.</summary>
public class TemplateActivationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public TemplateActivationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var category = new EventCategory { Level = CategoryLevel.Category, Name = "Template Cat", Slug = "template-cat" };
                db.EventCategories.Add(category);
                db.SaveChanges();
                _categoryId = category.Id;
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private const string JwtSecret = "dev-only-secret-change-me-0123456789abcdef";
    private const string Issuer = "kurx";
    private const string Audience = "kurx-app";

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    private static string TokenFor(Guid userId, params (string Type, string Value)[] extra)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(extra.Select(c => new Claim(c.Type, c.Value)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var jwt = new JwtSecurityToken(Issuer, Audience, claims,
            notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private async Task<HttpClient> AdminAsync(string phone)
    {
        var client = await LoginAsync(phone);
        var userId = (await Json(await client.GetAsync("/v1/me"))).GetProperty("id").GetGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.PlatformRoles.Add(new PlatformRoleAssignment { UserId = userId, Role = PlatformRole.SuperAdmin });
        await db.SaveChangesAsync();
        return client;
    }

    private Task<Guid> CreateOrgAsync(HttpClient client, string name)
        => Task.FromResult(_factory.SeedVerifiedOrgForClient(client, name));

    private async Task<Guid> CreateEventAsync(HttpClient client, Guid orgId, Guid? templateId = null)
    {
        // Deliberately no timezone in the body — so a template default can win over the platform default.
        var res = await client.CreateEventAsync(orgId, new
        {
            title = "Ev " + Guid.NewGuid().ToString("N")[..6],
            description = "A great event with details.",
            categoryId = _categoryId,
            templateId,
            venueName = "Main Hall",
            city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20),
            endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    // ── system templates ──────────────────────────────────────────────────────────
    [Fact]
    public async Task System_templates_are_published_platform_versioned()
    {
        var anon = _factory.CreateClient();
        var templates = (await Json(await anon.GetAsync("/v1/templates"))).EnumerateArray().ToList();
        var conference = templates.First(t => t.GetProperty("name").GetString() == "Conference");
        Assert.Equal("Platform", conference.GetProperty("scope").GetString());
        Assert.Equal("Published", conference.GetProperty("state").GetString());
        Assert.Equal(1, conference.GetProperty("version").GetInt32());
        Assert.Equal(conference.GetProperty("id").GetGuid(), conference.GetProperty("root_template_id").GetGuid());
    }

    // ── CRUD + lifecycle ────────────────────────────────────────────────────────────
    [Fact]
    public async Task Org_template_appears_in_context_only_after_publish()
    {
        var owner = await LoginAsync("9600000001");
        var orgId = await CreateOrgAsync(owner, "Template Org 1");

        var created = await Json(await owner.PostAsJsonAsync("/v1/templates", new
        { scope = "Org", orgId, name = "My Conference", description = "d", kindSlug = (string?)null, configJson = "{}" }));
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Draft", created.GetProperty("state").GetString());

        // Draft not yet visible in the org catalog.
        var beforeNames = (await Json(await owner.GetAsync($"/v1/templates?orgId={orgId}")))
            .EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.DoesNotContain("My Conference", beforeNames);

        var published = await owner.PostAsync($"/v1/templates/{id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        var afterNames = (await Json(await owner.GetAsync($"/v1/templates?orgId={orgId}")))
            .EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("My Conference", afterNames);
    }

    [Fact]
    public async Task Published_version_is_immutable_and_new_version_reopens_a_draft()
    {
        var owner = await LoginAsync("9600000002");
        var orgId = await CreateOrgAsync(owner, "Template Org 2");
        var v1 = (await Json(await owner.PostAsJsonAsync("/v1/templates", new { scope = "Org", orgId, name = "Versioned" }))).GetProperty("id").GetGuid();
        await owner.PostAsync($"/v1/templates/{v1}/publish", null);

        // A Published version cannot be edited.
        var edit = await owner.PatchAsJsonAsync($"/v1/templates/{v1}", new { description = "changed" });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal("not_draft", (await Json(edit)).GetProperty("error").GetString());

        // New version → a v2 Draft in the same family.
        var v2 = await Json(await owner.PostAsync($"/v1/templates/{v1}/versions", null));
        Assert.Equal(2, v2.GetProperty("version").GetInt32());
        Assert.Equal("Draft", v2.GetProperty("state").GetString());
        Assert.Equal(v1, v2.GetProperty("root_template_id").GetGuid());   // same family root as v1

        // The v2 Draft is editable.
        var v2id = v2.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await owner.PatchAsJsonAsync($"/v1/templates/{v2id}", new { description = "v2" })).StatusCode);
    }

    // ── validation against the closed registry ──────────────────────────────────────
    [Fact]
    public async Task Create_rejects_unknown_capability()
    {
        var owner = await LoginAsync("9600000003");
        var orgId = await CreateOrgAsync(owner, "Template Org 3");
        var res = await owner.PostAsJsonAsync("/v1/templates", new
        { scope = "Org", orgId, name = "Bad Caps", configJson = "{\"capabilities\":{\"not-a-real-cap\":{\"state\":\"On\"}}}" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("unknown_capability", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Create_rejects_referencing_a_disabled_capability()
    {
        var owner = await LoginAsync("9600000004");
        var orgId = await CreateOrgAsync(owner, "Template Org 4");
        var res = await owner.PostAsJsonAsync("/v1/templates", new
        { scope = "Org", orgId, name = "Off Cap", configJson = "{\"capabilities\":{\"teams\":{\"state\":\"Off\"}}}" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("capability_disabled", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Create_rejects_malformed_config_json()
    {
        var owner = await LoginAsync("9600000005");
        var orgId = await CreateOrgAsync(owner, "Template Org 5");
        var res = await owner.PostAsJsonAsync("/v1/templates", new
        { scope = "Org", orgId, name = "Bad JSON", configJson = "{not json" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_config_json", (await Json(res)).GetProperty("error").GetString());
    }

    // ── scope authority ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Platform_scope_requires_admin()
    {
        var user = await LoginAsync("9600000006");
        var denied = await user.PostAsJsonAsync("/v1/templates", new { scope = "Platform", name = "Sneaky Platform" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var admin = await AdminAsync("9600000007");
        var ok = await admin.PostAsJsonAsync("/v1/templates", new { scope = "Platform", name = "Real Platform" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("Platform", (await Json(ok)).GetProperty("scope").GetString());
    }

    [Fact]
    public async Task Personal_template_is_private_to_its_owner()
    {
        var a = await LoginAsync("9600000008");
        var b = await LoginAsync("9600000009");
        var id = (await Json(await a.PostAsJsonAsync("/v1/templates", new { scope = "Personal", name = "A's Template" }))).GetProperty("id").GetGuid();

        // B cannot edit or delete A's personal template.
        Assert.Equal(HttpStatusCode.Forbidden, (await b.PatchAsJsonAsync($"/v1/templates/{id}", new { description = "hijack" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.DeleteAsync($"/v1/templates/{id}")).StatusCode);
        // A can.
        Assert.Equal(HttpStatusCode.OK, (await a.PatchAsJsonAsync($"/v1/templates/{id}", new { description = "mine" })).StatusCode);
    }

    // ── snapshot at event creation ──────────────────────────────────────────────────
    [Fact]
    public async Task Event_from_template_snapshots_preset_defaults_and_version()
    {
        var owner = await LoginAsync("9600000010");
        var orgId = await CreateOrgAsync(owner, "Template Org 10");
        var root = (await Json(await owner.PostAsJsonAsync("/v1/templates", new
        {
            scope = "Org", orgId, name = "Hack Kit",
            configJson = "{\"capabilities\":{\"teams\":{\"state\":\"On\"}},\"timezone\":\"America/New_York\"}",
        }))).GetProperty("id").GetGuid();
        await owner.PostAsync($"/v1/templates/{root}/publish", null);

        var eventId = await CreateEventAsync(owner, orgId, root);

        // Declarative default applied: the template's timezone won over the platform default.
        var ev = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}"));
        Assert.Equal("America/New_York", ev.GetProperty("timezone").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // Version recorded for provenance.
        var version = await db.Events.Where(e => e.Id == eventId).Select(e => e.CreatedFromTemplateVersion).FirstAsync();
        Assert.Equal(1, version);
        // Capability preset overlaid onto the event's materialised set (teams is not a Kind default here).
        var teams = await db.EventCapabilities.FirstOrDefaultAsync(c => c.EventId == eventId && c.CapabilitySlug == "teams");
        Assert.NotNull(teams);
        Assert.Equal(CapabilityState.On, teams!.State);
    }

    [Fact]
    public async Task Event_from_unpublished_template_is_rejected()
    {
        var owner = await LoginAsync("9600000011");
        var orgId = await CreateOrgAsync(owner, "Template Org 11");
        var root = (await Json(await owner.PostAsJsonAsync("/v1/templates", new { scope = "Org", orgId, name = "Draft Only" }))).GetProperty("id").GetGuid();
        // NOT published.
        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Ev nope", description = "A great event with details.", categoryId = _categoryId, templateId = root,
            venueName = "Hall", city = "Vizag", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_template", (await Json(res)).GetProperty("error").GetString());
    }

    // ── generated workspace + publish checklist ─────────────────────────────────────
    [Fact]
    public async Task Workspace_is_generated_from_capability_tabs_with_a_publish_checklist()
    {
        var owner = await LoginAsync("9600000012");
        var orgId = await CreateOrgAsync(owner, "Template Org 12");
        var eventId = await CreateEventAsync(owner, orgId);
        // D-266 M5: the checklist below asserts this event SATISFIES the schedule gate. Institutional
        // authorization is now part of that gate, so it is seeded here — the subject is the generated
        // workspace projection, not which rules the gate contains.
        _factory.SeedApprovedEventAuthorization(eventId);

        var w = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/workspace"));
        // Universal capabilities give every event these tabs (generated, not hand-written).
        var tabs = w.GetProperty("tabs").EnumerateArray().Select(t => t.GetProperty("tab").GetString()).ToList();
        Assert.Contains("Registrations", tabs);

        // The checklist projects the forward §14.2 gates. From Draft: schedule + publish are the valid steps, and
        // this event satisfies both gates (description + venue + owner unit, no approval chain).
        var checklist = w.GetProperty("checklist").EnumerateArray()
            .ToDictionary(i => i.GetProperty("action").GetString()!, i => i.GetProperty("ok").GetBoolean());
        Assert.True(checklist.ContainsKey("schedule"));
        Assert.True(checklist["schedule"]);
        Assert.True(checklist.ContainsKey("publish"));
    }

    [Fact]
    public async Task Workspace_hides_from_non_members_as_404()
    {
        var owner = await LoginAsync("9600000013");
        var stranger = await LoginAsync("9600000014");
        var orgId = await CreateOrgAsync(owner, "Template Org 13");
        var eventId = await CreateEventAsync(owner, orgId);

        var res = await stranger.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/workspace");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── clone ───────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Clone_starts_a_new_family_as_a_draft()
    {
        var owner = await LoginAsync("9600000015");
        var orgId = await CreateOrgAsync(owner, "Template Org 15");
        var src = await Json(await owner.PostAsJsonAsync("/v1/templates", new { scope = "Org", orgId, name = "Cloneable" }));
        var srcId = src.GetProperty("id").GetGuid();
        await owner.PostAsync($"/v1/templates/{srcId}/publish", null);

        var clone = await Json(await owner.PostAsJsonAsync($"/v1/templates/{srcId}/clone", new { name = "My Copy" }));
        Assert.Equal("Draft", clone.GetProperty("state").GetString());
        Assert.Equal(1, clone.GetProperty("version").GetInt32());
        Assert.NotEqual(srcId, clone.GetProperty("root_template_id").GetGuid());   // a NEW family
        Assert.Equal("Personal", clone.GetProperty("scope").GetString());           // lands in the caller's own space
    }

    [Fact]
    public async Task Clone_of_another_users_personal_template_is_hidden()   // M2 — no personal-template disclosure
    {
        var a = await LoginAsync("9600000023");
        var b = await LoginAsync("9600000024");
        var id = (await Json(await a.PostAsJsonAsync("/v1/templates", new { scope = "Personal", name = "A Private" }))).GetProperty("id").GetGuid();
        var res = await b.PostAsJsonAsync($"/v1/templates/{id}/clone", new { name = "Stolen" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);   // existence hidden, config not exfiltrated
    }

    // ── concurrency (M5) ────────────────────────────────────────────────────────────
    [Fact]
    public async Task Parallel_version_creation_yields_one_winner_and_no_500()
    {
        var owner = await LoginAsync("9600000016");
        var orgId = await CreateOrgAsync(owner, "Template Org 16");
        var v1 = (await Json(await owner.PostAsJsonAsync("/v1/templates", new { scope = "Org", orgId, name = "Race Ver" }))).GetProperty("id").GetGuid();
        await owner.PostAsync($"/v1/templates/{v1}/publish", null);

        var results = await Task.WhenAll(
            owner.PostAsync($"/v1/templates/{v1}/versions", null),
            owner.PostAsync($"/v1/templates/{v1}/versions", null));

        Assert.DoesNotContain(results, r => (int)r.StatusCode >= 500);          // the loser is a clean conflict, never a 500
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(2, await db.EventTemplates.CountAsync(t => t.RootTemplateId == v1));   // v1 + exactly one v2 — the unique index held
    }

    [Fact]
    public async Task Parallel_publish_keeps_one_published_version_and_no_500()
    {
        var owner = await LoginAsync("9600000017");
        var orgId = await CreateOrgAsync(owner, "Template Org 17");
        var id = (await Json(await owner.PostAsJsonAsync("/v1/templates", new { scope = "Org", orgId, name = "Race Pub" }))).GetProperty("id").GetGuid();

        var results = await Task.WhenAll(
            owner.PostAsync($"/v1/templates/{id}/publish", null),
            owner.PostAsync($"/v1/templates/{id}/publish", null));

        Assert.DoesNotContain(results, r => (int)r.StatusCode >= 500);
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventTemplates.FirstAsync(x => x.Id == id);
        Assert.Equal(TemplateState.Published, t.State);
        Assert.Equal(1, await db.EventTemplates.CountAsync(x => x.RootTemplateId == id));   // no duplicate version from the race
    }

    // ── scope precedence (M5) ───────────────────────────────────────────────────────
    [Fact]
    public async Task Scope_precedence_lists_most_specific_first()
    {
        var admin = await AdminAsync("9600000018");
        var owner = await LoginAsync("9600000019");
        var orgId = await CreateOrgAsync(owner, "Template Org 19");

        Guid rootUnitId;
        using (var scope = _factory.Services.CreateScope())
            rootUnitId = await scope.ServiceProvider.GetRequiredService<IOrgUnitService>().EnsureRootAsync(orgId);

        async Task<Guid> Published(HttpClient c, object body)
        {
            var id = (await Json(await c.PostAsJsonAsync("/v1/templates", body))).GetProperty("id").GetGuid();
            await c.PostAsync($"/v1/templates/{id}/publish", null);
            return id;
        }
        var plat = await Published(admin, new { scope = "Platform", name = "ZZ Platform Prec" });
        var org = await Published(owner, new { scope = "Org", orgId, name = "ZZ Org Prec" });
        var unit = await Published(owner, new { scope = "Unit", orgId, orgUnitId = rootUnitId, name = "ZZ Unit Prec" });
        var pers = await Published(owner, new { scope = "Personal", name = "ZZ Personal Prec" });

        var list = (await Json(await owner.GetAsync($"/v1/templates?orgId={orgId}&orgUnitId={rootUnitId}"))).EnumerateArray().ToList();
        var mine = new[] { plat, org, unit, pers }.ToHashSet();
        var scopes = list.Where(t => mine.Contains(t.GetProperty("id").GetGuid())).Select(t => t.GetProperty("scope").GetString()).ToList();
        Assert.Equal(new[] { "Personal", "Unit", "Org", "Platform" }, scopes);   // most-specific first
    }

    // ── snapshot independence (M5) ──────────────────────────────────────────────────
    [Fact]
    public async Task Existing_event_is_unaffected_by_a_later_template_version()
    {
        var owner = await LoginAsync("9600000020");
        var orgId = await CreateOrgAsync(owner, "Template Org 20");
        var root = (await Json(await owner.PostAsJsonAsync("/v1/templates", new
        {
            scope = "Org", orgId, name = "Snap Indep",
            configJson = "{\"capabilities\":{\"teams\":{\"state\":\"On\"}},\"timezone\":\"America/New_York\"}",
        }))).GetProperty("id").GetGuid();
        await owner.PostAsync($"/v1/templates/{root}/publish", null);

        var eventId = await CreateEventAsync(owner, orgId, root);

        // Publish a DIFFERENT v2 (no teams, new timezone) — the existing event must not move.
        var v2 = (await Json(await owner.PostAsync($"/v1/templates/{root}/versions", null))).GetProperty("id").GetGuid();
        await owner.PatchAsJsonAsync($"/v1/templates/{v2}", new { configJson = "{\"timezone\":\"Europe/Paris\"}" });
        await owner.PostAsync($"/v1/templates/{v2}/publish", null);

        var ev = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}"));
        Assert.Equal("America/New_York", ev.GetProperty("timezone").GetString());   // still v1's snapshot

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, await db.Events.Where(e => e.Id == eventId).Select(e => e.CreatedFromTemplateVersion).FirstAsync());
        Assert.NotNull(await db.EventCapabilities.FirstOrDefaultAsync(c => c.EventId == eventId && c.CapabilitySlug == "teams"));
    }

    // ── deletion + archival (M5) ────────────────────────────────────────────────────
    [Fact]
    public async Task Delete_removes_unused_draft_family_but_refuses_in_use()
    {
        var owner = await LoginAsync("9600000021");
        var orgId = await CreateOrgAsync(owner, "Template Org 21");

        var draft = (await Json(await owner.PostAsJsonAsync("/v1/templates", new { scope = "Org", orgId, name = "Del Draft" }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"/v1/templates/{draft}")).StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            Assert.Equal(0, await db.EventTemplates.CountAsync(t => t.RootTemplateId == draft));
        }

        var used = (await Json(await owner.PostAsJsonAsync("/v1/templates", new { scope = "Org", orgId, name = "Del Used" }))).GetProperty("id").GetGuid();
        await owner.PostAsync($"/v1/templates/{used}/publish", null);
        await CreateEventAsync(owner, orgId, used);
        var refused = await owner.DeleteAsync($"/v1/templates/{used}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("template_in_use", (await Json(refused)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Archived_family_leaves_the_list_and_blocks_new_events()
    {
        var owner = await LoginAsync("9600000022");
        var orgId = await CreateOrgAsync(owner, "Template Org 22");
        var id = (await Json(await owner.PostAsJsonAsync("/v1/templates", new { scope = "Org", orgId, name = "Arch Me" }))).GetProperty("id").GetGuid();
        await owner.PostAsync($"/v1/templates/{id}/publish", null);

        var archived = await Json(await owner.PostAsync($"/v1/templates/{id}/archive", null));
        Assert.Equal("Archived", archived.GetProperty("state").GetString());

        var names = (await Json(await owner.GetAsync($"/v1/templates?orgId={orgId}"))).EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.DoesNotContain("Arch Me", names);   // only Published versions are listed

        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Arch ev", description = "A great event with details.", categoryId = _categoryId, templateId = id,
            venueName = "Hall", city = "Vizag", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_template", (await Json(res)).GetProperty("error").GetString());   // no Published version to snapshot
    }
}
