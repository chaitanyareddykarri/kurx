using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Regression cover for the Sponsors surface (2026-07-26 hardening pass). Every scenario here was
/// first verified by hand against a running server during the production audit; these tests exist so that
/// verification is repeatable rather than a one-off. They deliberately assert the *contract* — status codes
/// and error slugs — not internal state, so a refactor that preserves behaviour stays green.</summary>
public class SponsorHardeningTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public SponsorHardeningTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _counter;

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    private async Task<(HttpClient Client, Guid OrgId)> OwnerWithOrgAsync(string phone, string orgName)
    {
        var client = await LoginAsync(phone);
        return (client, _factory.SeedVerifiedOrgForClient(client, orgName));
    }

    private async Task<Guid> CategoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var name = $"SponsorCat {Interlocked.Increment(ref _counter)}";
        var category = new EventCategory { Level = CategoryLevel.Category, Name = name, Slug = name.ToLowerInvariant().Replace(" ", "-") };
        db.EventCategories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private async Task<Guid> EventAsync(HttpClient client, Guid orgId)
    {
        var ev = await Json(await client.CreateEventAsync(orgId, new
        {
            title = $"Sponsor Event {Interlocked.Increment(ref _counter)}",
            description = "desc",
            categoryId = await CategoryAsync(),
            venueName = "Hall",
            startsAt = DateTime.UtcNow.AddDays(5),
            endsAt = DateTime.UtcNow.AddDays(5).AddHours(2),
        }));
        return ev.GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> CreateSponsor(HttpClient c, Guid orgId, object body)
        => c.PostAsJsonAsync($"/v1/orgs/{orgId}/sponsors", body);

    private static async Task<string?> ErrorOf(HttpResponseMessage res)
        => (await Json(res)).TryGetProperty("error", out var e) ? e.GetString() : null;

    // ── CRUD ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_read_update_delete_round_trip()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9810000001", "CRUD Sponsors");

        var created = await Json(await CreateSponsor(client, orgId, new { name = "Acme", tier = "gold", priority = 2, website = "https://acme.test" }));
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("gold", created.GetProperty("tier").GetString());
        Assert.Equal(2, created.GetProperty("priority").GetInt32());

        // READ: a newly created sponsor must actually appear in the list — the failure mode that made
        // Templates unbuildable was a list query silently excluding freshly created rows.
        var list = await Json(await client.GetAsync($"/v1/orgs/{orgId}/sponsors"));
        Assert.Contains(list.EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);

        // UPDATE: omitted fields must be preserved, not nulled.
        var updated = await Json(await client.PatchAsJsonAsync($"/v1/orgs/{orgId}/sponsors/{id}", new { name = "Acme Renamed" }));
        Assert.Equal("Acme Renamed", updated.GetProperty("name").GetString());
        Assert.Equal("gold", updated.GetProperty("tier").GetString());
        Assert.Equal("https://acme.test", updated.GetProperty("website").GetString());

        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/v1/orgs/{orgId}/sponsors/{id}")).StatusCode);
        var after = await Json(await client.GetAsync($"/v1/orgs/{orgId}/sponsors"));
        Assert.DoesNotContain(after.EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);
    }

    // ── Event assignment ──────────────────────────────────────────────────────

    [Fact]
    public async Task Assign_is_idempotent_and_remove_detaches()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9810000002", "Assign Sponsors");
        var eventId = await EventAsync(client, orgId);
        var id = (await Json(await CreateSponsor(client, orgId, new { name = "Repeat Co" }))).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/sponsors", new { sponsorId = id })).StatusCode);
        // Assigning twice must stay a no-op rather than creating a second link row or erroring.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/sponsors", new { sponsorId = id })).StatusCode);

        var lineup = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/sponsors"));
        Assert.Single(lineup.EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);

        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/v1/orgs/{orgId}/events/{eventId}/sponsors/{id}")).StatusCode);
        var empty = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/sponsors"));
        Assert.DoesNotContain(empty.EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Removing_an_unassigned_sponsor_is_not_found()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9810000003", "Detach Sponsors");
        var eventId = await EventAsync(client, orgId);
        var id = (await Json(await CreateSponsor(client, orgId, new { name = "Never Attached" }))).GetProperty("id").GetGuid();

        var res = await client.DeleteAsync($"/v1/orgs/{orgId}/events/{eventId}/sponsors/{id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Invalid_sponsor_and_event_references_are_rejected()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9810000004", "Refs Sponsors");
        var eventId = await EventAsync(client, orgId);

        var badSponsor = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/sponsors", new { sponsorId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, badSponsor.StatusCode);
        Assert.Equal("invalid_sponsor", await ErrorOf(badSponsor));

        var id = (await Json(await CreateSponsor(client, orgId, new { name = "Orphan Ref" }))).GetProperty("id").GetGuid();
        var badEvent = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{Guid.NewGuid()}/sponsors", new { sponsorId = id });
        Assert.Equal(HttpStatusCode.NotFound, badEvent.StatusCode);
    }

    // ── Cascade ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deleting_a_sponsor_cascades_its_event_links()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9810000005", "Cascade Sponsors");
        var e1 = await EventAsync(client, orgId);
        var e2 = await EventAsync(client, orgId);
        var id = (await Json(await CreateSponsor(client, orgId, new { name = "Multi Event" }))).GetProperty("id").GetGuid();
        await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{e1}/sponsors", new { sponsorId = id });
        await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{e2}/sponsors", new { sponsorId = id });

        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/v1/orgs/{orgId}/sponsors/{id}")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.False(await db.EventSponsors.AnyAsync(x => x.SponsorId == id));   // no orphan link rows

        // A deleted sponsor must not be referenceable again.
        var reassign = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{e1}/sponsors", new { sponsorId = id });
        Assert.Equal(HttpStatusCode.BadRequest, reassign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/v1/orgs/{orgId}/sponsors/{id}")).StatusCode);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("x")]
    public async Task Blank_whitespace_and_too_short_names_are_rejected(string name)
    {
        var (client, orgId) = await OwnerWithOrgAsync("9810000006", "Name Sponsors");
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateSponsor(client, orgId, new { name })).StatusCode);
    }

    [Fact]
    public async Task Name_length_boundaries_are_enforced()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9810000007", "Length Sponsors");
        Assert.Equal(HttpStatusCode.OK, (await CreateSponsor(client, orgId, new { name = new string('a', 150) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateSponsor(client, orgId, new { name = new string('a', 151) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CreateSponsor(client, orgId, new { name = "ab" })).StatusCode);
    }

    [Fact]
    public async Task Invalid_tier_is_rejected_and_casing_is_accepted()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9810000008", "Tier Sponsors");

        // The slug is validation_failed, NOT the service's own invalid_tier: SponsorBodyValidator runs
        // first and rejects the tier at the boundary, which makes SponsorService's invalid_tier branch
        // unreachable over HTTP. Pinned deliberately — this is the contract a client actually sees.
        var bad = await CreateSponsor(client, orgId, new { name = "Bad Tier", tier = "diamond" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("validation_failed", await ErrorOf(bad));

        // Parsing is case-insensitive but the response is always lowercase — the admin client relies on
        // that normalisation to round-trip a <select> value.
        var ok = await Json(await CreateSponsor(client, orgId, new { name = "Case Tier", tier = "GOLD" }));
        Assert.Equal("gold", ok.GetProperty("tier").GetString());
    }

    [Fact]
    public async Task Malformed_payloads_return_400_not_500()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9810000009", "Malformed Sponsors");

        // Unparseable JSON, a wrong property type, and an Int32 overflow all used to surface as 500 with an
        // Error-level stack trace, letting any authenticated caller manufacture alert noise.
        var badJson = await client.PostAsync($"/v1/orgs/{orgId}/sponsors",
            new StringContent("{\"name\":", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, badJson.StatusCode);

        var wrongType = await client.PostAsync($"/v1/orgs/{orgId}/sponsors",
            new StringContent("{\"name\":123}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);

        var overflow = await client.PostAsync($"/v1/orgs/{orgId}/sponsors",
            new StringContent("{\"name\":\"Overflow\",\"priority\":1099511627776}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, overflow.StatusCode);
    }

    // ── Authorization + isolation ─────────────────────────────────────────────

    [Fact]
    public async Task Anonymous_callers_are_unauthorized()
    {
        var (owner, orgId) = await OwnerWithOrgAsync("9810000010", "Anon Sponsors");
        await CreateSponsor(owner, orgId, new { name = "Private Co" });

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/v1/orgs/{orgId}/sponsors")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateSponsor(anon, orgId, new { name = "Hacked" })).StatusCode);
    }

    [Fact]
    public async Task Non_member_cannot_read_or_mutate_another_orgs_sponsors()
    {
        var (owner, orgId) = await OwnerWithOrgAsync("9810000011", "Owned Sponsors");
        var id = (await Json(await CreateSponsor(owner, orgId, new { name = "Owned Co" }))).GetProperty("id").GetGuid();

        var stranger = await LoginAsync("9810000012");
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/v1/orgs/{orgId}/sponsors")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateSponsor(stranger, orgId, new { name = "Injected" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await stranger.PatchAsJsonAsync($"/v1/orgs/{orgId}/sponsors/{id}", new { name = "Pwned" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.DeleteAsync($"/v1/orgs/{orgId}/sponsors/{id}")).StatusCode);
    }

    [Fact]
    public async Task Cross_organization_isolation_is_enforced()
    {
        var (ownerA, orgA) = await OwnerWithOrgAsync("9810000013", "Isolation A");
        var (ownerB, orgB) = await OwnerWithOrgAsync("9810000014", "Isolation B");
        var sponsorA = (await Json(await CreateSponsor(ownerA, orgA, new { name = "A Co" }))).GetProperty("id").GetGuid();
        var eventB = await EventAsync(ownerB, orgB);

        // B may not attach A's sponsor to B's event.
        var cross = await ownerB.PostAsJsonAsync($"/v1/orgs/{orgB}/events/{eventB}/sponsors", new { sponsorId = sponsorA });
        Assert.Equal(HttpStatusCode.BadRequest, cross.StatusCode);
        Assert.Equal("invalid_sponsor", await ErrorOf(cross));

        // A's own list must not leak into B's.
        var listB = await Json(await ownerB.GetAsync($"/v1/orgs/{orgB}/sponsors"));
        Assert.DoesNotContain(listB.EnumerateArray(), s => s.GetProperty("id").GetGuid() == sponsorA);
    }

    [Fact]
    public async Task Route_orgId_is_enforced_on_update_and_delete()
    {
        var (ownerA, orgA) = await OwnerWithOrgAsync("9810000015", "Route A");
        var (_, orgB) = await OwnerWithOrgAsync("9810000016", "Route B");
        var id = (await Json(await CreateSponsor(ownerA, orgA, new { name = "Route Co" }))).GetProperty("id").GetGuid();

        // The route used to be decorative: the service looked the sponsor up by id and ignored {orgId}
        // entirely, so a mismatched org in the URL still mutated the record. It is now enforced, and the
        // mismatch is not_found so the endpoint never confirms the sponsor exists under a guessed org.
        Assert.Equal(HttpStatusCode.NotFound,
            (await ownerA.PatchAsJsonAsync($"/v1/orgs/{orgB}/sponsors/{id}", new { name = "Wrong Org" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerA.DeleteAsync($"/v1/orgs/{orgB}/sponsors/{id}")).StatusCode);

        // …and the correct org still works.
        Assert.Equal(HttpStatusCode.OK,
            (await ownerA.PatchAsJsonAsync($"/v1/orgs/{orgA}/sponsors/{id}", new { name = "Right Org" })).StatusCode);
    }

    // ── Concurrency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Concurrent_updates_are_last_write_wins_without_corruption()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9810000017", "Concurrent Sponsors");
        var id = (await Json(await CreateSponsor(client, orgId, new { name = "Contended", tier = "bronze", priority = 0 }))).GetProperty("id").GetGuid();

        // There is no rowversion/If-Match on this resource (documented decision): simultaneous edits are
        // last-write-wins. What must NOT happen is a 500, a lost row, or a half-applied blend of the two
        // payloads — this pins that guarantee.
        var a = client.PatchAsJsonAsync($"/v1/orgs/{orgId}/sponsors/{id}", new { name = "Writer A", tier = "gold", priority = 1 });
        var b = client.PatchAsJsonAsync($"/v1/orgs/{orgId}/sponsors/{id}", new { name = "Writer B", tier = "silver", priority = 2 });
        var responses = await Task.WhenAll(a, b);
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var final = (await Json(await client.GetAsync($"/v1/orgs/{orgId}/sponsors")))
            .EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == id);
        var name = final.GetProperty("name").GetString();
        var tier = final.GetProperty("tier").GetString();
        var priority = final.GetProperty("priority").GetInt32();

        // Whichever writer landed last, the row must be entirely that writer's payload — never a mix.
        Assert.True((name, tier, priority) is ("Writer A", "gold", 1) or ("Writer B", "silver", 2),
            $"row was blended across concurrent writers: {name}/{tier}/{priority}");
    }

    // ── Audit ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_mutation_writes_an_audit_row_with_before_and_after()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9810000018", "Audit Sponsors");
        var eventId = await EventAsync(client, orgId);
        var id = (await Json(await CreateSponsor(client, orgId, new { name = "Audited Co", tier = "bronze" }))).GetProperty("id").GetGuid();
        // Assert each step: a silently-failing mutation would otherwise look like a missing audit row.
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/v1/orgs/{orgId}/sponsors/{id}", new { name = "Audited Renamed", tier = "gold" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/sponsors", new { sponsorId = id })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/v1/orgs/{orgId}/events/{eventId}/sponsors/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/v1/orgs/{orgId}/sponsors/{id}")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var rows = await db.AuditLogs.AsNoTracking().Where(a => a.EntityId == id).ToListAsync();

        foreach (var action in new[] { "sponsor.create", "sponsor.update", "sponsor.event.assign", "sponsor.event.remove", "sponsor.delete" })
            Assert.Contains(rows, r => r.Action == action);

        // The update row must carry both sides so an auditor can see what actually changed.
        // The envelope stores the DOMAIN enum name ("Bronze"), not the API's lowercased wire value
        // ("bronze") — the audit trail records what the model held, not how a response rendered it.
        var update = rows.Single(r => r.Action == "sponsor.update").DetailsJson ?? "";
        Assert.True(update.Contains("\"before\"") && update.Contains("\"after\""),
            $"update envelope must carry both sides. Actual: {update}");
        Assert.Contains("Bronze", update);   // before
        Assert.Contains("Gold", update);     // after

        // A hard delete destroys the row, so its audit entry is the only surviving record of prior state.
        Assert.Contains("Audited Renamed", rows.Single(r => r.Action == "sponsor.delete").DetailsJson ?? "");
    }
}
