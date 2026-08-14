using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-266 M7 — the reviewer's checklist and the approve gate it controls.
///
/// <para>The milestone's exit criterion is one sentence: <b>approve is blocked until the checklist is
/// complete.</b> The rest of these cases exist because the checklist is <i>derived</i> rather than stored,
/// which is the design decision most likely to be undone by a later "optimisation".</para></summary>
public class ReviewChecklistTests(KurxApiFactory factory) : IClassFixture<KurxApiFactory>
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
            title = "Chk " + Guid.NewGuid().ToString("N")[..6], description = "a real description",
            categoryId = t.ParentId!.Value, typeId = t.Id, venueName = "Hall", city = "C",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(3),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> Act(HttpClient c, Guid orgId, Guid eventId, string action,
        string? reasonCode = null, string? notes = null)
        => c.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action, reasonCode, notes });

    private static async Task<JsonElement> ChecklistAsync(HttpClient reviewer, Guid eventId)
        => await Json(await reviewer.GetAsync($"/v1/admin/events/{eventId}/review-checklist"));

    private static Task<HttpResponseMessage> Tick(HttpClient reviewer, Guid eventId, string key, bool @checked = true)
        => reviewer.PutAsJsonAsync($"/v1/admin/events/{eventId}/review-checklist",
            new { itemKey = key, @checked });

    /// <summary>Walks an event to UnderReview with a reviewer holding it.</summary>
    private async Task<(HttpClient Owner, HttpClient Reviewer, Guid OrgId, Guid EventId)> UnderReviewAsync(string phone, string org)
    {
        var owner = await LoginAsync(phone);
        var orgId = _factory.SeedVerifiedOrgForClient(owner, org);
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_for_review");
        Assert.Equal(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "claim_review")).StatusCode);
        return (owner, reviewer, orgId, id);
    }

    // ── The milestone's exit criterion ───────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_is_blocked_until_every_checklist_item_is_ticked()
    {
        var (_, reviewer, orgId, id) = await UnderReviewAsync("9705000001", "Checklist College");

        var checklist = await ChecklistAsync(reviewer, id);
        var keys = checklist.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("key").GetString()!).ToList();
        // Guard the premise: a checklist with no items would make this test pass vacuously.
        Assert.NotEmpty(keys);
        Assert.False(checklist.GetProperty("is_complete").GetBoolean());

        var blocked = await Act(reviewer, orgId, id, "approve_review");
        Assert.NotEqual(HttpStatusCode.OK, blocked.StatusCode);
        Assert.Contains("checklist_incomplete", await blocked.Content.ReadAsStringAsync());

        foreach (var k in keys) Assert.Equal(HttpStatusCode.OK, (await Tick(reviewer, id, k)).StatusCode);
        Assert.True((await ChecklistAsync(reviewer, id)).GetProperty("is_complete").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "approve_review")).StatusCode);
    }

    /// <summary>Unticking must re-block. Otherwise "complete" would be a latch rather than a statement about
    /// the current state, and a reviewer who changed their mind could not withdraw the claim.</summary>
    [Fact]
    public async Task Unticking_an_item_blocks_approve_again()
    {
        var (_, reviewer, orgId, id) = await UnderReviewAsync("9705000002", "Untick College");

        var keys = (await ChecklistAsync(reviewer, id)).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("key").GetString()!).ToList();
        foreach (var k in keys) await Tick(reviewer, id, k);
        await Tick(reviewer, id, keys[0], @checked: false);

        var res = await Act(reviewer, orgId, id, "approve_review");
        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("checklist_incomplete", await res.Content.ReadAsStringAsync());

        // The timestamp goes with the tick: a CheckedAt surviving an untick would read as a confirmation
        // nobody currently stands behind.
        var item = (await ChecklistAsync(reviewer, id)).GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("key").GetString() == keys[0]);
        Assert.False(item.GetProperty("checked").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("checked_at").ValueKind);
    }

    /// <summary>A tick against an item not on this event's checklist would count towards completeness
    /// without the reviewer ever having read a real requirement — the gate bypassed, not satisfied.</summary>
    [Fact]
    public async Task An_item_that_is_not_on_the_checklist_is_refused()
    {
        var (_, reviewer, _, id) = await UnderReviewAsync("9705000003", "Unknown Item College");

        var res = await Tick(reviewer, id, "not_a_real_requirement");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("unknown_checklist_item", await res.Content.ReadAsStringAsync());
    }

    /// <summary>The checklist is a PROJECTION of the policy engine, not a stored snapshot. An event edited
    /// into needing a new rule must show it — otherwise a reviewer ticks a complete-looking list while the
    /// publish still refuses, which is the drift M3 exists to prevent.</summary>
    [Fact]
    public async Task The_checklist_tracks_the_policy_engine_rather_than_a_stored_snapshot()
    {
        var owner = await LoginAsync("9705000004");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Drift College");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        var before = (await ChecklistAsync(reviewer, id)).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("key").GetString()!).ToList();
        Assert.Contains("event_authorization_required", before);

        // Approving the authorization removes that requirement — the checklist must follow.
        _factory.SeedApprovedEventAuthorization(id);

        var after = (await ChecklistAsync(reviewer, id)).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("key").GetString()!).ToList();
        Assert.DoesNotContain("event_authorization_required", after);
    }

    /// <summary>Ticks are per reviewer. Releasing and reclaiming must not let one reviewer inherit another's
    /// sign-off — the whole point of a checklist is that the person approving did the reading.</summary>
    [Fact]
    public async Task One_reviewers_ticks_do_not_complete_anothers_checklist()
    {
        var (_, reviewer, orgId, id) = await UnderReviewAsync("9705000005", "Two Reviewer College");

        var keys = (await ChecklistAsync(reviewer, id)).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("key").GetString()!).ToList();
        foreach (var k in keys) await Tick(reviewer, id, k);
        Assert.True((await ChecklistAsync(reviewer, id)).GetProperty("is_complete").GetBoolean());

        // A second platform reviewer, same event.
        var other = await LoginAsync("9705000006");
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<Kurx.Application.Abstractions.IPlatformRoleService>()
                .GrantAsync(UserIdOf(other), PlatformRole.VerificationReviewer, grantedBy: null);

        Assert.False((await ChecklistAsync(other, id)).GetProperty("is_complete").GetBoolean());

        // While the first reviewer still holds it, the claim refuses the second one outright (D-266 M4) —
        // an earlier and stricter gate than the checklist.
        Assert.Contains("claimed_by_another_reviewer",
            await (await Act(other, orgId, id, "approve_review")).Content.ReadAsStringAsync());

        // Release and reclaim — the case this test exists for. With the item legitimately theirs, the
        // second reviewer is still refused, now because the ticks were never theirs to inherit.
        Assert.Equal(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "release_review")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Act(other, orgId, id, "claim_review")).StatusCode);
        Assert.False((await ChecklistAsync(other, id)).GetProperty("is_complete").GetBoolean());
        Assert.Contains("checklist_incomplete",
            await (await Act(other, orgId, id, "approve_review")).Content.ReadAsStringAsync());
    }

    /// <summary>Reviewer-gated, like every other admin review surface. An organiser must not be able to read
    /// or tick the list that decides their own event.</summary>
    [Fact]
    public async Task An_organiser_cannot_read_or_tick_the_checklist()
    {
        var (owner, _, _, id) = await UnderReviewAsync("9705000007", "Organiser Block College");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await owner.GetAsync($"/v1/admin/events/{id}/review-checklist")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Tick(owner, id, "anything")).StatusCode);
    }

    /// <summary>D-266 §5: `meetingPassword` is confidential everywhere. The pending payload has no such
    /// field by construction, so this asserts the property rather than one call site's filtering.</summary>
    [Fact]
    public async Task The_pending_queue_is_enriched_and_never_carries_the_meeting_password()
    {
        var owner = await LoginAsync("9705000008");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Enriched College");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}",
            new { location = new { eventMode = "Online", onlineUrl = "https://meet.example/x", meetingPassword = "s3cret-pw" } });
        await Act(owner, orgId, id, "submit_for_review");

        var raw = await (await reviewer.GetAsync("/v1/admin/events/pending")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("s3cret-pw", raw);
        Assert.DoesNotContain("meeting_password", raw);

        var row = JsonDocument.Parse(raw).RootElement.EnumerateArray()
            .First(e => e.GetProperty("event_id").GetGuid() == id);
        Assert.Equal("PendingReview", row.GetProperty("status").GetString());
        Assert.Equal("competitive", row.GetProperty("archetype_slug").GetString());
        Assert.Equal("Public", row.GetProperty("product").GetString());
    }

    private static Guid UserIdOf(HttpClient c)
    {
        var jwt = c.DefaultRequestHeaders.Authorization!.Parameter!;
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }
}
