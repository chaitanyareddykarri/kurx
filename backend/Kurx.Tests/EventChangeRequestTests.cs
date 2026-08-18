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

/// <summary>D-388 — an approved/live event cannot be changed by its host without an approved change
/// request.
///
/// <para>The defect these close was reproduced over HTTP against the running dev API before a line was
/// written: the owner of a <c>Published</c> public event sent one PATCH and its title and start date
/// changed on the spot — 200 OK, no reviewer, no notification, and <b>no audit row at all</b>. The
/// registrants who had already bought tickets were told nothing.</para>
///
/// <para>Both halves of the rule are tested, because both are load-bearing. A PRIVATE event stays
/// directly editable at every status — its host is its only audience — and an <c>Approved</c> event
/// keeps D-363 §4's behaviour, because it has never been public and can hold no orders. Narrowing the
/// guard to "any non-draft event" would have been simpler and wrong in both directions.</para></summary>
public class EventChangeRequestTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static Guid _privateTypeId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public EventChangeRequestTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new EventCategory { Level = CategoryLevel.Category, Name = "ChangeReq", Slug = "change-req" };
            db.EventCategories.Add(cat);
            // A Private-product Type, so the "Private events are exempt" half of the rule is testable at
            // all. Product is derived from the selected Type at creation (D-266 M1) and is immutable
            // afterwards, so there is no way to make an event Private except by creating it under one.
            // The archetype and the allowed-policy list are BOTH required, and a type carrying neither is
            // not a lighter fixture — it is an invalid one. Without `ArchetypeSlug` the event resolves no
            // behaviour model, and with no `AllowedRegistrationPoliciesJson` the default `Open` policy is
            // not on any list, so `PolicyResolver` refuses the publish with
            // `registration_policy_not_allowed_for_type` and the test fails on its own seed rather than on
            // the rule it is about.
            var privateType = new EventCategory
            {
                Level = CategoryLevel.Type, ParentId = cat.Id, Name = "ChangeReq Private",
                Slug = "change-req-private", ProductClass = EventProduct.Private,
                ArchetypeSlug = "private-gathering",
                AllowedRegistrationPoliciesJson = "[\"Open\"]",
            };
            db.EventCategories.Add(privateType);
            db.SaveChanges();
            _categoryId = cat.Id;
            _privateTypeId = privateType.Id;
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

    /// <summary>A Super Admin, for the one path that deliberately bypasses every guard here
    /// (<c>EmergencyUpdateAsync</c>, D-191). Same grant AdminEventManagementTests uses.</summary>
    private async Task<HttpClient> SuperAdminAsync(string phone)
    {
        var (client, userId) = await LoginAsync(phone);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.SuperAdmin, grantedBy: null);
        return client;
    }

    /// <summary>The factory's cached reviewer — see <c>ApprovedEventLifecycleTests</c> for why a fresh
    /// login per test dies on the OTP resend cooldown rather than on the rule under test.</summary>
    private Task<HttpClient> ReviewerAsync() => _factory.ReviewerClientAsync();

    private async Task<(Guid OrgId, Guid EventId)> DraftAsync(HttpClient owner, string title, Guid? typeId = null)
    {
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"CR {Guid.NewGuid():N}"[..18], "Company");
        var ev = await Json(await owner.CreateEventAsync(orgId, new
        {
            title, description = "An event under change-request test.",
            categoryId = _categoryId, typeId,
            venueName = "Main Hall", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(40), endsAt = DateTime.UtcNow.AddDays(40).AddHours(4),
            visibility = typeId == _privateTypeId ? "Unlisted" : "Listed",
        }));
        return (orgId, ev.GetProperty("id").GetGuid());
    }

    /// <summary>A PUBLISHED public event — the state the whole decision is about. Goes the real route
    /// (submit → claim → approve → publish), because an event whose status was written straight into the
    /// database would not prove that the guard survives the path organisers actually take.</summary>
    private async Task<(Guid OrgId, Guid EventId)> PublishedAsync(HttpClient owner, HttpClient reviewer, string title)
    {
        var (orgId, id) = await DraftAsync(owner, title);
        _factory.SeedApprovedEventAuthorization(id);
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "claim_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "approve_review" });
        var published = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish_approved" });
        Assert.Equal("published", (await Json(published)).GetProperty("status").GetString());
        return (orgId, id);
    }

    private async Task<Event> RowAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Events.AsNoTracking().FirstAsync(e => e.Id == id);
    }

    private static async Task<string?> ErrorAsync(HttpResponseMessage r)
    {
        var body = await Json(r);
        return body.TryGetProperty("error", out var e) ? e.GetString() : null;
    }

    // ── Draft · unchanged ────────────────────────────────────────────────────────────

    /// <summary>The regression that matters most: nothing about creating and editing a draft moves.
    /// A guard that also stopped drafts being edited would have broken the entire creation flow.</summary>
    [Fact]
    public async Task A_draft_is_still_edited_directly()
    {
        var (owner, _) = await LoginAsync("9940000001");
        var (orgId, id) = await DraftAsync(owner, "Draft Direct Edit");

        var patch = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}",
            new { title = "Draft Retitled", description = "Rewritten freely." });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal("Draft Retitled", (await RowAsync(id)).Title);

        // And no change request was invented on the way: a draft has nothing to propose against.
        Assert.Empty((await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{id}/change-requests"))).EnumerateArray());
    }

    /// <summary>A change request is refused where direct editing is allowed. Two ways to do one thing,
    /// with no way for the host to tell which their edit went through, is the state this avoids.</summary>
    [Fact]
    public async Task A_draft_cannot_open_a_change_request()
    {
        var (owner, _) = await LoginAsync("9940000002");
        var (orgId, id) = await DraftAsync(owner, "Draft No Request");

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new { title = "Nope" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("not_live_protected", await ErrorAsync(res));
    }

    // ── Live · the direct mutation that used to succeed ──────────────────────────────

    /// <summary>The reproduced defect, now refused. One case per protected field, because the guard is a
    /// predicate over the payload and a field omitted from it is a hole that no other test would find.</summary>
    [Theory]
    [InlineData("title", 10)]
    [InlineData("description", 11)]
    [InlineData("subtitle", 12)]
    [InlineData("startsAt", 13)]
    [InlineData("venueName", 14)]
    [InlineData("capacity", 15)]
    [InlineData("visibility", 16)]
    [InlineData("timezone", 17)]
    public async Task A_live_event_refuses_a_direct_edit_to_a_protected_field(string field, int phoneSuffix)
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync($"99400000{phoneSuffix}");
        var (orgId, id) = await PublishedAsync(owner, reviewer, $"Live Protected {field}");
        var before = await RowAsync(id);

        object body = field switch
        {
            "title" => new { title = "HIJACKED LIVE TITLE" },
            "description" => new { description = "A completely different event." },
            "subtitle" => new { subtitle = "Different subtitle" },
            // EARLIER than the untouched end, so a refusal is the GUARD and not `invalid_dates`.
            "startsAt" => new { startsAt = DateTime.UtcNow.AddDays(25) },
            "venueName" => new { venueName = "A Different Venue" },
            "capacity" => new { capacity = 9999 },
            "visibility" => new { visibility = "Unlisted" },
            _ => new { timezone = "America/New_York" },
        };

        var patch = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}", body);
        Assert.Equal(HttpStatusCode.Conflict, patch.StatusCode);
        Assert.Equal("change_request_required", await ErrorAsync(patch));

        // Refused means UNCHANGED, not "changed and then reported". Every protected column is compared,
        // because a guard that returned 409 after a partial write would pass a status-code-only assertion.
        var after = await RowAsync(id);
        Assert.Equal(before.Title, after.Title);
        Assert.Equal(before.Description, after.Description);
        Assert.Equal(before.Subtitle, after.Subtitle);
        Assert.Equal(before.StartsAt, after.StartsAt);
        Assert.Equal(before.VenueName, after.VenueName);
        Assert.Equal(before.Capacity, after.Capacity);
        Assert.Equal(before.Visibility, after.Visibility);
        Assert.Equal(before.Timezone, after.Timezone);
        Assert.Equal(before.Version, after.Version);
    }

    /// <summary>The other half of the field policy. An organiser fixing a phone number on the morning of
    /// their event cannot be made to wait for an admin, so the operational fields stay direct.</summary>
    [Fact]
    public async Task A_live_event_still_takes_a_direct_edit_to_an_operational_field()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000020");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Operational");

        var patch = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}", new
        {
            contactEmail = "help@example.com", contactPhone = "+919000000000",
            website = "https://example.com", bannerKey = "public/banners/new.png",
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal("help@example.com", (await RowAsync(id)).ContactEmail);
    }

    /// <summary>Private products are never reviewed, so nothing about them is frozen. Putting an admin
    /// between a family and their own wedding page would be an absurdity, not a safeguard.</summary>
    [Fact]
    public async Task A_live_private_event_is_still_edited_directly()
    {
        var (owner, _) = await LoginAsync("9940000021");
        var (orgId, id) = await DraftAsync(owner, "Private Live Edit", _privateTypeId);
        Assert.Equal(EventProduct.Private, (await RowAsync(id)).Product);
        // D-379: EVERY event needs an approved authorization before it can go live, Private included —
        // representation is about who is answerable, which visibility never decided.
        _factory.SeedApprovedEventAuthorization(id);

        /*
         * `PolicyResolver.AllowedFor` hard-limits a Private product to `InviteOnly` — the type's own
         * allow-list cannot widen it — while `Event.RegistrationPolicy` defaults to `Open` and NOTHING on
         * the create path derives it from the product. So a Private event created through the API is
         * refused `registration_policy_not_allowed_for_type` on publish, every time.
         *
         * That is a real pre-existing gap and it is NOT D-388's to fix: nothing here touches how an event
         * is created. Set on the row so this test can reach the state it is actually about — whether a
         * LIVE Private event stays directly editable. Flagged rather than quietly worked around.
         */
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var row = await db.Events.FirstAsync(e => e.Id == id);
            row.RegistrationPolicy = EventRegistrationPolicy.InviteOnly;
            await db.SaveChangesAsync();
        }

        // Private products publish straight from Draft — they never enter the review queue.
        var published = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish" });
        // The problem document, not just the code: "expected OK, got BadRequest" says nothing about WHICH
        // publish gate refused, and finding out costs another suite run.
        Assert.True(published.IsSuccessStatusCode, await published.Content.ReadAsStringAsync());

        var patch = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}", new { title = "Renamed Privately" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal("Renamed Privately", (await RowAsync(id)).Title);
    }

    // ── The change request itself ────────────────────────────────────────────────────

    /// <summary>The whole guarantee in one test: a proposal exists, and the live event does not move.</summary>
    [Fact]
    public async Task A_change_request_leaves_the_live_event_untouched()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000030");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Proposal");
        var before = await RowAsync(id);

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new
        {
            title = "Live Proposal 2026",
            startsAt = DateTime.UtcNow.AddDays(35),
            reason = "The venue moved us to a later slot.",
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var cr = await Json(res);
        Assert.Equal("pending", cr.GetProperty("status").GetString());
        Assert.Equal(before.Version, cr.GetProperty("base_version").GetInt32());
        Assert.False(cr.GetProperty("stale").GetBoolean());

        // The diff a reviewer will read, built server-side so all three clients show the same comparison.
        var changes = cr.GetProperty("changes").EnumerateArray().ToList();
        var title = changes.Single(c => c.GetProperty("field").GetString() == "title");
        Assert.Equal("Live Proposal", title.GetProperty("current").GetString());
        Assert.Equal("Live Proposal 2026", title.GetProperty("proposed").GetString());

        var after = await RowAsync(id);
        Assert.Equal(before.Title, after.Title);
        Assert.Equal(before.StartsAt, after.StartsAt);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(EventStatus.Published, after.Status);   // and it never left the public site
    }

    /// <summary>A second proposal replaces the first. Two pending requests would make "what is waiting
    /// for approval" unanswerable — and the partial unique index makes it impossible anyway, so the
    /// alternative to replacing is a save the host cannot clear.</summary>
    [Fact]
    public async Task A_second_proposal_replaces_the_pending_one()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000031");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Replace");

        var first = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new { title = "First Try" }));
        var second = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new { title = "Second Try" }));
        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());

        var list = (await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{id}/change-requests"))).EnumerateArray().ToList();
        Assert.Single(list);
        Assert.Equal("Second Try",
            list[0].GetProperty("changes").EnumerateArray()
                .Single(c => c.GetProperty("field").GetString() == "title").GetProperty("proposed").GetString());
    }

    /// <summary>Proposing nothing is refused rather than queued. A reviewer opening an empty diff learns
    /// only that someone pressed a button.</summary>
    [Fact]
    public async Task A_proposal_that_changes_nothing_is_refused()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000032");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live No Change");

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests",
            new { title = "Live No Change" });   // identical to what is live
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("no_changes", await ErrorAsync(res));
    }

    /// <summary>The host takes their own proposal back, and the event is proposable again.</summary>
    [Fact]
    public async Task A_host_can_withdraw_their_own_pending_request()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000033");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Withdraw");

        var cr = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new { title = "Withdraw Me" }));
        var crId = cr.GetProperty("id").GetGuid();

        var res = await owner.DeleteAsync($"/v1/orgs/{orgId}/events/{id}/change-requests/{crId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("withdrawn", (await Json(res)).GetProperty("status").GetString());

        // Withdrawn frees the slot: the partial unique index only binds PENDING rows.
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new { title = "Try Again" })).StatusCode);
    }

    // ── Approval ─────────────────────────────────────────────────────────────────────

    /// <summary>Approval applies every proposed value and moves the event to version N+1. The event's own
    /// STATUS does not move at all — the two lifecycles are separate, which is why the change request has
    /// its own enum.</summary>
    [Fact]
    public async Task Approval_applies_the_proposed_values_and_bumps_the_version()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000040");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Approve");
        var before = await RowAsync(id);
        var newStart = DateTime.UtcNow.AddDays(38);

        var cr = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new
        {
            title = "Live Approve 2026", description = "Updated description.",
            startsAt = newStart, venueName = "Auditorium B", capacity = 750,
        }));
        var crId = cr.GetProperty("id").GetGuid();

        var decided = await reviewer.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/change-requests/{crId}/decision", new { approve = true });
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        var view = await Json(decided);
        Assert.Equal("approved", view.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, view.GetProperty("applied_at").ValueKind);

        var after = await RowAsync(id);
        Assert.Equal("Live Approve 2026", after.Title);
        Assert.Equal("Updated description.", after.Description);
        Assert.Equal("Auditorium B", after.VenueName);
        Assert.Equal(750, after.Capacity);
        Assert.Equal(before.Version + 1, after.Version);
        Assert.Equal(EventStatus.Published, after.Status);   // still live throughout

        // The approval is audited, and the decision is in the SAME store the event review writes to —
        // no second review table (REVIEW_LIFECYCLE.md).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == id && a.Action == "event.change_request.approved"));
        Assert.True(await db.VerificationReviews.AnyAsync(v =>
            v.SubjectId == id && v.SubjectType == VerificationSubjectType.Event
            && v.Decision == VerificationDecision.Approve));
    }

    /// <summary>The approved values are authoritative EVERYWHERE, not just on the workspace read. The
    /// search projection is the one denormalised copy of an event's content in the system, and it is
    /// re-enqueued by the shared apply path rather than by anything this feature wrote.</summary>
    [Fact]
    public async Task Approval_reaches_the_public_read_and_re_enqueues_the_search_projection()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000041");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Propagate");

        var proposed = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests",
            new { title = "Live Propagate 2026" });
        Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);
        var cr = await Json(proposed);
        var decided = await reviewer.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/change-requests/{cr.GetProperty("id").GetGuid()}/decision", new { approve = true });
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);

        // The public, unauthenticated read — what an attendee sees. By SLUG: `/v1/events/{id}` is the
        // authenticated organiser read, and an anonymous caller gets no body from it at all, which is a
        // 404 masquerading as a JSON parse error if you go straight to reading the response.
        var anon = _factory.CreateClient();
        var slug = (await RowAsync(id)).Slug;
        var pubRes = await anon.GetAsync($"/v1/events/{slug}");
        Assert.Equal(HttpStatusCode.OK, pubRes.StatusCode);
        Assert.Equal("Live Propagate 2026", (await Json(pubRes)).GetProperty("title").GetString());

        // And the discovery index has been told. Asserting the OUTBOX rather than the document, because
        // the projector is a background job: waiting for it would make this a timing test.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // Matched on the IDEMPOTENCY KEY, not on the payload: `PayloadJson` is a jsonb column, and
        // `.Contains` over it translates to `jsonb ~~ jsonb`, which Postgres has no operator for. The key
        // is plain text and already carries the event id by construction (`search.reindex:{id}:{guid}`).
        Assert.True(await db.OutboxMessages.AnyAsync(m =>
            m.Type == "search.reindex" && m.IdempotencyKey.StartsWith($"search.reindex:{id}:")));
    }

    // ── Rejection ────────────────────────────────────────────────────────────────────

    /// <summary>A rejection must leave no mark on the live event whatsoever.</summary>
    [Fact]
    public async Task Rejection_leaves_the_live_event_exactly_as_it_was()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000042");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Reject");
        var before = await RowAsync(id);

        var cr = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests",
            new { title = "Should Never Land", capacity = 12345 }));

        var decided = await reviewer.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/change-requests/{cr.GetProperty("id").GetGuid()}/decision",
            new { approve = false, reasonCode = "ProhibitedContent", notes = "Not acceptable." });
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        var view = await Json(decided);
        Assert.Equal("rejected", view.GetProperty("status").GetString());
        Assert.Equal("ProhibitedContent", view.GetProperty("review_reason_code").GetString());
        // The host must be able to read WHY — a refusal they cannot act on is a dead end.
        Assert.Equal("Not acceptable.", view.GetProperty("review_notes").GetString());

        var after = await RowAsync(id);
        Assert.Equal(before.Title, after.Title);
        Assert.Equal(before.Capacity, after.Capacity);
        Assert.Equal(before.Version, after.Version);   // rejection is not a version
    }

    /// <summary>A rejection with no reason is refused. Free-form or absent refusals cannot be analysed,
    /// and the closed vocabulary is the same one the event review uses.</summary>
    [Fact]
    public async Task Rejection_requires_a_reason_code()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000043");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Reject No Reason");
        var cr = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new { title = "X Y Z" }));

        var res = await reviewer.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/change-requests/{cr.GetProperty("id").GetGuid()}/decision", new { approve = false });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ── Authorization ────────────────────────────────────────────────────────────────

    /// <summary>A host may not decide their own proposal, and holding a reviewer role does not change
    /// that. The event review already enforces this on <c>approve_review</c>; a rule written at one door
    /// is not a rule.</summary>
    [Fact]
    public async Task A_host_cannot_approve_their_own_change_request()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000050");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Self Approve");
        var cr = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new { title = "Self Approved" }));
        var crId = cr.GetProperty("id").GetGuid();

        // As the host: not a reviewer at all.
        var asHost = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests/{crId}/decision", new { approve = true });
        Assert.Equal(HttpStatusCode.Forbidden, asHost.StatusCode);
        Assert.Equal("reviewer_required", await ErrorAsync(asHost));

        Assert.Equal("Live Self Approve", (await RowAsync(id)).Title);
    }

    /// <summary>The same rule from the other side: a genuine reviewer who is also the requester. The
    /// check is on WHO PROPOSED, not on whether the caller holds the role.</summary>
    [Fact]
    public async Task A_reviewer_cannot_approve_a_request_they_made_themselves()
    {
        var reviewer = await ReviewerAsync();
        var second = await _factory.SecondReviewerClientAsync();
        var (owner, _) = await LoginAsync("9940000051");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Reviewer Self");

        // Give the reviewer real management standing on the event, so the proposal is theirs to make.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ev = await db.Events.FirstAsync(e => e.Id == id);
            var me = await Json(await second.GetAsync("/v1/me"));
            ev.CreatedBy = me.GetProperty("id").GetGuid();
            await db.SaveChangesAsync();
        }

        var cr = await Json(await second.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new { title = "Reviewer's Own" }));
        var res = await second.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/change-requests/{cr.GetProperty("id").GetGuid()}/decision", new { approve = true });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("cannot_review_own_request", await ErrorAsync(res));
        Assert.Equal("Live Reviewer Self", (await RowAsync(id)).Title);
    }

    /// <summary>Somebody with no standing on the event cannot propose a change to it.</summary>
    [Fact]
    public async Task A_stranger_cannot_open_a_change_request()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000052");
        var (stranger, _) = await LoginAsync("9940000053");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Stranger");

        var res = await stranger.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new { title = "Not Mine" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Empty((await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{id}/change-requests"))).EnumerateArray());
    }

    // ── Concurrency ──────────────────────────────────────────────────────────────────

    /// <summary>NO LOST APPROVED CHANGES. A proposal authored against version N must not be applied over
    /// version N+1 — the values a reviewer approved were read in a context that no longer exists, and a
    /// silent overwrite would destroy whatever was approved in between.</summary>
    [Fact]
    public async Task A_stale_request_cannot_overwrite_a_newer_version()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000060");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Stale");

        // Proposal A, authored against version N.
        var a = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new { title = "Proposal A" }));
        var aId = a.GetProperty("id").GetGuid();
        var baseVersion = a.GetProperty("base_version").GetInt32();

        // The event moves to N+1 underneath it — here by the Super Admin emergency path, which
        // deliberately bypasses the guard (D-191) and is the realistic way a live event changes while a
        // proposal is pending.
        var admin = await SuperAdminAsync("9940000063");
        var emergency = await admin.PostAsJsonAsync($"/v1/admin/events/{id}/emergency-edit",
            new { reason = "Venue emergency", input = new { title = "Emergency Retitled" } });
        Assert.Equal(HttpStatusCode.OK, emergency.StatusCode);
        Assert.Equal(baseVersion + 1, (await RowAsync(id)).Version);

        // Approving A now would silently discard the emergency edit.
        var decided = await reviewer.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/change-requests/{aId}/decision", new { approve = true });
        Assert.Equal(HttpStatusCode.Conflict, decided.StatusCode);
        Assert.Equal("version_conflict", await ErrorAsync(decided));

        // The newer value stands, and the request is still pending for the host to withdraw and redo.
        Assert.Equal("Emergency Retitled", (await RowAsync(id)).Title);
        var list = (await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{id}/change-requests"))).EnumerateArray().ToList();
        Assert.Equal("pending", list[0].GetProperty("status").GetString());
        Assert.True(list[0].GetProperty("stale").GetBoolean());
    }

    /// <summary>A decided request is decided. The second reviewer's verdict is refused rather than
    /// overwriting the first — claimed in SQL, so both passing the read-time gate is not enough.</summary>
    [Fact]
    public async Task A_decided_request_cannot_be_decided_again()
    {
        var reviewer = await ReviewerAsync();
        var second = await _factory.SecondReviewerClientAsync();
        var (owner, _) = await LoginAsync("9940000061");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Double Decide");
        var cr = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new { title = "Decide Once" }));
        var crId = cr.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await reviewer.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/change-requests/{crId}/decision", new { approve = true })).StatusCode);

        var again = await second.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/change-requests/{crId}/decision",
            new { approve = false, reasonCode = "Incomplete" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("change_request_decided", await ErrorAsync(again));
        Assert.Equal("Decide Once", (await RowAsync(id)).Title);   // the approval stands
    }

    // ── Atomicity ────────────────────────────────────────────────────────────────────

    /// <summary>ALL or NOTHING. A proposal the apply path refuses must leave the live event with none of
    /// its values — never the new title beside the old date — and must leave the request PENDING rather
    /// than marked approved for a change that never landed.</summary>
    [Fact]
    public async Task A_proposal_the_apply_path_refuses_changes_nothing()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000062");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Atomic");
        var before = await RowAsync(id);

        // A valid-looking proposal whose category does not exist. It passes body validation (a non-empty
        // GUID) and is refused by `ApplyUpdateAsync` — AFTER the title has already been assigned on the
        // tracked entity, which is precisely the half-applied state the transaction has to undo.
        var cr = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/change-requests", new
        {
            title = "Atomic New Title",
            categoryId = Guid.NewGuid(),
        }));
        var crId = cr.GetProperty("id").GetGuid();

        var decided = await reviewer.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/change-requests/{crId}/decision", new { approve = true });
        Assert.False(decided.IsSuccessStatusCode);
        Assert.Equal("invalid_category", await ErrorAsync(decided));

        var after = await RowAsync(id);
        Assert.Equal(before.Title, after.Title);           // the title did NOT survive the rollback
        Assert.Equal(before.CategoryId, after.CategoryId);
        Assert.Equal(before.Version, after.Version);

        // And the verdict rolled back with it: the request is still awaiting a decision.
        var list = (await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{id}/change-requests"))).EnumerateArray().ToList();
        Assert.Equal("pending", list.Single().GetProperty("status").GetString());
    }

    // ── The sibling surfaces ─────────────────────────────────────────────────────────

    /// <summary>What a live event COSTS and WHO MAY ENTER do not live on the event row, and D-363 §3's
    /// lesson was that a guard on the one path you thought of holds only until someone adds a second.
    /// Both are refused on a live event by the same shared predicate.</summary>
    [Fact]
    public async Task Ticket_types_and_audience_rules_are_frozen_on_a_live_event_too()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000070");
        var (orgId, id) = await PublishedAsync(owner, reviewer, "Live Siblings");

        // The FULL payload, matching TicketTypeTests. A partial one is refused by body validation (400)
        // before the guard is reached, which would have made this test pass for the wrong reason had it
        // asserted only "not OK".
        var ticket = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/ticket-types", new
        {
            name = "Late Price Hike", pricePaise = 500000L, pricingUnit = "PerTicket",
            registrationMode = "Individual", groupMin = (int?)null, groupMax = (int?)null, quantity = 100,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30),
            perUserLimit = 5, isAllAccess = false,
        });
        Assert.Equal(HttpStatusCode.Conflict, ticket.StatusCode);
        Assert.Equal("change_request_required", await ErrorAsync(ticket));

        var audience = await owner.PutAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/audience",
            new { appliesTo = "EveryMember", requireVerified = true });
        Assert.Equal(HttpStatusCode.Conflict, audience.StatusCode);
        Assert.Equal("change_request_required", await ErrorAsync(audience));
    }

    // ── D-363 §4 · unchanged ─────────────────────────────────────────────────────────

    /// <summary>An <c>Approved</c> event is reviewed but NOT public — no attendee has seen it and it can
    /// hold no orders — so D-363 §4 stands: the edit applies and the event returns to the queue. This is
    /// the boundary the whole decision turns on, and it is asserted here as well as in
    /// <c>ApprovedEventLifecycleTests</c> so that widening the guard breaks a D-388 test too.</summary>
    [Fact]
    public async Task An_approved_but_unpublished_event_still_edits_directly_and_returns_to_review()
    {
        var reviewer = await ReviewerAsync();
        var (owner, _) = await LoginAsync("9940000071");
        var (orgId, id) = await DraftAsync(owner, "Approved Not Live");
        _factory.SeedApprovedEventAuthorization(id);
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "claim_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "approve_review" });

        var patch = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}", new { title = "Retitled Before Going Live" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var after = await RowAsync(id);
        Assert.Equal("Retitled Before Going Live", after.Title);
        Assert.Equal(EventStatus.PendingReview, after.Status);   // D-363 §4, not D-388
    }
}
