using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>M7 Slice A (D-101): event cancellation as a terminal state, organization-verification
/// resubmission rules (Representative may resubmit — the D-075 regression; suspended may not; rejections
/// capped), and derived representation vacancy blocking publish. Real HTTP + kurx_test.</summary>
public class WorkflowLifecycleTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public WorkflowLifecycleTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "WF Cat", Slug = "wf-cat" };
                db.EventCategories.Add(cat);
                db.SaveChanges();
                _categoryId = cat.Id;
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

    /// <summary>Creates an event that is ready to publish. D-266 M5 added institutional authorization to
    /// that definition: a Public event representing a non-personal org cannot publish without an approved
    /// one, and every case in this class is about the lifecycle rather than about that rule.</summary>
    private async Task<Guid> CreateEventAsync(HttpClient client, Guid orgId)
    {
        var id = (await Json(await client.CreateEventAsync(orgId, new
        {
            title = "WF Fest " + Guid.NewGuid().ToString("N")[..6],
            description = "An event with plenty of detail.",
            categoryId = _categoryId, venueName = "Main Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }))).GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(id);
        return id;
    }

    private static Task<HttpResponseMessage> Transition(HttpClient client, Guid orgId, Guid eventId, string action) =>
        client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action });

    private static Task<HttpResponseMessage> SubmitVerification(HttpClient client, Guid orgId) =>
        client.PostAsJsonAsync($"/v1/orgs/{orgId}/verification/submit",
            new { documents = new[] { new { docType = "registration_cert", storageKey = "private/verif/reg.pdf" } } });

    // ── Cancellation (terminal state, distinct from Closed) ──────────────────
    [Fact]
    public async Task Organizer_can_cancel_a_published_event_and_it_leaves_discovery()
    {
        var owner = await LoginAsync("9930000001");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Cancel Fest College");
        var eventId = await CreateEventAsync(owner, orgId);
        Assert.Equal(HttpStatusCode.OK, (await Transition(owner, orgId, eventId, "publish")).StatusCode);

        var cancelled = await Transition(owner, orgId, eventId, "cancel");
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("cancelled", (await Json(cancelled)).GetProperty("status").GetString());

        // PublicBaseQuery filters Status == Published, so a cancelled event drops out of discovery.
        var pub = _factory.CreateClient();
        var found = await Json(await pub.GetAsync("/v1/events?q=WF Fest"));
        Assert.DoesNotContain(found.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == eventId);
    }

    [Fact]
    public async Task Cancelled_event_cannot_be_published_again_but_can_be_archived()
    {
        var owner = await LoginAsync("9930000002");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Terminal State College");
        var eventId = await CreateEventAsync(owner, orgId);
        await Transition(owner, orgId, eventId, "publish");
        await Transition(owner, orgId, eventId, "cancel");

        // No path out of Cancelled back to Published — a cancelled event is never resurrected (D-101).
        var republish = await Transition(owner, orgId, eventId, "publish");
        Assert.Equal(HttpStatusCode.Conflict, republish.StatusCode);
        Assert.Equal("invalid_transition", (await Json(republish)).GetProperty("error").GetString());

        var archived = await Transition(owner, orgId, eventId, "archive");
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        Assert.Equal("archived", (await Json(archived)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Organizer_cannot_cancel_once_the_event_has_started()
    {
        var owner = await LoginAsync("9930000003");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Started Already College");
        var eventId = await CreateEventAsync(owner, orgId);
        await Transition(owner, orgId, eventId, "publish");

        // Move the event into the past — cancelling now would owe refunds after the fact (D-101 default #1).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ev = await db.Events.FirstAsync(e => e.Id == eventId);
            ev.StartsAt = DateTime.UtcNow.AddHours(-3);
            ev.EndsAt = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }

        var res = await Transition(owner, orgId, eventId, "cancel");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("event_already_started", (await Json(res)).GetProperty("error").GetString());
    }

    // ── Organization verification resubmission (D-101) ───────────────────────
    [Fact]
    public async Task Representative_can_resubmit_verification_after_changes_requested()
    {
        // Regression guard for D-075: an institution created event-first has a Representative and NO Owner,
        // so the old Owner-only guard locked its submitter out of resubmitting. It must work now.
        var rep = await LoginAsync("9930000004");
        var org = await Json(await rep.PostAsJsonAsync("/v1/orgs/representation-requests", new
        {
            name = "Resubmit Institute", type = "College",
            documents = new[] { new { docType = "registration_cert", storageKey = "private/verif/reg.pdf" } },
        }));
        var orgId = org.GetProperty("id").GetGuid();
        Assert.Equal("representative", org.GetProperty("role").GetString());   // no Owner exists

        var reviewer = await _factory.ReviewerClientAsync();
        var rc = await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review",
            new { decision = "request_changes", notes = "letterhead unreadable" });
        Assert.Equal(HttpStatusCode.OK, rc.StatusCode);

        var resubmit = await SubmitVerification(rep, orgId);
        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);
        Assert.Equal("pendingreview", (await Json(resubmit)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Suspended_org_cannot_resubmit_itself_back_into_review()
    {
        var owner = await LoginAsync("9930000005");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Suspendable Society", status: OrgVerificationStatus.Unverified);
        await SubmitVerification(owner, orgId);

        var reviewer = await _factory.ReviewerClientAsync();
        await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review", new { decision = "approve" });
        await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/suspend", new { reason = "chargebacks" });

        // Escaping a suspension is an admin decision, never a self-service resubmission (D-101).
        var res = await SubmitVerification(owner, orgId);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Resubmission_is_capped_after_repeated_rejections()
    {
        var owner = await LoginAsync("9930000006");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Persistent Institute", status: OrgVerificationStatus.Unverified);
        var reviewer = await _factory.ReviewerClientAsync();

        for (var i = 0; i < OrgVerificationServiceLimits.MaxResubmissions; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await SubmitVerification(owner, orgId)).StatusCode);
            await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review",
                new { decision = "reject", reasonCode = "insufficient_evidence" });
        }

        var blocked = await SubmitVerification(owner, orgId);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Equal("resubmit_limit_reached", (await Json(blocked)).GetProperty("error").GetString());
    }

    // ── Clone (content copied, runtime state never inherited) ────────────────
    [Fact]
    public async Task Clone_copies_content_as_a_fresh_draft_and_resets_inventory()
    {
        var owner = await LoginAsync("9930000008");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Cloneable College");
        var eventId = await CreateEventAsync(owner, orgId);

        Guid tagId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            // Free ticket type on purpose: a priced one would make this a PAID event, which cannot
            // self-publish (paid_event_requires_review) — and this test needs a Published source.
            db.TicketTypes.Add(new TicketType
            {
                EventId = eventId, Name = "General", PricePaise = 0, Quantity = 100, Sold = 37,
                SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(30),
            });
            var tag = new Tag { Name = "Music", Slug = "music-" + Guid.NewGuid().ToString("N")[..6] };
            db.Tags.Add(tag);
            db.EventTags.Add(new EventTag { EventId = eventId, TagId = tag.Id });
            db.EventSessions.Add(new EventSession
            {
                EventId = eventId, Title = "Keynote", Description = "",
                StartsAt = DateTime.UtcNow.AddDays(20), EndsAt = DateTime.UtcNow.AddDays(20).AddHours(1), Sort = 0,
            });
            await db.SaveChangesAsync();
            tagId = tag.Id;
        }
        Assert.Equal(HttpStatusCode.OK, (await Transition(owner, orgId, eventId, "publish")).StatusCode);

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/clone", new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var clone = await Json(res);
        var cloneId = clone.GetProperty("id").GetGuid();

        Assert.NotEqual(eventId, cloneId);
        Assert.Equal("draft", clone.GetProperty("status").GetString());       // never inherits Published
        Assert.EndsWith("(Copy)", clone.GetProperty("title").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var copied = await db.TicketTypes.AsNoTracking().SingleAsync(t => t.EventId == cloneId);
            Assert.Equal("General", copied.Name);
            Assert.Equal(100, copied.Quantity);                               // configuration is copied
            Assert.Equal(0, copied.Sold);                                     // inventory is never inherited
            Assert.True(await db.EventTags.AsNoTracking().AnyAsync(t => t.EventId == cloneId && t.TagId == tagId));
            Assert.True(await db.EventSessions.AsNoTracking().AnyAsync(s => s.EventId == cloneId && s.Title == "Keynote"));
            // The source event is left completely untouched.
            var src = await db.Events.AsNoTracking().SingleAsync(e => e.Id == eventId);
            Assert.Equal(EventStatus.Published, src.Status);
            Assert.Equal(37, (await db.TicketTypes.AsNoTracking().SingleAsync(t => t.EventId == eventId)).Sold);
        }
    }

    [Fact]
    public async Task Non_member_cannot_clone_an_event()
    {
        var owner = await LoginAsync("9930000009");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Guarded Clone College");
        var eventId = await CreateEventAsync(owner, orgId);

        var outsider = await LoginAsync("9930000010");
        var res = await outsider.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/clone", new { });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── Representation vacancy (derived, blocks publish) ─────────────────────
    [Fact]
    public async Task Verified_org_with_no_verified_representative_cannot_publish()
    {
        var rep = await LoginAsync("9930000007");
        var orgId = await _factory.CreateVerifiedOrgAsync(rep, "Vacant Seat College");
        var eventId = await CreateEventAsync(rep, orgId);

        // The representative loses verification (e.g. revoked) and there is no Owner to fall back to.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var m = await db.Memberships.FirstAsync(x => x.OrgId == orgId && x.Role == OrgRole.Representative);
            m.IsVerified = false;
            await db.SaveChangesAsync();
        }

        var res = await Transition(rep, orgId, eventId, "publish");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("representation_vacant", (await Json(res)).GetProperty("error").GetString());
    }
}

/// <summary>Mirrors OrgVerificationService's public cap so the test asserts against the real limit.</summary>
internal static class OrgVerificationServiceLimits
{
    public const int MaxResubmissions = Kurx.Infrastructure.Orgs.OrgVerificationService.MaxResubmissions;
}
