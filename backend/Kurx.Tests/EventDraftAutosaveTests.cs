using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-266 M8 — the wizard's autosave.
///
/// <para>The load-bearing property is that a draft is <b>opaque, per-user client state</b>: the server
/// stores it verbatim, never parses it, and never lets it reach the event or the publish path. These cases
/// pin exactly that, because the tempting "optimisation" is to give it columns — which would create a
/// second, weaker definition of an event.</para></summary>
public class EventDraftAutosaveTests(KurxApiFactory factory) : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory = factory;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var c = _factory.CreateClient();
        await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var t = await Json(await c.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return (c, t.GetProperty("user_id").GetGuid());
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == "hackathon")
            .Select(c => new { c.Id, c.ParentId }).FirstAsync();

        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Draft " + Guid.NewGuid().ToString("N")[..6], description = "a real description",
            categoryId = t.ParentId!.Value, typeId = t.Id, venueName = "Hall", city = "C",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(3),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> Save(HttpClient c, Guid eventId, string payload, string? step = "details")
        => c.PutAsJsonAsync($"/v1/events/{eventId}/draft", new { payloadJson = payload, stepKey = step });

    /// <summary>Normalises both sides through one serializer so the comparison is about CONTENT.
    ///
    /// <para>The column is <c>jsonb</c>, which reparses and reformats: <c>{"v":2}</c> comes back as
    /// <c>{"v": 2}</c>, and key order is not preserved. That is the right trade — a form restore parses the
    /// JSON anyway, and jsonb adds a database-level guarantee it is valid — but it does mean the round trip
    /// is semantic, not byte-for-byte, and asserting the latter would pin a property the storage does not
    /// actually have.</para></summary>
    private static string Normalize(string json) => Canonical(JsonDocument.Parse(json).RootElement);

    /// <summary>Recursively sorts object keys. Serializing a <see cref="JsonElement"/> preserves the
    /// document's own key order, which is NOT enough here: jsonb stores object keys in its own order
    /// (shortest first, then bytewise), so `{"title":…,"capacity":…}` comes back reordered. Comparing
    /// without sorting fails on a difference that carries no meaning — JSON objects are unordered by
    /// spec, and the property under test is that the CONTENT survived.</summary>
    private static string Canonical(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", e.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", e.EnumerateArray().Select(Canonical)) + "]",
        _ => e.GetRawText(),
    };

    [Fact]
    public async Task A_draft_round_trips_intact_and_resumes_on_its_step()
    {
        var (owner, _) = await LoginAsync("9707000001");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Autosave College");
        var id = await CreateEventAsync(owner, orgId);

        // Deliberately half-filled and internally inconsistent — exactly the state autosave exists for.
        const string payload = """{"title":"Half typed","capacity":null,"tags":["a"],"unknownField":true}""";
        Assert.Equal(HttpStatusCode.OK, (await Save(owner, id, payload, "windows")).StatusCode);

        var got = await Json(await owner.GetAsync($"/v1/events/{id}/draft"));
        Assert.Equal("windows", got.GetProperty("step_key").GetString());
        // Content is preserved intact — including `unknownField`, which the server must never strip: it
        // does not know or care what the wizard's shape is.
        Assert.Equal(Normalize(payload), Normalize(got.GetProperty("payload_json").GetString()!));
    }

    [Fact]
    public async Task Saving_again_replaces_rather_than_accumulating()
    {
        var (owner, _) = await LoginAsync("9707000002");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Upsert College");
        var id = await CreateEventAsync(owner, orgId);

        await Save(owner, id, """{"v":1}""");
        await Save(owner, id, """{"v":2}""");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, await db.EventDraftSnapshots.AsNoTracking().CountAsync(d => d.EventId == id));

        var got = await Json(await owner.GetAsync($"/v1/events/{id}/draft"));
        Assert.Equal(Normalize("""{"v":2}"""), Normalize(got.GetProperty("payload_json").GetString()!));
    }

    [Fact]
    public async Task No_draft_answers_204_not_404()
    {
        var (owner, _) = await LoginAsync("9707000003");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Empty Draft College");
        var id = await CreateEventAsync(owner, orgId);

        // The event exists and the caller may edit it; nothing has been autosaved. A 404 would be
        // indistinguishable from "no such event".
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync($"/v1/events/{id}/draft")).StatusCode);
    }

    /// <summary>Two managers mid-edit each keep their own form. Merging would produce something neither
    /// typed; last-write-wins would silently discard one person's work.</summary>
    [Fact]
    public async Task Drafts_are_per_user_and_never_leak_between_managers()
    {
        var (owner, _) = await LoginAsync("9707000004");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Two Manager College");
        var id = await CreateEventAsync(owner, orgId);

        var (second, secondId) = await LoginAsync("9707000005");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.Memberships.Add(new Kurx.Domain.Entities.Membership
            {
                OrgId = orgId, UserId = secondId, Role = Kurx.Domain.Enums.OrgRole.Manager, IsVerified = true,
            });
            await db.SaveChangesAsync();
        }

        await Save(owner, id, """{"who":"owner"}""");
        await Save(second, id, """{"who":"manager"}""");

        Assert.Contains("owner",
            (await Json(await owner.GetAsync($"/v1/events/{id}/draft"))).GetProperty("payload_json").GetString());
        Assert.Contains("manager",
            (await Json(await second.GetAsync($"/v1/events/{id}/draft"))).GetProperty("payload_json").GetString());
    }

    /// <summary>A draft is client state and must never touch the event. If it did, an organiser could
    /// change a live event by typing in a form they never submitted.</summary>
    [Fact]
    public async Task A_draft_never_modifies_the_event()
    {
        var (owner, _) = await LoginAsync("9707000006");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Isolation College");
        var id = await CreateEventAsync(owner, orgId);

        string TitleAsync()
        {
            using var scope = _factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<KurxDbContext>()
                .Events.AsNoTracking().Where(e => e.Id == id).Select(e => e.Title).First();
        }
        var before = TitleAsync();

        await Save(owner, id, """{"title":"THIS MUST NOT BE APPLIED","capacity":99999}""");

        Assert.Equal(before, TitleAsync());
    }

    [Fact]
    public async Task A_stranger_cannot_read_or_write_a_draft()
    {
        var (owner, _) = await LoginAsync("9707000007");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Draft Authz College");
        var id = await CreateEventAsync(owner, orgId);
        var (stranger, _) = await LoginAsync("9707000008");

        // 404, never 403 — a 403 confirms the draft event exists (D-018).
        Assert.Equal(HttpStatusCode.NotFound, (await Save(stranger, id, """{"x":1}""")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/v1/events/{id}/draft")).StatusCode);
    }

    [Fact]
    public async Task A_payload_that_is_not_json_is_refused()
    {
        var (owner, _) = await LoginAsync("9707000009");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Bad Payload College");
        var id = await CreateEventAsync(owner, orgId);

        Assert.Equal(HttpStatusCode.BadRequest, (await Save(owner, id, "not json at all")).StatusCode);
    }

    /// <summary>Autosave is the one call that genuinely races with itself: the same organiser with the
    /// wizard open in two tabs fires it on a timer from both. Both find no row, both insert, and the unique
    /// (EventId, UserId) index rejects the loser — which must not cost the organiser their typing.</summary>
    [Fact]
    public async Task Concurrent_first_saves_leave_one_draft_and_no_error()
    {
        var (owner, _) = await LoginAsync("9703000040");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "RaceDraft College");
        var id = await CreateEventAsync(owner, orgId);

        // Ten simultaneous FIRST saves — no row exists yet, so every one of them tries to insert.
        var payloads = Enumerable.Range(0, 10).Select(i => $"{{\"tab\":{i}}}").ToList();
        var results = await Task.WhenAll(payloads.Select(p => Save(owner, id, p)));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // Exactly one row survives, and it holds one of the payloads that was actually sent.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var rows = await db.EventDraftSnapshots.Where(d => d.EventId == id).ToListAsync();
        Assert.Single(rows);
        Assert.Contains(Normalize(rows[0].PayloadJson), payloads.Select(Normalize));
    }
}
