using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-266 M4 Phase 3 — the review lifecycle through the real API: real authorization, real
/// persistence, no mocks. The unit suite proves the transition table is correct; this proves the running
/// system honours it, which is a different claim.</summary>
public class ReviewLifecycleIntegrationTests(KurxApiFactory factory) : IClassFixture<KurxApiFactory>
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

    private async Task<(HttpClient Client, Guid OrgId)> LoginOrgAsync(string phone, string name)
    {
        var (c, _) = await LoginAsync(phone);
        return (c, _factory.SeedVerifiedOrgForClient(c, name));
    }

    private async Task<(Guid CatId, Guid TypeId)> TaxonAsync(string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == slug)
            .Select(c => new { c.Id, c.ParentId }).FirstAsync();
        return (t.ParentId!.Value, t.Id);
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Rev " + Guid.NewGuid().ToString("N")[..6], description = "a real description",
            categoryId = catId, typeId, venueName = "Hall", city = "C",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> Act(HttpClient c, Guid orgId, Guid eventId,
        string action, string? reasonCode = null, string? notes = null)
        => c.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition",
            new { action, reasonCode, notes });

    /// <summary>D-266 M7 — ticks every item on the reviewer's checklist, which <c>approve_review</c> now
    /// requires. Driven through the real API rather than seeded, because this suite's subject IS the review
    /// path and the checklist is now part of it. The cases that assert a REFUSAL deliberately do not call
    /// this: an approval blocked for a different reason must still be blocked.</summary>
    private static async Task CompleteChecklistAsync(HttpClient reviewer, Guid eventId)
    {
        var checklist = await Json(await reviewer.GetAsync($"/v1/admin/events/{eventId}/review-checklist"));
        foreach (var item in checklist.GetProperty("items").EnumerateArray())
            await reviewer.PutAsJsonAsync($"/v1/admin/events/{eventId}/review-checklist",
                new { itemKey = item.GetProperty("key").GetString(), @checked = true });
    }

    private async Task<EventStatus> StatusAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<KurxDbContext>()
            .Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.Status).FirstAsync();
    }

    private async Task<List<VerificationReviewRow>> ReviewsAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<KurxDbContext>()
            .VerificationReviews.AsNoTracking()
            .Where(r => r.SubjectType == VerificationSubjectType.Event && r.SubjectId == eventId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new VerificationReviewRow(r.Decision, r.ReviewerId, r.ReasonCode, r.Notes, r.CreatedAt))
            .ToListAsync();
    }

    private record VerificationReviewRow(VerificationDecision Decision, Guid? ReviewerId,
        string? ReasonCode, string? Notes, DateTime CreatedAt);

    // ── Full lifecycle ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_full_review_path_runs_end_to_end()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000001", "Lifecycle Review Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        Assert.Equal(HttpStatusCode.OK, (await Act(owner, orgId, id, "submit_for_review")).StatusCode);
        Assert.Equal(EventStatus.PendingReview, await StatusAsync(id));

        Assert.Equal(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "claim_review")).StatusCode);
        Assert.Equal(EventStatus.UnderReview, await StatusAsync(id));

        await CompleteChecklistAsync(reviewer, id);   // D-266 M7 — approve is gated on it
        Assert.Equal(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "approve_review")).StatusCode);
        Assert.Equal(EventStatus.Approved, await StatusAsync(id));
    }

    [Fact]
    public async Task Withdraw_returns_an_unclaimed_submission_to_draft()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000002", "Withdraw Org");
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_for_review");
        Assert.Equal(HttpStatusCode.OK, (await Act(owner, orgId, id, "withdraw")).StatusCode);
        Assert.Equal(EventStatus.Draft, await StatusAsync(id));
    }

    [Fact]
    public async Task A_claim_can_be_released_and_reclaimed()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000003", "Release Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");
        Assert.Equal(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "release_review")).StatusCode);
        Assert.Equal(EventStatus.PendingReview, await StatusAsync(id));

        Assert.Equal(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "claim_review")).StatusCode);
        Assert.Equal(EventStatus.UnderReview, await StatusAsync(id));
    }

    [Fact]
    public async Task Request_changes_then_resubmit_returns_to_the_queue()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000004", "Changes Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");
        Assert.Equal(HttpStatusCode.OK,
            (await Act(reviewer, orgId, id, "request_changes", notes: "Add the agenda.")).StatusCode);
        Assert.Equal(EventStatus.ChangesRequested, await StatusAsync(id));

        Assert.Equal(HttpStatusCode.OK, (await Act(owner, orgId, id, "submit_for_review")).StatusCode);
        Assert.Equal(EventStatus.PendingReview, await StatusAsync(id));
    }

    [Fact]
    public async Task Reject_requires_a_reason_and_records_it()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000005", "Reject Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");

        // No reason -> refused, and the event must not move.
        var noReason = await Act(reviewer, orgId, id, "reject_review");
        Assert.NotEqual(HttpStatusCode.OK, noReason.StatusCode);
        Assert.Equal(EventStatus.UnderReview, await StatusAsync(id));

        Assert.Equal(HttpStatusCode.OK,
            (await Act(reviewer, orgId, id, "reject_review", reasonCode: "Incomplete", notes: "Missing venue.")).StatusCode);
        Assert.Equal(EventStatus.Rejected, await StatusAsync(id));
    }

    [Fact]
    public async Task Request_changes_requires_notes()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000006", "Notes Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");

        var noNotes = await Act(reviewer, orgId, id, "request_changes");
        Assert.NotEqual(HttpStatusCode.OK, noNotes.StatusCode);
        Assert.Equal(EventStatus.UnderReview, await StatusAsync(id));
    }

    // ── Authorization ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_organiser_cannot_decide_their_own_submission()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000010", "SelfApprove Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");

        // The organiser can manage the event, but reviewing it is not theirs to do. Asserted as 403
        // specifically: a bare "not OK" let this return 400, which tells a client its request was
        // malformed rather than that it lacks the authority.
        foreach (var action in new[] { "approve_review", "reject_review", "request_changes" })
        {
            var res = await Act(owner, orgId, id, action, reasonCode: "Incomplete", notes: "n");
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
        Assert.Equal(EventStatus.UnderReview, await StatusAsync(id));
    }

    [Fact]
    public async Task A_stranger_gets_no_access_to_the_workflow()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000011", "Stranger Org");
        var (stranger, _) = await LoginAsync("9701000012");
        var id = await CreateEventAsync(owner, orgId);

        var res = await Act(stranger, orgId, id, "submit_for_review");
        Assert.True(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"expected 403/404 for an unrelated user, got {res.StatusCode}");
    }

    // ── Edit lock ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Editing_is_locked_while_with_a_reviewer_and_open_otherwise()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000020", "EditLock Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        async Task<HttpStatusCode> Patch() =>
            (await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}", new { subtitle = "s" })).StatusCode;

        Assert.Equal(HttpStatusCode.OK, await Patch());                 // Draft — editable

        await Act(owner, orgId, id, "submit_for_review");
        Assert.Equal(HttpStatusCode.Conflict, await Patch());           // PendingReview — 409

        await Act(reviewer, orgId, id, "claim_review");
        Assert.Equal(HttpStatusCode.Conflict, await Patch());           // UnderReview — 409

        await Act(reviewer, orgId, id, "request_changes", notes: "fix");
        Assert.Equal(HttpStatusCode.OK, await Patch());                 // ChangesRequested — editing is the point
    }

    // ── VerificationReview persistence ───────────────────────────────────────────────────────

    [Fact]
    public async Task Only_decisions_are_recorded_and_they_carry_their_evidence()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000030", "Persist Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");
        Assert.Empty(await ReviewsAsync(id));            // claim is queue mechanics, not a verdict

        await Act(reviewer, orgId, id, "release_review");
        Assert.Empty(await ReviewsAsync(id));            // release likewise

        await Act(reviewer, orgId, id, "claim_review");
        await Act(reviewer, orgId, id, "request_changes", notes: "Add the agenda.");
        var afterChanges = await ReviewsAsync(id);
        Assert.Single(afterChanges);
        Assert.Equal(VerificationDecision.RequestChanges, afterChanges[0].Decision);
        Assert.Equal("Add the agenda.", afterChanges[0].Notes);
        Assert.NotNull(afterChanges[0].ReviewerId);
        Assert.NotEqual(default, afterChanges[0].CreatedAt);

        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");
        await Act(reviewer, orgId, id, "reject_review", reasonCode: "Duplicate", notes: "Same as #4.");

        var all = await ReviewsAsync(id);
        Assert.Equal(2, all.Count);
        Assert.Equal(VerificationDecision.Reject, all[0].Decision);     // newest first
        Assert.Equal("Duplicate", all[0].ReasonCode);
    }

    [Fact]
    public async Task Approval_records_exactly_one_decision()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000031", "Approve Persist Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");
        await CompleteChecklistAsync(reviewer, id);   // D-266 M7 — approve is gated on it
        await Act(reviewer, orgId, id, "approve_review");

        var rows = await ReviewsAsync(id);
        Assert.Single(rows);
        Assert.Equal(VerificationDecision.Approve, rows[0].Decision);
        Assert.Null(rows[0].ReasonCode);   // approval needs no justification
    }

    // ── Invalid transitions ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invalid_and_duplicate_transitions_are_refused()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000040", "Invalid Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        // Cannot decide before submission, cannot publish a draft through the reviewed door.
        Assert.NotEqual(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "approve_review")).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await Act(owner, orgId, id, "publish_approved")).StatusCode);

        await Act(owner, orgId, id, "submit_for_review");
        // Cannot submit twice.
        Assert.NotEqual(HttpStatusCode.OK, (await Act(owner, orgId, id, "submit_for_review")).StatusCode);

        await Act(reviewer, orgId, id, "claim_review");
        // Cannot claim an already-claimed item.
        Assert.NotEqual(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "claim_review")).StatusCode);

        await CompleteChecklistAsync(reviewer, id);   // D-266 M7 — approve is gated on it
        await Act(reviewer, orgId, id, "approve_review");
        // Cannot approve twice.
        Assert.NotEqual(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "approve_review")).StatusCode);
        Assert.Equal(EventStatus.Approved, await StatusAsync(id));
    }

    [Fact]
    public async Task A_rejected_event_cannot_publish()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000041", "RejectPublish Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");
        await Act(reviewer, orgId, id, "reject_review", reasonCode: "ProhibitedContent");

        Assert.NotEqual(HttpStatusCode.OK, (await Act(owner, orgId, id, "publish_approved")).StatusCode);
        Assert.Equal(EventStatus.Rejected, await StatusAsync(id));
    }

    // ── Publish gate consumes the policy engine ──────────────────────────────────────

    /// <summary>D-266 M4 Phase 4 — the publish gate must consume PolicyResolver.PublishBlockers, not a
    /// second validation. Asserted through the API: whatever /policy-requirements reports as a blocker is
    /// what the transition actually refuses with. If these two ever disagree, a reviewer is reading a
    /// checklist that does not match the rule being enforced.</summary>
    [Fact]
    public async Task The_publish_gate_and_the_policy_endpoint_agree()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000060", "PublishGate Org");
        var id = await CreateEventAsync(owner, orgId);

        var req = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{id}/policy-requirements"));
        var blockers = req.GetProperty("publish_blockers").EnumerateArray()
            .Select(b => b.GetString()!).ToList();

        var res = await Act(owner, orgId, id, "schedule");
        if (blockers.Count > 0)
        {
            // The transition must refuse, and with a blocker the endpoint already reported.
            Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadAsStringAsync();
            Assert.True(blockers.Any(b => body.Contains(b, StringComparison.Ordinal)),
                $"gate refused with a reason absent from publish_blockers [{string.Join(", ", blockers)}]: {body}");
        }
        else
        {
            // No policy blocker means policy did not stop it; any refusal must come from a different gate
            // (readiness, approval chain) and never cite a policy violation.
            Assert.DoesNotContain("private_product", await res.Content.ReadAsStringAsync());
        }
    }

    // ── Admin review API contracts ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Review_history_is_newest_first_and_shaped_as_documented()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000050", "History Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");
        await Act(reviewer, orgId, id, "request_changes", notes: "first");
        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");
        await CompleteChecklistAsync(reviewer, id);   // D-266 M7 — approve is gated on it
        await Act(reviewer, orgId, id, "approve_review");

        var body = await Json(await reviewer.GetAsync($"/v1/admin/events/{id}/review-history"));
        var items = body.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);

        foreach (var it in items)
            foreach (var f in new[] { "id", "decision", "reviewer_id", "reviewer_name", "reason_code", "notes", "created_at" })
                Assert.True(it.TryGetProperty(f, out _), $"review-history item is missing '{f}'");

        // Newest first: the approval came last.
        Assert.Equal("Approve", items[0].GetProperty("decision").GetString());
    }

    [Fact]
    public async Task Review_counts_match_the_queue_and_expose_the_legacy_bucket()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000051", "Counts Org");
        var reviewer = await _factory.ReviewerClientAsync();

        var before = await Json(await reviewer.GetAsync("/v1/admin/events/review-counts"));
        var pendingBefore = before.GetProperty("pending_review").GetInt32();

        var id = await CreateEventAsync(owner, orgId);
        await Act(owner, orgId, id, "submit_for_review");

        var after = await Json(await reviewer.GetAsync("/v1/admin/events/review-counts"));
        Assert.Equal(pendingBefore + 1, after.GetProperty("pending_review").GetInt32());

        foreach (var f in new[] { "pending_review", "under_review", "changes_requested",
                                  "approved", "rejected", "legacy_in_review" })
            Assert.True(after.TryGetProperty(f, out _), $"review-counts is missing '{f}'");
    }

    [Fact]
    public async Task The_admin_queue_surfaces_events_awaiting_review()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000052", "Queue Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);
        await Act(owner, orgId, id, "submit_for_review");

        var body = await Json(await reviewer.GetAsync("/v1/admin/events/pending?limit=200"));
        Assert.Contains(body.EnumerateArray(), e => e.GetProperty("event_id").GetGuid() == id);
    }

    /// <summary>D-058 requires a dashboard tile to agree with the queue it links to, and
    /// `pending_events` links to /events/pending. The M4 split broke that: the tile counted only
    /// PendingReview, so every event a reviewer had claimed vanished from the number while staying in
    /// the queue. Claiming is the case that regressed, so this asserts across a claim.</summary>
    [Fact]
    public async Task Dashboard_pending_events_agrees_with_the_review_queue_after_a_claim()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000053", "Dashboard Tile Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);
        await Act(owner, orgId, id, "submit_for_review");
        await Act(reviewer, orgId, id, "claim_review");
        Assert.Equal(EventStatus.UnderReview, await StatusAsync(id));

        var queued = (await Json(await reviewer.GetAsync("/v1/admin/events/pending?limit=200")))
            .EnumerateArray().Count();
        var tile = (await Json(await reviewer.GetAsync("/v1/admin/dashboard/summary")))
            .GetProperty("pending_events").GetInt32();

        Assert.Equal(queued, tile);
    }

    // ── Concurrency · the transition compare-and-swap ────────────────────────────────────────

    /// <summary>Two reviewers deciding in the same moment both pass the gates on the same snapshot. Without
    /// the claim, both write: the status is whoever committed last while BOTH verdicts are recorded, and the
    /// review history then says one event was approved twice.</summary>
    [Fact]
    public async Task Two_simultaneous_approvals_record_exactly_one_decision()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000090", "Race Approve Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_review");
        await Act(reviewer, orgId, id, "claim_review");
        await CompleteChecklistAsync(reviewer, id);

        var results = await Task.WhenAll(
            Act(reviewer, orgId, id, "approve_review"),
            Act(reviewer, orgId, id, "approve_review"));

        Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(EventStatus.Approved, await StatusAsync(id));
        Assert.Single(await ReviewsAsync(id));
    }

    /// <summary>The worst shape of the race: an approval and a rejection landing together. One must lose,
    /// or the event carries two contradictory verdicts and a status that matches only one of them.</summary>
    [Fact]
    public async Task An_approval_and_a_rejection_cannot_both_land()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000091", "Race Verdict Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_review");
        await Act(reviewer, orgId, id, "claim_review");
        await CompleteChecklistAsync(reviewer, id);

        var results = await Task.WhenAll(
            Act(reviewer, orgId, id, "approve_review"),
            Act(reviewer, orgId, id, "reject_review", reasonCode: nameof(EventReviewReason.ProhibitedContent)));

        Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.OK));

        var reviews = await ReviewsAsync(id);
        Assert.Single(reviews);

        // The recorded verdict and the event's status must agree — that is the whole point.
        var status = await StatusAsync(id);
        Assert.Equal(reviews[0].Decision == VerificationDecision.Approve
            ? EventStatus.Approved : EventStatus.Rejected, status);
    }

    /// <summary>Claiming is how the queue hands an item to one reviewer. Two claims landing together must
    /// not both succeed, or two people work the same item without either knowing.</summary>
    [Fact]
    public async Task Two_simultaneous_claims_leave_one_winner()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000092", "Race Claim Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_review");

        var results = await Task.WhenAll(
            Act(reviewer, orgId, id, "claim_review"),
            Act(reviewer, orgId, id, "claim_review"));

        Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(EventStatus.UnderReview, await StatusAsync(id));
    }

    /// <summary>A claim names its owner. Before this the queue said an item was UnderReview but never by
    /// whom, so nothing could tell a second reviewer it was already someone's work.</summary>
    [Fact]
    public async Task Claiming_records_the_holder_and_the_queue_shows_them()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000094", "Claim Owner Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_review");
        Assert.Equal(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "claim_review")).StatusCode);

        var queue = await Json(await reviewer.GetAsync("/v1/admin/events/pending?limit=200"));
        var row = queue.EnumerateArray().First(e => e.GetProperty("event_id").GetGuid() == id);
        var holderId = row.GetProperty("review_claimed_by").GetGuid();
        Assert.NotEqual(Guid.Empty, holderId);

        // The holder resolves to a real account — the load-bearing fact. The NAME is deliberately not
        // asserted: an account created through OTP has none until onboarding, so requiring one here would
        // test the fixture rather than the feature.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            Assert.True(await db.Users.AsNoTracking().AnyAsync(u => u.Id == holderId));
        }

        // Releasing hands it back to the queue and clears the owner — an unclaimed item owned by nobody.
        Assert.Equal(HttpStatusCode.OK, (await Act(reviewer, orgId, id, "release_review")).StatusCode);
        queue = await Json(await reviewer.GetAsync("/v1/admin/events/pending?limit=200"));
        row = queue.EnumerateArray().First(e => e.GetProperty("event_id").GetGuid() == id);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("review_claimed_by").ValueKind);
    }

    /// <summary>Deciding on someone else's claim is how two reviewers end up working one event: the
    /// checklist is per-reviewer, so the second signs off against ticks they never made.</summary>
    [Fact]
    public async Task A_second_reviewer_cannot_decide_an_item_another_holds()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000095", "Two Reviewer Org");
        var holder = await _factory.ReviewerClientAsync();
        var intruder = await _factory.SecondReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_review");
        Assert.Equal(HttpStatusCode.OK, (await Act(holder, orgId, id, "claim_review")).StatusCode);

        await CompleteChecklistAsync(intruder, id);
        var stolen = await Act(intruder, orgId, id, "approve_review");
        Assert.NotEqual(HttpStatusCode.OK, stolen.StatusCode);
        Assert.Contains("claimed_by_another_reviewer", await stolen.Content.ReadAsStringAsync());
        Assert.Equal(EventStatus.UnderReview, await StatusAsync(id));

        // The holder still can.
        await CompleteChecklistAsync(holder, id);
        Assert.Equal(HttpStatusCode.OK, (await Act(holder, orgId, id, "approve_review")).StatusCode);
        Assert.Equal(EventStatus.Approved, await StatusAsync(id));
    }

    /// <summary>A decided event is nobody's work in progress — the verdict lives in verification_reviews,
    /// so the hold must not outlive it and leave the item looking occupied forever.</summary>
    [Fact]
    public async Task Deciding_releases_the_hold()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000096", "Hold Release Org");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_review");
        await Act(reviewer, orgId, id, "claim_review");
        await CompleteChecklistAsync(reviewer, id);
        await Act(reviewer, orgId, id, "approve_review");

        using var scope = _factory.Services.CreateScope();
        var held = await scope.ServiceProvider.GetRequiredService<KurxDbContext>()
            .Events.AsNoTracking().Where(e => e.Id == id)
            .Select(e => new { e.ReviewClaimedBy, e.ReviewClaimedAt }).FirstAsync();
        Assert.Null(held.ReviewClaimedBy);
        Assert.Null(held.ReviewClaimedAt);
    }

    /// <summary>The legacy `publish`/`reject` are valid from <c>UnderReview</c> and are what the admin bulk
    /// path posts — so they end a review just as surely as <c>approve_review</c> does, and must honour the
    /// same hold. Scoped to the review state: publishing one's own Draft is untouched.</summary>
    [Fact]
    public async Task Legacy_publish_cannot_end_a_review_another_reviewer_holds()
    {
        var (owner, orgId) = await LoginOrgAsync("9701000097", "Legacy Hold Org");
        var holder = await _factory.ReviewerClientAsync();
        var intruder = await _factory.SecondReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Act(owner, orgId, id, "submit_review");
        Assert.Equal(HttpStatusCode.OK, (await Act(holder, orgId, id, "claim_review")).StatusCode);

        foreach (var legacy in new[] { "publish", "reject" })
        {
            var res = await Act(intruder, orgId, id, legacy);
            Assert.Contains("claimed_by_another_reviewer", await res.Content.ReadAsStringAsync());
        }
        Assert.Equal(EventStatus.UnderReview, await StatusAsync(id));

        // The holder is unaffected.
        Assert.Equal(HttpStatusCode.OK, (await Act(holder, orgId, id, "reject")).StatusCode);
        Assert.Equal(EventStatus.Draft, await StatusAsync(id));
    }
}
