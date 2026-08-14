using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>V3 §3 (Structure) + §13.2 (Series), Phase 12. Composition depth ≤ 3; structural discovery (roots/editions
/// discoverable, sub-events opt-in); EventSeries (RECURRING/EDITIONS, rrule validation, one-series rule, followers);
/// AgendaItem (EventSession) may hold an InventoryPool. Additive — the approved money path and subsystems are untouched.</summary>
public class SeriesStructureTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public SeriesStructureTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private ISeriesService Series(IServiceScope s) => s.ServiceProvider.GetRequiredService<ISeriesService>();

    // ── Composition depth (§3.4 rule 1) ──────────────────────────────────────

    [Fact]
    public async Task Composition_depth_is_capped_at_3()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700012001", "Depth Org");
        var root = await CreateEventAsync(owner, orgId);                 // depth 1
        var sub = await CreateEventAsync(owner, orgId, parent: root);    // depth 2
        var subsub = await CreateEventAsync(owner, orgId, parent: sub);  // depth 3
        Assert.NotEqual(Guid.Empty, subsub);

        // A 4th composition level is a modelling error.
        var res = await CreateEventRawAsync(owner, orgId, parent: subsub);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("max_composition_depth", (await Json(res)).GetProperty("error").GetString());
    }

    // ── Structural discovery (§3.4 rule 2) ───────────────────────────────────

    [Fact]
    public async Task Sub_event_is_hidden_from_discovery_until_opted_in()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700012002", "Discovery Org");
        var root = await CreateEventAsync(owner, orgId);
        var sub = await CreateEventAsync(owner, orgId, parent: root);
        await PublishDirectAsync(root, sub);   // both Published + Public
        await DrainOutboxAsync();              // V3 §15 (Phase 16): project into the discovery index before listing

        using var scope = _factory.Services.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<IEventService>();
        var listed = (await events.UpcomingAsync(100)).Select(e => e.Id).ToHashSet();
        Assert.Contains(root, listed);         // a root is discoverable
        Assert.DoesNotContain(sub, listed);    // a sub-event is not, by default

        await SetListedStandaloneAsync(sub, true);   // organiser opts the sub-event into standalone discovery
        var after = (await events.UpcomingAsync(100)).Select(e => e.Id).ToHashSet();
        Assert.Contains(sub, after);
    }

    // ── EventSeries (§13.2) ──────────────────────────────────────────────────

    [Fact]
    public async Task Series_create_requires_manage_and_validates_mode_and_rrule()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700012010", "Series Org");
        var (_, strangerId) = await LoginAsync("9700012011");

        using var scope = _factory.Services.CreateScope();
        var svc = Series(scope);
        Assert.Equal("forbidden", (await svc.CreateAsync(strangerId, orgId, false, new SeriesInput("X", "Editions", null, null, null, null, null))).Error);
        Assert.Equal("invalid_mode", (await svc.CreateAsync(ownerId, orgId, false, new SeriesInput("X", "Nonsense", null, null, null, null, null))).Error);
        Assert.Equal("invalid_rrule", (await svc.CreateAsync(ownerId, orgId, false, new SeriesInput("Weekly", "Recurring", null, null, null, "FREQ=NEVER", null))).Error);

        var ok = await svc.CreateAsync(ownerId, orgId, false, new SeriesInput("Weekly Meetup", "Recurring", "d", null, null, "FREQ=WEEKLY;BYDAY=MO", ["2026-08-10"]));
        Assert.True(ok.Ok);
        Assert.Equal("Recurring", ok.Value!.Mode);
        Assert.Equal("weekly-meetup", ok.Value!.Slug);
        Assert.Equal("FREQ=WEEKLY;BYDAY=MO", ok.Value!.Rrule);
    }

    [Fact]
    public async Task An_event_belongs_to_at_most_one_series()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700012012", "Edition Org");
        var ev = await CreateEventAsync(owner, orgId);

        using var scope = _factory.Services.CreateScope();
        var svc = Series(scope);
        var s1 = (await svc.CreateAsync(ownerId, orgId, false, new SeriesInput("Fest S1", "Editions", null, null, null, null, null))).Value!;
        var s2 = (await svc.CreateAsync(ownerId, orgId, false, new SeriesInput("Fest S2", "Editions", null, null, null, null, null))).Value!;

        var attach = await svc.AttachEventAsync(ownerId, s1.Id, false, new SeriesMemberInput(ev, 2026, "2026 Edition"));
        Assert.True(attach.Ok);
        Assert.Equal("2026 Edition", attach.Value!.EditionLabel);
        Assert.Equal("already_in_series", (await svc.AttachEventAsync(ownerId, s2.Id, false, new SeriesMemberInput(ev, 1, null))).Error);   // §3.4 rule 5

        Assert.True((await svc.DetachEventAsync(ownerId, s1.Id, ev, false)).Ok);
        Assert.True((await svc.AttachEventAsync(ownerId, s2.Id, false, new SeriesMemberInput(ev, 1, "First"))).Ok);   // now free to re-attach
        Assert.Single(await svc.ListMembersAsync(s2.Id, ownerId, false));
    }

    [Fact]
    public async Task Follow_and_unfollow_a_series_is_idempotent()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700012013", "Follow Org");
        var (_, followerId) = await LoginAsync("9700012014");

        using var scope = _factory.Services.CreateScope();
        var svc = Series(scope);
        var s = (await svc.CreateAsync(ownerId, orgId, false, new SeriesInput("Followable", "Recurring", null, null, null, null, null))).Value!;
        Assert.True((await svc.FollowAsync(followerId, s.Id)).Ok);
        Assert.True((await svc.FollowAsync(followerId, s.Id)).Ok);   // idempotent
        Assert.Equal(1, (await svc.GetAsync(s.Id)).Value!.FollowerCount);
        Assert.True((await svc.UnfollowAsync(followerId, s.Id)).Ok);
        Assert.Equal(0, (await svc.GetAsync(s.Id)).Value!.FollowerCount);
    }

    [Fact]   // H1 (review fix) — the public series member list must never disclose non-public editions to anonymous callers.
    public async Task Series_member_list_hides_non_public_editions_from_anonymous_callers()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700012030", "Leak Org");
        var (_, strangerId) = await LoginAsync("9700012031");
        var pubPublic = await CreateEventAsync(owner, orgId);
        var draft = await CreateEventAsync(owner, orgId);
        var privateEd = await CreateEventAsync(owner, orgId);
        var unlisted = await CreateEventAsync(owner, orgId);
        await SetStatusVisibilityAsync(pubPublic, EventStatus.Published, EventVisibility.Listed);
        await SetStatusVisibilityAsync(draft, EventStatus.Draft, EventVisibility.Listed);
        await SetStatusVisibilityAsync(privateEd, EventStatus.Published, EventVisibility.InviteOnly);
        await SetStatusVisibilityAsync(unlisted, EventStatus.Published, EventVisibility.Unlisted);

        using var scope = _factory.Services.CreateScope();
        var svc = Series(scope);
        var s = (await svc.CreateAsync(ownerId, orgId, false, new SeriesInput("Brand", "Editions", null, null, null, null, null))).Value!;
        var ord = 1;
        foreach (var e in new[] { pubPublic, draft, privateEd, unlisted })
            Assert.True((await svc.AttachEventAsync(ownerId, s.Id, false, new SeriesMemberInput(e, ord++, null))).Ok);

        // Anonymous (and any non-manager) caller sees ONLY the published+public edition.
        var anon = (await svc.ListMembersAsync(s.Id, null, false)).Select(m => m.EventId).ToHashSet();
        Assert.Single(anon);
        Assert.Contains(pubPublic, anon);
        Assert.DoesNotContain(draft, anon);       // Draft not disclosed
        Assert.DoesNotContain(privateEd, anon);   // Private not disclosed
        Assert.DoesNotContain(unlisted, anon);    // Unlisted not disclosed

        var stranger = (await svc.ListMembersAsync(s.Id, strangerId, false)).Select(m => m.EventId).ToHashSet();
        Assert.Single(stranger);                  // an authenticated non-member is gated identically
        Assert.Contains(pubPublic, stranger);

        // The org owner (and an admin) still sees every member they are permitted to manage.
        var asOwner = (await svc.ListMembersAsync(s.Id, ownerId, false)).Select(m => m.EventId).ToHashSet();
        Assert.Equal(4, asOwner.Count);
        foreach (var e in new[] { pubPublic, draft, privateEd, unlisted }) Assert.Contains(e, asOwner);
        Assert.Equal(4, (await svc.ListMembersAsync(s.Id, null, true)).Count);   // admin sees all
    }

    // ── AgendaItem may hold a pool (§3.4 rule 3) ──────────────────────────────

    [Fact]
    public async Task Agenda_item_may_hold_an_inventory_pool()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700012020", "Agenda Org");
        var (eventId, _) = await PublishFreeEventWithPoolAsync(owner, orgId);
        Guid poolId;
        using (var scope = _factory.Services.CreateScope())
            poolId = await scope.ServiceProvider.GetRequiredService<KurxDbContext>().InventoryPools.Where(p => p.EventId == eventId).Select(p => p.Id).FirstAsync();

        using var s2 = _factory.Services.CreateScope();
        var sched = s2.ServiceProvider.GetRequiredService<IScheduleService>();
        var bad = await sched.CreateAsync(ownerId, eventId, false, new SessionInput("Keynote", null, "Session", DateTime.UtcNow.AddDays(21), DateTime.UtcNow.AddDays(21).AddHours(1), null, Guid.NewGuid()));
        Assert.Equal("invalid_pool", bad.Error);
        var ok = await sched.CreateAsync(ownerId, eventId, false, new SessionInput("Workshop", null, "Session", DateTime.UtcNow.AddDays(21), DateTime.UtcNow.AddDays(21).AddHours(2), null, poolId));
        Assert.True(ok.Ok);
        Assert.Equal(poolId, ok.Value!.InventoryPoolId);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> CreateEventRawAsync(HttpClient owner, Guid orgId, Guid? parent = null)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        return await owner.CreateEventAsync(orgId, new
        {
            title = "Ev " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId, parentEventId = parent,
            venueName = "V", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        });
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId, Guid? parent = null)
    {
        var res = await CreateEventRawAsync(owner, orgId, parent);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishFreeEventWithPoolAsync(HttpClient owner, Guid orgId)
    {
        var eventId = await CreateEventAsync(owner, orgId);
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "Free", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode = "Individual",
            quantity = 100, saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 50, isAllAccess = false,
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var ttId = (await Json(res)).GetProperty("id").GetGuid();
        return (eventId, ttId);
    }

    private async Task PublishDirectAsync(params Guid[] eventIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        foreach (var e in await db.Events.Where(x => eventIds.Contains(x.Id)).ToListAsync())
        {
            e.Status = EventStatus.Published;
            e.Visibility = EventVisibility.Listed;
        }
        await db.SaveChangesAsync();
    }

    private async Task SetListedStandaloneAsync(Guid eventId, bool value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var e = await db.Events.FirstAsync(x => x.Id == eventId);
        e.ListedStandalone = value;
        await db.SaveChangesAsync();
        // Direct DB write enqueues no reindex message — project the discovery index explicitly (§15, Phase 16).
        await scope.ServiceProvider.GetRequiredService<ISearchIndexService>().ProjectAsync(eventId);
    }

    private async Task DrainOutboxAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Kurx.Infrastructure.Jobs.OutboxDispatchJob>().RunAsync();
    }

    private async Task SetStatusVisibilityAsync(Guid eventId, EventStatus status, EventVisibility visibility)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var e = await db.Events.FirstAsync(x => x.Id == eventId);
        e.Status = status;
        e.Visibility = visibility;
        await db.SaveChangesAsync();
    }

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<(HttpClient Client, Guid OrgId, Guid UserId)> LoginOrgAsync(string phone, string name)
    {
        var (client, userId) = await LoginAsync(phone);
        return (client, _factory.SeedVerifiedOrgForClient(client, name), userId);
    }

    private async Task<(Guid CatId, Guid TypeId)> TaxonAsync(string typeSlug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == typeSlug).Select(c => new { c.Id, c.ParentId }).SingleAsync();
        return (t.ParentId!.Value, t.Id);
    }
}
