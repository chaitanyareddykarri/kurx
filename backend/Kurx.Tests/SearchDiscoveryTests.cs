using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>V3 §15 Discovery &amp; Search (Phase 16). The outbox-fed index (Postgres FTS + trigram) backs the existing
/// public discovery endpoints: alias search, typo tolerance, typed filters, deterministic ranking, the discovery
/// collapse rules (a Festival is one card, a RECURRING series is one listing), and the eligibility-aware feed that
/// never leaks an internal event's existence. Real HTTP / real kurx_test; the index is drained through the real
/// OutboxDispatchJob so the whole outbox → projector path is exercised.</summary>
public class SearchDiscoveryTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public SearchDiscoveryTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var category = new EventCategory { Level = CategoryLevel.Category, Name = "Search Cat", Slug = "search-cat" };
                db.EventCategories.Add(category);
                db.SaveChanges();
                _categoryId = category.Id;
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

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

    private Task<Guid> CreateOrgAsync(HttpClient client, string name) => Task.FromResult(_factory.SeedVerifiedOrgForClient(client, name));

    private async Task<Guid> CreateEventAsync(HttpClient client, Guid orgId, string title, Guid? parentEventId = null,
        string city = "Vizag", int days = 20, Guid? categoryId = null)
    {
        var res = await client.CreateEventAsync(orgId, new
        {
            title,
            description = "A discoverable event with plenty of details for search.",
            categoryId = categoryId ?? _categoryId,
            parentEventId,
            venueName = "Main Hall",
            city,
            startsAt = DateTime.UtcNow.AddDays(days),
            endsAt = DateTime.UtcNow.AddDays(days).AddHours(4),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    /// <summary>A second category, created on demand, so a filter has something to exclude.</summary>
    private Guid OtherCategoryAsync(string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var existing = db.EventCategories.FirstOrDefault(c => c.Slug == slug);
        if (existing is not null) return existing.Id;
        var cat = new EventCategory { Level = CategoryLevel.Category, Name = slug, Slug = slug };
        db.EventCategories.Add(cat);
        db.SaveChanges();
        return cat.Id;
    }

    /// <summary>D-266 M5 added institutional authorization to what "publishable" means. Every case in this
    /// class is about DISCOVERY — what a published event looks like in search — so the authorization is
    /// fixture setup. Non-static now because seeding needs the factory.</summary>
    private Task Publish(HttpClient client, Guid orgId, Guid eventId)
    {
        _factory.SeedApprovedEventAuthorization(eventId);
        return client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
    }

    // Drain the outbox through the real dispatcher — this is how search.reindex messages reach the projector.
    private async Task DrainOutboxAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutboxDispatchJob>().RunAsync();
    }

    private async Task ProjectAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISearchIndexService>().ProjectAsync(eventId);
    }

    private async Task RefreshSignalsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISearchIndexService>().RefreshSignalsAsync();
    }

    private static List<string> Titles(JsonElement arr) => arr.EnumerateArray().Select(e => e.GetProperty("title").GetString()!).ToList();
    private static async Task<List<string>> SearchTitles(HttpClient c, string query, string? sort = null)
        => Titles((await Json(await c.GetAsync(
            $"/v1/events?q={Uri.EscapeDataString(query)}{(sort is null ? "" : $"&sort={sort}")}"))).GetProperty("items"));

    // ── indexing + FTS ───────────────────────────────────────────────────────────
    [Fact]
    public async Task Published_event_is_searchable_and_a_draft_is_not()
    {
        var owner = await LoginAsync("9710000001");
        var orgId = await CreateOrgAsync(owner, "Search Org 1");
        var token = "Quasar" + Guid.NewGuid().ToString("N")[..6];
        var published = await CreateEventAsync(owner, orgId, $"{token} Summit");
        await Publish(owner, orgId, published);
        var draft = await CreateEventAsync(owner, orgId, $"{token} Draft");   // never published
        await DrainOutboxAsync();

        var titles = await SearchTitles(_factory.CreateClient(), token);
        Assert.Contains($"{token} Summit", titles);
        Assert.DoesNotContain($"{token} Draft", titles);
    }

    [Fact]
    public async Task Search_matches_a_retired_kind_alias()
    {
        var owner = await LoginAsync("9710000002");
        var orgId = await CreateOrgAsync(owner, "Search Org 2");
        var token = "Nebula" + Guid.NewGuid().ToString("N")[..6];
        var id = await CreateEventAsync(owner, orgId, $"{token} Build");
        await Publish(owner, orgId, id);

        string alias;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ev = await db.Events.FirstAsync(e => e.Id == id);
            ev.KindSlug = "hackathon";                       // stamp a real kind whose retired names are seeded aliases
            await db.SaveChangesAsync();
            alias = await db.KindAliases.Where(a => a.KindSlug == "hackathon").Select(a => a.Alias).FirstAsync();
        }
        await ProjectAsync(id);   // reindex so the kind's aliases enter the document

        // Searching a retired type name (e.g. "Ideathon") finds the Hackathon-kinded event.
        var titles = await SearchTitles(_factory.CreateClient(), alias);
        Assert.Contains($"{token} Build", titles);
    }

    [Fact]
    public async Task Fuzzy_search_tolerates_a_typo()
    {
        var owner = await LoginAsync("9710000003");
        var orgId = await CreateOrgAsync(owner, "Search Org 3");
        var token = "Photon";
        var id = await CreateEventAsync(owner, orgId, $"{token}mania Festival");
        await Publish(owner, orgId, id);
        await DrainOutboxAsync();

        var titles = await SearchTitles(_factory.CreateClient(), "Photonmaina");   // transposed typo
        Assert.Contains($"{token}mania Festival", titles);
    }

    [Fact]
    public async Task City_filter_narrows_results()
    {
        var owner = await LoginAsync("9710000004");
        var orgId = await CreateOrgAsync(owner, "Search Org 4");
        var token = "Comet" + Guid.NewGuid().ToString("N")[..6];
        var here = await CreateEventAsync(owner, orgId, $"{token} Local", city: "Hyderabad");
        var elsewhere = await CreateEventAsync(owner, orgId, $"{token} Remote", city: "Chennai");
        await Publish(owner, orgId, here);
        await Publish(owner, orgId, elsewhere);
        await DrainOutboxAsync();

        var items = (await Json(await _factory.CreateClient().GetAsync($"/v1/events?q={token}&city=Hyderabad"))).GetProperty("items");
        var titles = Titles(items);
        Assert.Contains($"{token} Local", titles);
        Assert.DoesNotContain($"{token} Remote", titles);
    }

    /// <summary>D-299. `?categoryId=` matched EVERYTHING: Phase 16 moved discovery onto the search index and
    /// the index had no category column, so the filter was dropped on the floor. It failed open, which is
    /// why nothing caught it — the Category dropdown on both clients, and the category browse page, all
    /// returned the full catalogue under a heading naming one category.</summary>
    [Fact]
    public async Task Category_filter_narrows_results()
    {
        var owner = await LoginAsync("9710000021");
        var orgId = await CreateOrgAsync(owner, "Search Org 21");
        var other = OtherCategoryAsync("search-cat-other");
        var token = "Categ" + Guid.NewGuid().ToString("N")[..6];

        var mine = await CreateEventAsync(owner, orgId, $"{token} Mine");                        // _categoryId
        var theirs = await CreateEventAsync(owner, orgId, $"{token} Theirs", categoryId: other);
        await Publish(owner, orgId, mine);
        await Publish(owner, orgId, theirs);
        await DrainOutboxAsync();

        // Both are discoverable without the filter — otherwise this proves nothing.
        var all = Titles((await Json(await _factory.CreateClient().GetAsync($"/v1/events?q={token}"))).GetProperty("items"));
        Assert.Contains($"{token} Mine", all);
        Assert.Contains($"{token} Theirs", all);

        var filtered = Titles((await Json(await _factory.CreateClient()
            .GetAsync($"/v1/events?q={token}&categoryId={_categoryId}"))).GetProperty("items"));
        Assert.Contains($"{token} Mine", filtered);
        Assert.DoesNotContain($"{token} Theirs", filtered);

        // And the other way, so a filter that simply drops everything cannot pass.
        var flipped = Titles((await Json(await _factory.CreateClient()
            .GetAsync($"/v1/events?q={token}&categoryId={other}"))).GetProperty("items"));
        Assert.Contains($"{token} Theirs", flipped);
        Assert.DoesNotContain($"{token} Mine", flipped);
    }

    // Client integration verification (V3 Phase 16 client-completion pass) found `sort` silently ignored:
    // RankedIdsAsync's one canonical query (M4) never read EventListFilter.Sort, so every `sort` value both
    // Web and Flutter already sent had no effect on ordering. Fixed in SearchService.RankedIdsAsync.
    [Fact]
    public async Task Sort_parameter_controls_result_order()
    {
        var owner = await LoginAsync("9710000014");
        var orgId = await CreateOrgAsync(owner, "Search Org 14");
        var token = "Sortable" + Guid.NewGuid().ToString("N")[..6];
        var soon = await CreateEventAsync(owner, orgId, $"{token} Soon", days: 5);
        var later = await CreateEventAsync(owner, orgId, $"{token} Later", days: 50);
        await Publish(owner, orgId, soon);
        await Publish(owner, orgId, later);
        await DrainOutboxAsync();

        var ascTitles = await SearchTitles(_factory.CreateClient(), token, sort: "date_asc");
        Assert.Equal([$"{token} Soon", $"{token} Later"], ascTitles);

        var descTitles = await SearchTitles(_factory.CreateClient(), token, sort: "date_desc");
        Assert.Equal([$"{token} Later", $"{token} Soon"], descTitles);
    }

    // ── lifecycle: reindex on transition ───────────────────────────────────────────
    [Fact]
    public async Task Unpublishing_removes_the_event_from_discovery()
    {
        var owner = await LoginAsync("9710000005");
        var orgId = await CreateOrgAsync(owner, "Search Org 5");
        var token = "Vortex" + Guid.NewGuid().ToString("N")[..6];
        var id = await CreateEventAsync(owner, orgId, $"{token} Live");
        await Publish(owner, orgId, id);
        await DrainOutboxAsync();
        Assert.Contains($"{token} Live", await SearchTitles(_factory.CreateClient(), token));

        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "unpublish" });
        await DrainOutboxAsync();
        Assert.DoesNotContain($"{token} Live", await SearchTitles(_factory.CreateClient(), token));
    }

    // ── collapse: festival + recurring series ──────────────────────────────────────
    [Fact]
    public async Task Festival_child_is_hidden_from_discovery_unless_standalone()
    {
        var owner = await LoginAsync("9710000006");
        var orgId = await CreateOrgAsync(owner, "Search Org 6");
        var token = "Orion" + Guid.NewGuid().ToString("N")[..6];
        var parent = await CreateEventAsync(owner, orgId, $"{token} Fest");
        var child = await CreateEventAsync(owner, orgId, $"{token} SubEvent", parentEventId: parent);
        await Publish(owner, orgId, parent);
        await Publish(owner, orgId, child);
        await DrainOutboxAsync();

        var titles = await SearchTitles(_factory.CreateClient(), token);
        Assert.Contains($"{token} Fest", titles);            // the fest is one card
        Assert.DoesNotContain($"{token} SubEvent", titles);  // the non-standalone sub-event is collapsed out
    }

    [Fact]
    public async Task Recurring_series_collapses_to_one_listing()
    {
        var owner = await LoginAsync("9710000007");
        var orgId = await CreateOrgAsync(owner, "Search Org 7");
        var token = "Pulsar" + Guid.NewGuid().ToString("N")[..6];
        var first = await CreateEventAsync(owner, orgId, $"{token} Weekly", days: 10);
        var second = await CreateEventAsync(owner, orgId, $"{token} Weekly", days: 17);
        await Publish(owner, orgId, first);
        await Publish(owner, orgId, second);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var series = new EventSeries { OrgId = orgId, Name = $"{token} Series", Slug = $"{token.ToLower()}-series", Mode = SeriesMode.Recurring };
            db.EventSeries.Add(series);
            foreach (var eid in new[] { first, second })
                (await db.Events.FirstAsync(e => e.Id == eid)).SeriesId = series.Id;
            await db.SaveChangesAsync();
        }
        await ProjectAsync(first);
        await ProjectAsync(second);
        await RefreshSignalsAsync();   // marks exactly one occurrence as the series primary

        var titles = await SearchTitles(_factory.CreateClient(), token);
        Assert.Single(titles);   // a RECURRING series is one listing, not two occurrences
    }

    // ── eligibility-aware feed ─────────────────────────────────────────────────────
    [Fact]
    public async Task Eligibility_feed_excludes_a_rule_gated_event_the_user_fails()
    {
        var owner = await LoginAsync("9710000008");
        var orgId = await CreateOrgAsync(owner, "Search Org 8");
        var token = "Zenith" + Guid.NewGuid().ToString("N")[..6];
        var open = await CreateEventAsync(owner, orgId, $"{token} Open");
        var gated = await CreateEventAsync(owner, orgId, $"{token} Gated");
        await Publish(owner, orgId, open);
        await Publish(owner, orgId, gated);
        await DrainOutboxAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.AudienceRules.Add(new AudienceRule { EventId = gated, RoleInJson = "[\"Owner\"]", ExternalOrgsAllowed = false });
            await db.SaveChangesAsync();
        }
        await ProjectAsync(gated);   // refresh HasAudienceRule so the feed evaluates eligibility for it

        var attendee = await LoginAsync("9710000009");   // not a member of the org → fails the Owner-only rule
        var feed = Titles(await Json(await attendee.GetAsync("/v1/events/for-you?limit=50")));
        Assert.Contains($"{token} Open", feed);
        Assert.DoesNotContain($"{token} Gated", feed);
    }

    [Fact]
    public async Task For_you_requires_authentication()
    {
        var res = await _factory.CreateClient().GetAsync("/v1/events/for-you");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── review-fix regressions: reindex triggers fire through the REAL API paths (no manual projection) ──────────────
    [Fact]   // H1
    public async Task Audience_rule_change_via_api_updates_the_eligibility_feed()
    {
        var owner = await LoginAsync("9720000001");
        var orgId = await CreateOrgAsync(owner, "Search Org H1");
        var token = "Astra" + Guid.NewGuid().ToString("N")[..6];
        var open = await CreateEventAsync(owner, orgId, $"{token} Open");
        var gated = await CreateEventAsync(owner, orgId, $"{token} Gated");
        await Publish(owner, orgId, open);
        await Publish(owner, orgId, gated);
        await DrainOutboxAsync();

        var attendee = await LoginAsync("9720000002");   // not a member of the org
        var before = Titles(await Json(await attendee.GetAsync("/v1/events/for-you?limit=50")));
        Assert.Contains($"{token} Gated", before);        // no rule yet → visible

        // D-388 — this suite's subject is that a mutation FLOWS INTO THE INDEX through the outbox, so the
        // mutation has to genuinely execute: seeding rows would test nothing. Every fixture here publishes
        // first because discovery only surfaces live events, so the writes below run with the status
        // momentarily suspended and the real endpoint — and the reindex it enqueues — still fire.
        // Add an Owner-only rule via the API (no manual reindex) — the outbox must carry the change.
        await _factory.WithLiveStatusSuspendedAsync(gated, () =>
            owner.PutAsJsonAsync($"/v1/orgs/{orgId}/events/{gated}/audience", new { roleIn = new[] { "Owner" }, externalOrgsAllowed = false }));
        await DrainOutboxAsync();
        var after = Titles(await Json(await attendee.GetAsync("/v1/events/for-you?limit=50")));
        Assert.Contains($"{token} Open", after);
        Assert.DoesNotContain($"{token} Gated", after);   // the API rule change flowed into the index

        // Remove the rule via the API → it becomes open again.
        await _factory.WithLiveStatusSuspendedAsync(gated, () =>
            owner.DeleteAsync($"/v1/orgs/{orgId}/events/{gated}/audience"));
        await DrainOutboxAsync();
        Assert.Contains($"{token} Gated", Titles(await Json(await attendee.GetAsync("/v1/events/for-you?limit=50"))));
    }

    [Fact]   // M1
    public async Task Ticket_type_creation_via_api_updates_the_paid_filter()
    {
        var owner = await LoginAsync("9720000003");
        var orgId = await CreateOrgAsync(owner, "Search Org M1");
        var token = "Lyra" + Guid.NewGuid().ToString("N")[..6];
        var id = await CreateEventAsync(owner, orgId, $"{token} Show");
        await Publish(owner, orgId, id);
        await DrainOutboxAsync();
        var pub = _factory.CreateClient();
        Assert.Contains(id, (await Json(await pub.GetAsync($"/v1/events?price=free&orgId={orgId}"))).GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()));

        // Add a priced ticket type through the API (no manual reindex).
        var res = await _factory.WithLiveStatusSuspendedAsync(id, () =>
            owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/ticket-types", new
        {
            name = "VIP", pricePaise = 50000L, pricingUnit = "PerTicket", registrationMode = "Individual",
            groupMin = (int?)null, groupMax = (int?)null, quantity = 10,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 5, isAllAccess = false,
        }));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        await DrainOutboxAsync();
        Assert.Contains(id, (await Json(await pub.GetAsync($"/v1/events?price=paid&orgId={orgId}"))).GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()));
    }

    [Fact]   // M2
    public async Task Series_attach_via_api_collapses_recurring_occurrences()
    {
        var owner = await LoginAsync("9720000004");
        var orgId = await CreateOrgAsync(owner, "Search Org M2");
        var token = "Vega" + Guid.NewGuid().ToString("N")[..6];
        var first = await CreateEventAsync(owner, orgId, $"{token} Weekly", days: 10);
        var second = await CreateEventAsync(owner, orgId, $"{token} Weekly", days: 17);
        await Publish(owner, orgId, first);
        await Publish(owner, orgId, second);
        await DrainOutboxAsync();
        Assert.Equal(2, (await SearchTitles(_factory.CreateClient(), token)).Count);   // two standalone until series-attached

        var seriesId = (await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/series", new { name = $"{token} Series", mode = "Recurring" }))).GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync($"/v1/series/{seriesId}/events", new { eventId = first });
        await owner.PostAsJsonAsync($"/v1/series/{seriesId}/events", new { eventId = second });
        await DrainOutboxAsync();
        await RefreshSignalsAsync();

        Assert.Single(await SearchTitles(_factory.CreateClient(), token));   // one listing after the API attach
    }

    [Fact]   // M4 — proximity participates in the no-query browse path
    public async Task No_query_location_ranks_nearer_events_first()
    {
        var owner = await LoginAsync("9720000005");
        var orgId = await CreateOrgAsync(owner, "Search Org M4");
        var token = "Rigel" + Guid.NewGuid().ToString("N")[..6];
        var near = await CreateEventAsync(owner, orgId, $"{token} Near");
        var far = await CreateEventAsync(owner, orgId, $"{token} Far");
        await Publish(owner, orgId, near);
        await Publish(owner, orgId, far);
        await SetCoordsAndReindexAsync(near, 17.7, 83.3);   // Visakhapatnam
        await SetCoordsAndReindexAsync(far, 28.6, 77.2);    // Delhi (far from the query point)

        // No q, just a location near the "Near" event — proximity must order it first.
        var items = (await Json(await _factory.CreateClient().GetAsync($"/v1/events?orgId={orgId}&lat=17.7&lng=83.3"))).GetProperty("items");
        var order = items.EnumerateArray().Select(e => e.GetProperty("title").GetString()).ToList();
        Assert.True(order.IndexOf($"{token} Near") < order.IndexOf($"{token} Far"), $"expected Near before Far, got: {string.Join(", ", order)}");
    }

    [Fact]   // outbox hygiene — exactly one reindex per mutation, none lost
    public async Task A_single_mutation_enqueues_exactly_one_reindex()
    {
        var owner = await LoginAsync("9720000006");
        var orgId = await CreateOrgAsync(owner, "Search Org OB");
        var id = await CreateEventAsync(owner, orgId, "Outbox " + Guid.NewGuid().ToString("N")[..6]);
        await Publish(owner, orgId, id);
        await DrainOutboxAsync();   // clear the create + publish messages

        await _factory.WithLiveStatusSuspendedAsync(id, () =>
            owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}", new { description = "an updated description" }));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // PayloadJson is jsonb (can't LIKE in SQL) — load the pending reindex messages and match this event in memory.
        var pendingMsgs = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == "search.reindex" && m.Status == OutboxStatus.Pending).ToListAsync();
        var pending = pendingMsgs.Count(m => m.PayloadJson.Contains(id.ToString()));
        Assert.Equal(1, pending);   // exactly one, not zero (lost) and not many (duplicate)
    }

    [Fact]   // H2 — the FTS query is index-eligible (with seq scan disabled the planner must use the GIN indexes)
    public async Task Search_query_uses_the_gin_indexes()
    {
        var owner = await LoginAsync("9720000007");
        var orgId = await CreateOrgAsync(owner, "Search Org H2");
        var id = await CreateEventAsync(owner, orgId, "Indexable Event " + Guid.NewGuid().ToString("N")[..6]);
        await Publish(owner, orgId, id);
        await DrainOutboxAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        using (var set = conn.CreateCommand()) { set.Transaction = tx; set.CommandText = "SET LOCAL enable_seqscan = off"; await set.ExecuteNonQueryAsync(); }
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"EXPLAIN SELECT ""EventId"" FROM event_search_documents d
            WHERE d.search_vector @@ websearch_to_tsquery('english', 'hackathon')
               OR d.""FuzzyText"" % 'hackathon' OR 'hackathon' <% d.""FuzzyText""";
        var plan = new StringBuilder();
        using (var r = await cmd.ExecuteReaderAsync()) while (await r.ReadAsync()) plan.AppendLine(r.GetString(0));
        await tx.RollbackAsync();
        await conn.CloseAsync();

        // With seq scan disabled, an index-eligible query is planned as (bitmap) index scans; the old functional
        // word_similarity predicate would have forced a Seq Scan even here.
        Assert.Contains("Index", plan.ToString());
    }

    [Fact]   // self-review: speaker attach is a direct event mutation of the indexed document
    public async Task Speaker_attached_via_api_becomes_searchable()
    {
        var owner = await LoginAsync("9730000001");
        var orgId = await CreateOrgAsync(owner, "Search Org SP");
        var token = "Maxwell" + Guid.NewGuid().ToString("N")[..6];   // distinctive speaker surname
        var id = await CreateEventAsync(owner, orgId, "Speaker Talk " + Guid.NewGuid().ToString("N")[..6]);
        await Publish(owner, orgId, id);
        await DrainOutboxAsync();
        Assert.Empty(await SearchTitles(_factory.CreateClient(), token));   // speaker not attached yet → not found by name

        Guid speakerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var sp = new Speaker { OrgId = orgId, Name = $"Dr {token}" };
            db.Speakers.Add(sp);
            await db.SaveChangesAsync();
            speakerId = sp.Id;
        }
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/speakers", new { speakerId, sessionId = (Guid?)null });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        await DrainOutboxAsync();

        // The speaker's name is now in the indexed document (via the real attach endpoint, no manual reindex).
        var titles = (await Json(await _factory.CreateClient().GetAsync($"/v1/events?q={token}"))).GetProperty("items")
            .EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(id, titles);
    }

    [Fact]   // self-review: org rename is a referenced-entity name change indexed in every event's document
    public async Task Org_rename_via_api_updates_the_indexed_org_name()
    {
        var owner = await LoginAsync("9730000002");
        var orgId = await CreateOrgAsync(owner, "Original Org Name");
        var newName = "Quantico" + Guid.NewGuid().ToString("N")[..6];   // distinctive new name token
        var id = await CreateEventAsync(owner, orgId, "Org Rename Event " + Guid.NewGuid().ToString("N")[..6]);
        await Publish(owner, orgId, id);
        await DrainOutboxAsync();
        Assert.Empty(await SearchTitles(_factory.CreateClient(), newName));   // new name not yet in any document

        await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}", new { name = newName });
        await DrainOutboxAsync();

        var found = (await Json(await _factory.CreateClient().GetAsync($"/v1/events?q={newName}"))).GetProperty("items")
            .EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(id, found);   // the org's published event is searchable by the new org name
    }

    private async Task SetCoordsAndReindexAsync(Guid eventId, double lat, double lng)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var e = await db.Events.FirstAsync(x => x.Id == eventId);
        e.Lat = lat; e.Lng = lng;
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<ISearchIndexService>().ProjectAsync(eventId);
    }
}
