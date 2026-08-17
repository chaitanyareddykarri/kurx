using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-363 §1/§2/§4 — leaving `Approved`, and what approval actually binds to.
///
/// <para>D-377 made `Approved` a state an event genuinely SITS in rather than passes through, which
/// exposed three gaps at once. It was forward-only: no withdrawal, no cancellation, so an organiser who
/// changed their mind was stuck holding it and a reviewer who approved in error could not take it back.
/// And `IsEditLocked` covered the review states only, so an approved event was **fully editable with no
/// re-review** — approved, then retitled, re-dated, re-venued, and published as something no reviewer had
/// ever seen. The approval was real; what it applied to was not.</para></summary>
public class ApprovedEventLifecycleTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public ApprovedEventLifecycleTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Approved", Slug = "approved-lc" };
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
        var t = await Json(await c.PostAsJsonAsync("/v1/auth/otp/verify",
            new { phone, code = _factory.WhatsApp.LastOtpFor(phone) }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return (c, t.GetProperty("user_id").GetGuid());
    }

    /// <summary>The factory's CACHED reviewer, deliberately, rather than one login per test. Logging a
    /// fresh reviewer in for each test re-requests an OTP for a phone that already has a live one, which
    /// the resend cooldown (D-005) refuses — the second request issues no code, the stale one no longer
    /// verifies, and the test dies reading `access_token` off a problem document. That failure names the
    /// wrong thing entirely.</summary>
    private Task<HttpClient> ReviewerAsync() => _factory.ReviewerClientAsync();

    /// <summary>An event that has been through review and come out approved — the state under test.
    /// <paramref name="seed"/> runs on the DRAFT, before submission, for anything the reviewer must have
    /// seen: a ticket type added afterwards is a price nobody reviewed, which is itself material.</summary>
    private async Task<(Guid OrgId, Guid EventId)> ApprovedAsync(HttpClient owner, HttpClient reviewer, string title,
        Func<Guid, Guid, Task>? seed = null)
    {
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Appr {Guid.NewGuid():N}"[..18], "Company");
        var ev = await Json(await owner.CreateEventAsync(orgId, new
        {
            title, description = "An approved event under test.",
            categoryId = _categoryId, venueName = "Hall", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(4),
            visibility = "Listed",
        }));
        var id = ev.GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(id);
        if (seed is not null) await seed(orgId, id);
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "claim_review" });
        var approved = await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "approve_review" });
        Assert.Equal("approved", (await Json(approved)).GetProperty("status").GetString());
        return (orgId, id);
    }

    private async Task<EventStatus> StatusAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Events.Where(e => e.Id == id).Select(e => e.Status).FirstAsync();
    }

    // ── §1 · Withdrawal ──────────────────────────────────────────────────────────────

    /// <summary>An approved event has never been public, so it can hold no orders — returning it to
    /// `Draft` destroys nothing and puts it where its creator can edit and resubmit. Without this,
    /// deciding to rework an approved event was a dead end.</summary>
    [Fact]
    public async Task An_approved_event_can_be_withdrawn_to_draft()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9930000001");
        var (orgId, id) = await ApprovedAsync(owner, reviewer, "Approved Withdraw");

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "withdraw" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("draft", (await Json(res)).GetProperty("status").GetString());

        // And it is editable again — the point of going back rather than sideways.
        Assert.Equal(HttpStatusCode.OK, (await owner.PatchAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}", new { title = "Reworked Title" })).StatusCode);
    }

    // ── §2 · Cancellation ────────────────────────────────────────────────────────────

    /// <summary>For the organiser abandoning the event outright rather than reworking it. D-101 is
    /// unchanged: `Cancelled` is terminal, never resurrected.</summary>
    [Fact]
    public async Task An_approved_event_can_be_cancelled()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9930000002");
        var (orgId, id) = await ApprovedAsync(owner, reviewer, "Approved Cancel");

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "cancel" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("cancelled", (await Json(res)).GetProperty("status").GetString());

        // Terminal: only archive follows. Publishing a cancelled event must stay impossible.
        var republish = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish" });
        Assert.False(republish.IsSuccessStatusCode);
    }

    // ── §4 · Approval binds to what was reviewed ─────────────────────────────────────

    /// <summary>Kept in the same order as the <c>[InlineData]</c> rows below; the index is the phone.</summary>
    private static readonly string[] MaterialFields =
        ["title", "startsAt", "venueName", "capacity", "visibility", "timezone"];

    /// <summary>The defect this closes: approved, then changed into a different event, then published.
    /// A material edit sends it back to the queue rather than being refused — refusing would mean an
    /// approved event can never be corrected at all.</summary>
    [Theory]
    [InlineData("title")]
    [InlineData("startsAt")]
    [InlineData("venueName")]
    [InlineData("capacity")]
    [InlineData("visibility")]
    [InlineData("timezone")]   // moves the effective times without touching them
    public async Task A_material_edit_after_approval_returns_the_event_to_review(string field)
    {
        var reviewer = await ReviewerAsync();
        // One phone per case, by position. Deriving it from the field's LENGTH collided three ways
        // ("startsAt", "capacity" and "timezone" are all 8), and two cases sharing a phone means the
        // second one asks for an OTP the resend cooldown will not issue.
        var (owner, _) = await LoginAsync($"99300000{20 + Array.IndexOf(MaterialFields, field)}");
        var (orgId, id) = await ApprovedAsync(owner, reviewer, $"Approved Material {field}");

        object body = field switch
        {
            "title" => new { title = "A Completely Different Event" },
            // EARLIER than the event's existing end, not later: `ApplyUpdateAsync` re-checks
            // `EndsAt > StartsAt` on the merged entity, so moving the start past an untouched end is
            // `invalid_dates` (400) and the test would fail on its own payload rather than on the rule.
            "startsAt" => new { startsAt = DateTime.UtcNow.AddDays(20) },
            "venueName" => new { venueName = "A Different Venue Entirely" },
            "capacity" => new { capacity = 5000 },
            "visibility" => new { visibility = "Unlisted" },
            _ => new { timezone = "America/New_York" },
        };

        var patch = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}", body);
        // The problem document, not just the code: a bare "expected OK, got BadRequest" says nothing
        // about WHICH rule refused, and finding out costs another full suite run.
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        Assert.Equal(EventStatus.PendingReview, await StatusAsync(id));
    }

    /// <summary>The other half, and the reason this is a split rather than a freeze: forcing a fresh
    /// review to fix a typo in the FAQ makes re-review something organisers route around.</summary>
    [Fact]
    public async Task A_cosmetic_edit_after_approval_leaves_the_approval_standing()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9930000003");
        var (orgId, id) = await ApprovedAsync(owner, reviewer, "Approved Cosmetic");

        var res = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}", new
        {
            subtitle = "Now with a subtitle",
            contactEmail = "hello@example.com",
            bannerKey = "events/banner-2.jpg",
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(EventStatus.Approved, await StatusAsync(id));
    }

    /// <summary>The consequence that matters: after a material edit the event is back in the queue, so
    /// the creator cannot publish it. Without this the whole rule would be decorative.</summary>
    [Fact]
    public async Task A_material_edit_takes_away_the_right_to_publish()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9930000004");
        var (orgId, id) = await ApprovedAsync(owner, reviewer, "Approved Then Changed");

        await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}", new { title = "Something Else" });

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition",
            new { action = "publish_approved" });
        Assert.False(res.IsSuccessStatusCode);
        Assert.Equal(EventStatus.PendingReview, await StatusAsync(id));
    }

    /// <summary>A reviewer is told why it came back. An item reappearing in the queue with no reason is
    /// indistinguishable from one that was never decided.</summary>
    [Fact]
    public async Task The_return_to_review_is_audited()
    {
        var reviewer = await ReviewerAsync();
        var (owner, ownerId) = await LoginAsync("9930000005");
        var (orgId, id) = await ApprovedAsync(owner, reviewer, "Approved Audited");

        await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}", new { capacity = 999 });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var row = await db.AuditLogs
            .Where(a => a.EntityId == id && a.Action == "event.review.reopened_by_edit")
            .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync();
        Assert.NotNull(row);
        Assert.Equal(ownerId, row!.ActorId);
    }

    /// <summary>§4 names ticket price as material, and price does not live on `events` — so the trigger on
    /// the event's own PATCH could not see it. Approved on a free event, then ₹5,000 a ticket, then
    /// publish it yourself: the rule was real and the door beside it was open.</summary>
    [Fact]
    public async Task A_price_change_after_approval_returns_the_event_to_review()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9930000007");
        // The free ticket exists on the DRAFT, so the reviewer approved THIS price. Adding one afterwards
        // is a different act and reopens review on its own — asserted below.
        JsonElement tt = default;
        var (orgId, id) = await ApprovedAsync(owner, reviewer, "Approved Then Repriced",
            async (o, e) => tt = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{o}/events/{e}/ticket-types", TicketBody(0))));

        var repriced = await owner.PatchAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/ticket-types/{tt.GetProperty("id").GetGuid()}", TicketBody(500_000));
        Assert.True(repriced.IsSuccessStatusCode, await repriced.Content.ReadAsStringAsync());
        Assert.Equal(EventStatus.PendingReview, await StatusAsync(id));
    }

    /// <summary>The same rule at the earlier moment. `IsEditLocked` froze the event's own fields under a
    /// reviewer while its PRICES stayed editable — so a reviewer could approve a price that had already
    /// been replaced underneath them.</summary>
    [Fact]
    public async Task A_ticket_price_cannot_change_while_a_reviewer_holds_the_event()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9930000008");
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Appr {Guid.NewGuid():N}"[..18], "Company");
        var ev = await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Priced Under Review", description = "Submitted, then repriced.",
            categoryId = _categoryId, venueName = "Hall", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(4),
        }));
        var id = ev.GetProperty("id").GetGuid();
        var tt = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/ticket-types", TicketBody(10_000)));
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "claim_review" });

        var res = await owner.PatchAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/ticket-types/{tt.GetProperty("id").GetGuid()}", TicketBody(999_000));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("event_under_review", (await Json(res)).GetProperty("error").GetString());
    }

    /// <summary>The other half of the same door: a ticket type ADDED after approval is a price the
    /// reviewer never saw, so it reopens review just as changing one does. A free approved event could
    /// otherwise acquire its whole price list the moment it was cleared.</summary>
    [Fact]
    public async Task A_ticket_type_added_after_approval_returns_the_event_to_review()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9930000016");
        var (orgId, id) = await ApprovedAsync(owner, reviewer, "Approved Then Priced");

        var added = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/ticket-types", TicketBody(250_000));
        Assert.True(added.IsSuccessStatusCode, await added.Content.ReadAsStringAsync());
        Assert.Equal(EventStatus.PendingReview, await StatusAsync(id));
    }

    /// <summary>Eligibility is material, and the audience rule is eligibility asked through a different
    /// endpoint — who may attend, changed after the person who assessed who may attend said yes.</summary>
    [Fact]
    public async Task An_audience_rule_change_after_approval_returns_the_event_to_review()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9930000009");
        var (orgId, id) = await ApprovedAsync(owner, reviewer, "Approved Then Restricted");

        var res = await owner.PutAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/audience",
            new { requireVerified = true, externalOrgsAllowed = false });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        Assert.Equal(EventStatus.PendingReview, await StatusAsync(id));
    }

    /// <summary>§4 names legal/authorization as material, and the letter is the evidence a reviewer read.
    /// Resubmitting already cleared the AUTHORIZATION's own verdict; it left the EVENT's standing, so an
    /// approved event could go live on a decision made about a letter that had since been swapped.</summary>
    [Fact]
    public async Task Replacing_the_authorization_letter_after_approval_returns_the_event_to_review()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9930000017");
        var (_, id) = await ApprovedAsync(owner, reviewer, "Approved Then Re-lettered");

        var res = await owner.PostAsJsonAsync($"/v1/events/{id}/authorization", new
        {
            headName = "Dr. A. Rao", headDesignation = "Head of Department",
            officialEmail = "hod@college.edu", officialPhone = "+919700000000",
            representativeRole = "Principal",
            letterheadDocumentKey = "events/seed/authorization/letter",
            signatureDocumentKey = (string?)null,
            supportingDocumentKeys = new[] { "events/seed/authorization/annexure" },
        });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        Assert.Equal(EventStatus.PendingReview, await StatusAsync(id));
    }

    /// <summary>A valid ticket body at a given price — the fields the endpoint requires, nothing more.</summary>
    private static object TicketBody(long pricePaise) => new
    {
        name = "General", pricePaise, pricingUnit = "PerTicket", registrationMode = "Individual",
        quantity = 100, saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(20),
        perUserLimit = 5, isAllAccess = false, isCompetition = false,
    };

    /// <summary>Editing a DRAFT must not have acquired a status change. The trigger is scoped to
    /// `Approved`, and a rule that fires one state too wide would send every draft edit to the queue.</summary>
    [Fact]
    public async Task Editing_a_draft_is_unaffected()
    {
        var (owner, _) = await LoginAsync("9930000006");
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Appr {Guid.NewGuid():N}"[..18], "Company");
        var ev = await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Approved Draft Untouched", description = "Still a draft.",
            categoryId = _categoryId, venueName = "Hall", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(4),
        }));
        var id = ev.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await owner.PatchAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}", new { title = "Edited Freely" })).StatusCode);
        Assert.Equal(EventStatus.Draft, await StatusAsync(id));
    }
}
