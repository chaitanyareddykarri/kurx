using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-362 — an event becomes public when it is PUBLISHED, and by no other route.
///
/// <para><b>Why this file exists.</b> `EventExposureTests` pins the exposure predicate as a pure unit, and
/// `EventTests` checks one draft-by-slug case. Neither walked the review lifecycle over HTTP, and the gap
/// hid a real defect: the reviewer gate on `publish` sat inside `if (isPaid)`, so an organiser could submit
/// a FREE public event for review and immediately publish it themselves — live, with no approval, out of
/// the reviewer's queue.</para>
///
/// <para>Every case here is an ANONYMOUS request against the real API, because the question is what an
/// unauthenticated stranger can reach. Asserting on a service predicate would have passed throughout the
/// window in which the bug shipped.</para></summary>
public class LifecycleVisibilityTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public LifecycleVisibilityTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Lifecycle", Slug = "lifecycle-vis" };
            db.EventCategories.Add(cat);
            db.SaveChanges();
            _categoryId = cat.Id;
            _reset = true;
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var c = _factory.CreateClient();
        await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var t = await Json(await c.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return (c, t.GetProperty("user_id").GetGuid());
    }

    private async Task<HttpClient> ReviewerAsync(string phone)
    {
        var (client, userId) = await LoginAsync(phone);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.VerificationReviewer, grantedBy: null);
        return client;
    }

    /// <summary>A free, Public, Listed event owned by the caller — the shape that must not leak.</summary>
    private async Task<(Guid OrgId, Guid EventId, string Slug)> DraftAsync(HttpClient owner, string title)
    {
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Vis Org {Guid.NewGuid():N}"[..20], "Company");
        var ev = await Json(await owner.CreateEventAsync(orgId, new
        {
            title, description = "An event whose visibility is under test.",
            categoryId = _categoryId, venueName = "Hall", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(4),
            visibility = "Listed",
        }));
        _factory.SeedApprovedEventAuthorization(ev.GetProperty("id").GetGuid());
        return (orgId, ev.GetProperty("id").GetGuid(), ev.GetProperty("slug").GetString()!);
    }

    /// <summary>Discovery is an outbox-fed index (V3 §15) — drain it, or a "not found" is just latency.</summary>
    private async Task DrainAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutboxDispatchJob>().RunAsync();
    }

    private async Task AssertNotPublicAsync(Guid eventId, string slug, string status)
    {
        await DrainAsync();
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/v1/events/{slug}")).StatusCode);

        // Every anonymous discovery surface, not just search: each is its own query and any one of them
        // could carry a different filter.
        foreach (var feed in new[] { "/v1/events?q=Visibility", "/v1/events", "/v1/events/upcoming",
                                     "/v1/events/trending", "/v1/events/featured", "/v1/events/latest" })
        {
            var body = await (await anon.GetAsync(feed)).Content.ReadAsStringAsync();
            Assert.False(body.Contains(slug, StringComparison.Ordinal),
                $"a {status} event appeared in {feed}");
            Assert.False(body.Contains(eventId.ToString(), StringComparison.Ordinal),
                $"a {status} event appeared in {feed}");
        }
    }

    // ── Every pre-publication state is invisible to a stranger ───────────────────────────────────

    [Fact]
    public async Task A_draft_is_not_public()
    {
        var (owner, _) = await LoginAsync("9940000001");
        var (_, id, slug) = await DraftAsync(owner, "Visibility Draft");
        await AssertNotPublicAsync(id, slug, "Draft");
    }

    [Fact]
    public async Task A_pending_review_event_is_not_public()
    {
        var (owner, _) = await LoginAsync("9940000002");
        var (orgId, id, slug) = await DraftAsync(owner, "Visibility Pending");
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" })).StatusCode);
        await AssertNotPublicAsync(id, slug, "PendingReview");
    }

    [Fact]
    public async Task An_under_review_event_is_not_public()
    {
        var reviewer = await ReviewerAsync("9940000010");
        var (owner, _) = await LoginAsync("9940000003");
        var (orgId, id, slug) = await DraftAsync(owner, "Visibility UnderReview");
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "claim_review" });
        await AssertNotPublicAsync(id, slug, "UnderReview");
    }

    [Fact]
    public async Task A_rejected_event_is_not_public()
    {
        var reviewer = await ReviewerAsync("9940000011");
        var (owner, _) = await LoginAsync("9940000004");
        var (orgId, id, slug) = await DraftAsync(owner, "Visibility Rejected");
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "claim_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition",
            new { action = "reject_review", reasonCode = nameof(EventReviewReason.Incomplete), notes = "no" });
        await AssertNotPublicAsync(id, slug, "Rejected");
    }

    [Fact]
    public async Task A_changes_requested_event_is_not_public()
    {
        var reviewer = await ReviewerAsync("9940000012");
        var (owner, _) = await LoginAsync("9940000005");
        var (orgId, id, slug) = await DraftAsync(owner, "Visibility Changes");
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "claim_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition",
            new { action = "request_changes", reasonCode = nameof(EventReviewReason.Incomplete), notes = "fix" });
        await AssertNotPublicAsync(id, slug, "ChangesRequested");
    }

    /// <summary>Approval is NOT publication. An approved event waits for its organiser to publish it, and
    /// is invisible until then — the distinction this lifecycle deliberately keeps.</summary>
    [Fact]
    public async Task An_approved_event_is_not_public_until_it_is_published()
    {
        var reviewer = await ReviewerAsync("9940000013");
        var (owner, _) = await LoginAsync("9940000006");
        var (orgId, id, slug) = await DraftAsync(owner, "Visibility Approved");
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "claim_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "approve_review" });

        await AssertNotPublicAsync(id, slug, "Approved");

        // …and publishing it makes it public, so the assertion above is not passing vacuously.
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish" })).StatusCode);
        await DrainAsync();
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/v1/events/{slug}")).StatusCode);
        var feed = await (await anon.GetAsync("/v1/events?q=Visibility")).Content.ReadAsStringAsync();
        Assert.Contains(slug, feed, StringComparison.Ordinal);
    }

    // ── The defect itself ────────────────────────────────────────────────────────────────────────

    /// <summary><b>The bug.</b> The reviewer gate on `publish` sat inside `if (isPaid)`, so an organiser
    /// could submit a FREE event for review and then publish it themselves — bypassing approval entirely
    /// and taking it out of the reviewer's queue while someone may have been holding it.</summary>
    [Fact]
    public async Task An_organiser_cannot_publish_their_own_event_out_of_review()
    {
        var (owner, _) = await LoginAsync("9940000007");
        var (orgId, id, slug) = await DraftAsync(owner, "Visibility Bypass");
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("reviewer_required", (await Json(res)).GetProperty("error").GetString());

        await AssertNotPublicAsync(id, slug, "PendingReview after a refused self-publish");
    }

    /// <summary>The same refusal once a reviewer is holding the item — publishing it out from under them
    /// is the other half of the same bypass.</summary>
    [Fact]
    public async Task An_organiser_cannot_publish_an_event_a_reviewer_is_holding()
    {
        var reviewer = await ReviewerAsync("9940000014");
        var (owner, _) = await LoginAsync("9940000008");
        var (orgId, id, _) = await DraftAsync(owner, "Visibility Claimed");
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "claim_review" });

        // Refused one step EARLIER than the reviewer gate, by the claim guard (D-266 M4): the item
        // belongs to the reviewer holding it. A different code for a different reason — both refuse, and
        // asserting the specific one is what would catch the guard silently disappearing.
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish" });
        Assert.False(res.IsSuccessStatusCode);
        Assert.Equal("claimed_by_another_reviewer", (await Json(res)).GetProperty("error").GetString());
    }

    /// <summary>The legitimate self-publish path is untouched: a free event that never entered review is
    /// exactly what D-047 permits an organiser to publish. Narrowing that would have been a different bug.</summary>
    [Fact]
    public async Task An_organiser_may_still_publish_a_free_event_straight_from_draft()
    {
        var (owner, _) = await LoginAsync("9940000009");
        var (orgId, id, slug) = await DraftAsync(owner, "Visibility DirectPublish");

        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish" })).StatusCode);

        await DrainAsync();
        Assert.Equal(HttpStatusCode.OK,
            (await _factory.CreateClient().GetAsync($"/v1/events/{slug}")).StatusCode);
    }

    /// <summary><b>Approval hands control back, it does not publish.</b> The creator — not a reviewer —
    /// ends an approved event's private life, and does so on their own timing.
    ///
    /// <para>The other half of the same defect: the reviewer gate inside <c>if (isPaid)</c> demanded a
    /// REVIEWER press publish even from <c>Approved</c>, so an organiser whose paid event had already been
    /// approved could not act on that approval at all.</para></summary>
    [Fact]
    public async Task A_creator_publishes_their_own_approved_paid_event()
    {
        var reviewer = await ReviewerAsync("9940000030");
        var (owner, ownerId) = await LoginAsync("9940000031");

        // Paid needs the organiser paid-verified and the org verified (M8) before publish will pass.
        await owner.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Owner" });
        await owner.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Owner" });
        var (orgId, id, slug) = await DraftAsync(owner, "Visibility ApprovedPaid");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.TicketTypes.Add(new TicketType
            {
                EventId = id, Name = "Entry", PricePaise = 50_000, Quantity = 20,
                SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(29),
            });
            await db.SaveChangesAsync();
        }

        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "claim_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "approve_review" });

        // Still not public — approval is permission, not publication.
        await AssertNotPublicAsync(id, slug, "Approved (paid)");

        // …and the CREATOR is the one who may end that.
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("published", (await Json(res)).GetProperty("status").GetString());
    }

    /// <summary>A paid event that never entered review still cannot go straight out — D-047, unchanged.</summary>
    [Fact]
    public async Task A_paid_event_still_cannot_publish_straight_from_draft()
    {
        var (owner, _) = await LoginAsync("9940000032");
        var (orgId, id, _) = await DraftAsync(owner, "Visibility PaidDraft");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.TicketTypes.Add(new TicketType
            {
                EventId = id, Name = "Entry", PricePaise = 50_000, Quantity = 20,
                SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(29),
            });
            await db.SaveChangesAsync();
        }

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish" });
        Assert.Equal("paid_event_requires_review", (await Json(res)).GetProperty("error").GetString());
    }

    // ── The organiser keeps their own event, which is not the same as it being public ────────────

    /// <summary>D-268 — the creator sees their own event in every state. That is the behaviour most easily
    /// mistaken for a leak: the same URL answers 200 to its owner and 404 to everyone else.</summary>
    [Fact]
    public async Task The_creator_sees_their_pending_event_but_a_signed_in_stranger_does_not()
    {
        var (owner, _) = await LoginAsync("9940000020");
        var (orgId, id, slug) = await DraftAsync(owner, "Visibility OwnerView");
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/v1/events/{slug}")).StatusCode);

        var (stranger, _) = await LoginAsync("9940000021");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/v1/events/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _factory.CreateClient().GetAsync($"/v1/events/{slug}")).StatusCode);
    }
}
