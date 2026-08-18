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

/// <summary>D-191: Admin Event Management production-completion sprint — moderation audit coverage, bulk
/// operations, CSV export, admin pagination/audit-filter/reviewer-access gaps, the tightened UpdateAsync
/// policy, and Super Admin Emergency Edit. Real HTTP / real kurx_test.</summary>
public class AdminEventManagementTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public AdminEventManagementTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "AEM Cat", Slug = "aem-cat" };
            db.EventCategories.Add(category);
            db.SaveChanges();
            _categoryId = category.Id;
            _reset = true;
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<HttpClient> ReviewerAsync(string phone)
    {
        var (client, userId) = await LoginAsync(phone);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.VerificationReviewer, grantedBy: null);
        return client;
    }

    private async Task<HttpClient> SuperAdminAsync(string phone)
    {
        var (client, userId) = await LoginAsync(phone);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.SuperAdmin, grantedBy: null);
        return client;
    }

    private async Task<(HttpClient Owner, Guid OrgId, Guid EventId)> PublishedEventAsync(string ownerPhone, string title)
    {
        var (owner, _) = await LoginAsync(ownerPhone);
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "AEM Org " + Guid.NewGuid().ToString("N")[..6]);
        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title, description = "An AEM test event.", categoryId = _categoryId, venueName = "Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }))).GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event

        /*
         * D-377 — this helper used to submit for review and then publish as the OWNER, which worked only
         * because the reviewer gate was misplaced. A fixture named `PublishedEventAsync` was quietly
         * exercising the bypass, and the one test in this class that needs the event to actually be past
         * review (`Content_edit_requires_a_real_org_role_not_the_admin_claim`, which the edit lock 409s in
         * PendingReview) is what surfaced it.
         *
         * The real path, and the one the corrected rule describes: the reviewer decides, then the OWNER
         * publishes their approved event.
         */
        var reviewer = await ReviewerAsync("99600001" + ownerPhone[^2..]);
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "submit_for_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "claim_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "approve_review" });
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish_approved" });
        return (owner, orgId, eventId);
    }

    private async Task<AuditLog> LastAuditAsync(Guid entityId, string action)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.AuditLogs.Where(a => a.EntityId == entityId && a.Action == action)
            .OrderByDescending(a => a.CreatedAt).FirstAsync();
    }

    // ── Moderation actions mutate + audit ──────────────────────────────────────────
    [Fact]
    public async Task Suspend_and_unsuspend_mutate_the_row_and_write_audit()
    {
        var reviewer = await ReviewerAsync("9960000001");
        var (_, _, eventId) = await PublishedEventAsync("9960000002", "Suspend Test");

        var suspended = await Json(await reviewer.PostAsJsonAsync($"/v1/admin/events/{eventId}/suspend", new { reason = "policy check" }));
        Assert.True(suspended.GetProperty("is_suspended").GetBoolean());
        var auditRow = await LastAuditAsync(eventId, "admin.event.suspend");
        Assert.NotNull(auditRow);

        var unsuspended = await Json(await reviewer.PostAsJsonAsync($"/v1/admin/events/{eventId}/unsuspend", new { }));
        Assert.False(unsuspended.GetProperty("is_suspended").GetBoolean());
        Assert.NotNull(await LastAuditAsync(eventId, "admin.event.unsuspend"));
    }

    [Fact]
    public async Task Hide_and_unhide_mutate_the_row_and_write_audit()
    {
        var reviewer = await ReviewerAsync("9960000003");
        var (_, _, eventId) = await PublishedEventAsync("9960000004", "Hide Test");

        var hidden = await Json(await reviewer.PostAsJsonAsync($"/v1/admin/events/{eventId}/hide", new { reason = "review" }));
        Assert.True(hidden.GetProperty("is_hidden").GetBoolean());
        Assert.NotNull(await LastAuditAsync(eventId, "admin.event.hide"));

        var unhidden = await Json(await reviewer.PostAsJsonAsync($"/v1/admin/events/{eventId}/unhide", new { }));
        Assert.False(unhidden.GetProperty("is_hidden").GetBoolean());
        Assert.NotNull(await LastAuditAsync(eventId, "admin.event.unhide"));
    }

    [Fact]
    public async Task Message_and_warn_organizer_write_audit_with_the_message_body()
    {
        var reviewer = await ReviewerAsync("9960000005");
        var (_, _, eventId) = await PublishedEventAsync("9960000006", "Message Test");

        Assert.Equal(HttpStatusCode.OK,
            (await reviewer.PostAsJsonAsync($"/v1/admin/events/{eventId}/message", new { message = "Please update your banner." })).StatusCode);
        var messageAudit = await LastAuditAsync(eventId, "admin.event.message");
        Assert.Contains("Please update your banner.", messageAudit.DetailsJson);

        Assert.Equal(HttpStatusCode.OK,
            (await reviewer.PostAsJsonAsync($"/v1/admin/events/{eventId}/warn", new { message = "Policy violation noted." })).StatusCode);
        var warnAudit = await LastAuditAsync(eventId, "admin.event.warning");
        Assert.Contains("Policy violation noted.", warnAudit.DetailsJson);
    }

    // ── Bulk operations ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Bulk_suspend_processes_a_mixed_batch_and_reports_partial_failure()
    {
        var reviewer = await ReviewerAsync("9960000007");
        var (_, _, eventA) = await PublishedEventAsync("9960000008", "Bulk A");
        var (_, _, eventB) = await PublishedEventAsync("9960000009", "Bulk B");
        var missingId = Guid.NewGuid();

        var res = await reviewer.PostAsJsonAsync("/v1/admin/events/bulk",
            new { eventIds = new[] { eventA, eventB, missingId }, action = "suspend", reason = "bulk sweep" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await Json(res);
        var succeeded = body.GetProperty("succeeded").EnumerateArray().Select(e => e.GetGuid()).ToList();
        var failed = body.GetProperty("failed").EnumerateArray().ToList();
        Assert.Contains(eventA, succeeded);
        Assert.Contains(eventB, succeeded);
        Assert.Single(failed);
        Assert.Equal(missingId, failed[0].GetProperty("event_id").GetGuid());

        // Bulk goes through SuspendAsync (SetModerationFlagAsync) for each — the same audit write the
        // single-event route produces, not a separate bulk-only audit path.
        Assert.NotNull(await LastAuditAsync(eventA, "admin.event.suspend"));
        Assert.NotNull(await LastAuditAsync(eventB, "admin.event.suspend"));

        var check = await Json(await reviewer.GetAsync($"/v1/admin/events?q=Bulk"));
        var rows = check.GetProperty("items").EnumerateArray().ToList();
        Assert.All(rows, r => Assert.True(r.GetProperty("is_suspended").GetBoolean()));
    }

    [Fact]
    public async Task Bulk_endpoint_rejects_an_unknown_action_and_an_oversized_batch()
    {
        var reviewer = await ReviewerAsync("9960000010");
        var (_, _, eventId) = await PublishedEventAsync("9960000011", "Bulk Bad Action");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await reviewer.PostAsJsonAsync("/v1/admin/events/bulk", new { eventIds = new[] { eventId }, action = "delete_everything" })).StatusCode);

        var tooMany = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await reviewer.PostAsJsonAsync("/v1/admin/events/bulk", new { eventIds = tooMany, action = "suspend" })).StatusCode);
    }

    // ── CSV export ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CSV_export_returns_the_filtered_rows_as_csv()
    {
        var reviewer = await ReviewerAsync("9960000012");
        var (_, _, eventId) = await PublishedEventAsync("9960000013", "CSV Export Unique Title");

        var res = await reviewer.GetAsync("/v1/admin/events/export?q=CSV Export Unique Title");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv", res.Content.Headers.ContentType?.MediaType);
        var csv = await res.Content.ReadAsStringAsync();
        Assert.Contains("EventId,Title,Status", csv);
        Assert.Contains(eventId.ToString(), csv);
        Assert.Contains("CSV Export Unique Title", csv);
    }

    // ── Sorting ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Sort_by_revenue_orders_events_highest_first()
    {
        var reviewer = await ReviewerAsync("9960000030");
        var token = "SortRev" + Guid.NewGuid().ToString("N")[..6];
        var (_, orgLow, eventLow) = await PublishedEventAsync("9960000031", $"{token} Low");
        var (_, orgHigh, eventHigh) = await PublishedEventAsync("9960000032", $"{token} High");
        await SeedRevenueAsync(eventLow, orgLow, 10000);
        await SeedRevenueAsync(eventHigh, orgHigh, 500000);

        var res = await Json(await reviewer.GetAsync($"/v1/admin/events?q={token}&sort=revenue"));
        var ids = res.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("event_id").GetGuid()).ToList();
        Assert.Equal(new[] { eventHigh, eventLow }, ids);
    }

    private async Task SeedRevenueAsync(Guid eventId, Guid orgId, long amountPaise)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var tt = new TicketType { EventId = eventId, Name = "GA", PricePaise = amountPaise, Quantity = 10, SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(10) };
        db.TicketTypes.Add(tt);
        var order = new Order { EventId = eventId, TicketTypeId = tt.Id, Status = OrderStatus.Paid, AmountPaise = amountPaise };
        db.Orders.Add(order);
        var item = new OrderItem { OrderId = order.Id, TicketTypeId = tt.Id, Qty = 1, UnitPricePaise = amountPaise };
        db.OrderItems.Add(item);
        db.ValueAllocationRecords.Add(new ValueAllocationRecord { OrderId = order.Id, OrderItemId = item.Id, EventId = eventId, AllocatedPaise = amountPaise });
        await db.SaveChangesAsync();
    }

    // ── Filtering survives pagination ────────────────────────────────────────────────
    [Fact]
    public async Task City_filter_narrows_results_across_every_page()
    {
        var reviewer = await ReviewerAsync("9960000033");
        var token = "CityPage" + Guid.NewGuid().ToString("N")[..6];
        var matchIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var (owner, orgId, eventId) = await PublishedEventAsync($"99600000{34 + i}", $"{token} Match {i}");
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var ev = await db.Events.FirstAsync(e => e.Id == eventId);
                ev.City = "FilterCity";
                await db.SaveChangesAsync();
            }
            matchIds.Add(eventId);
        }
        // A non-matching event with the same search token, different city — must never appear on any page.
        var (_, _, otherCity) = await PublishedEventAsync("9960000037", $"{token} Other");

        var seen = new List<Guid>();
        for (var page = 1; page <= 2; page++)
        {
            var res = await Json(await reviewer.GetAsync($"/v1/admin/events?q={token}&city=FilterCity&limit=2&page={page}"));
            seen.AddRange(res.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("event_id").GetGuid()));
        }
        Assert.Equal(matchIds.OrderBy(x => x), seen.OrderBy(x => x));
        Assert.DoesNotContain(otherCity, seen);
    }

    // ── Audit entityId filter ────────────────────────────────────────────────────────
    [Fact]
    public async Task Audit_entityId_filter_returns_only_that_events_rows()
    {
        var reviewer = await ReviewerAsync("9960000014");
        // GET /v1/admin/audit is gated by the "Audit" policy (SuperAdmin/ReadOnlyAuditor) — a distinct
        // policy from the "VerificationReviewer" one moderation routes use, so this needs its own persona.
        var auditor = await SuperAdminAsync("9960000029");
        var (_, _, eventA) = await PublishedEventAsync("9960000015", "Audit Filter A");
        var (_, _, eventB) = await PublishedEventAsync("9960000016", "Audit Filter B");
        await reviewer.PostAsJsonAsync($"/v1/admin/events/{eventA}/suspend", new { reason = "a" });
        await reviewer.PostAsJsonAsync($"/v1/admin/events/{eventB}/suspend", new { reason = "b" });

        var rows = (await Json(await auditor.GetAsync($"/v1/admin/audit?entity=events&entityId={eventA}"))).EnumerateArray().ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(eventA, r.GetProperty("entity_id").GetGuid()));
    }

    // ── Pagination ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Admin_events_pagination_returns_distinct_non_overlapping_pages()
    {
        var reviewer = await ReviewerAsync("9960000017");
        for (var i = 0; i < 3; i++) await PublishedEventAsync($"99600000{18 + i}", $"Page Test {Guid.NewGuid():N}"[..20]);

        var page1 = await Json(await reviewer.GetAsync("/v1/admin/events?limit=2&page=1"));
        var page2 = await Json(await reviewer.GetAsync("/v1/admin/events?limit=2&page=2"));
        var ids1 = page1.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("event_id").GetGuid()).ToHashSet();
        var ids2 = page2.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("event_id").GetGuid()).ToHashSet();
        Assert.Empty(ids1.Intersect(ids2));
    }

    // ── Reviewer detail-view access ──────────────────────────────────────────────────
    [Fact]
    public async Task Reviewer_without_org_membership_can_view_event_detail()
    {
        var reviewer = await ReviewerAsync("9960000021");
        var (owner, orgId, eventId) = await PublishedEventAsync("9960000022", "Reviewer Detail Test");

        var res = await reviewer.GetAsync($"/v1/orgs/{orgId}/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ── D-191: UpdateAsync policy tightened ──────────────────────────────────────────
    [Fact]
    public async Task Content_edit_requires_a_real_org_role_not_the_admin_claim()
    {
        var superAdmin = await SuperAdminAsync("9960000023");   // kurx_admin claim — the bypass this test targets
        var (owner, orgId, eventId) = await PublishedEventAsync("9960000024", "Policy Test");

        // The organizer's own token still edits fine.
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { title = "Owner Edited Title" })).StatusCode);

        // D-191: kurx_admin/SuperAdmin no longer bypasses content edit — moderation access was never
        // content-edit access. (Emergency Edit, tested separately, is the real Super Admin path now.)
        Assert.Equal(HttpStatusCode.Forbidden,
            (await superAdmin.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { title = "Admin Edited Title" })).StatusCode);
    }

    // ── D-191: Emergency Edit ─────────────────────────────────────────────────────────
    [Fact]
    public async Task Emergency_edit_requires_super_admin_and_a_reason_and_writes_a_before_after_snapshot()
    {
        var superAdmin = await SuperAdminAsync("9960000025");
        var reviewer = await ReviewerAsync("9960000026");   // staff, but not SuperAdmin
        var (_, orgId, eventId) = await PublishedEventAsync("9960000027", "Emergency Edit Original");

        // Missing reason is refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await superAdmin.PostAsJsonAsync(
            $"/v1/admin/events/{eventId}/emergency-edit", new { reason = "", input = new { title = "x" } })).StatusCode);

        // A non-SuperAdmin staff token is forbidden by the route policy.
        var reviewerAttempt = await reviewer.PostAsJsonAsync(
            $"/v1/admin/events/{eventId}/emergency-edit", new { reason = "trying anyway", input = new { title = "x" } });
        Assert.Equal(HttpStatusCode.Forbidden, reviewerAttempt.StatusCode);

        // SuperAdmin + reason succeeds and leaves a full before/after snapshot.
        var res = await superAdmin.PostAsJsonAsync($"/v1/admin/events/{eventId}/emergency-edit",
            new { reason = "Legal takedown request #4471", input = new { title = "Emergency Edit Applied" } });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = await Json(res);
        Assert.Equal("Emergency Edit Applied", updated.GetProperty("title").GetString());

        var auditRow = await LastAuditAsync(eventId, "admin.event.emergency_edit");
        Assert.Contains("Legal takedown request #4471", auditRow.DetailsJson);
        Assert.Contains("Emergency Edit Original", auditRow.DetailsJson);   // Before snapshot
        Assert.Contains("Emergency Edit Applied", auditRow.DetailsJson);    // After snapshot
    }

    // ── The organization name is part of the admin event contract ──────────────────
    //
    // `AdminEventResponse` was written without an `OrgName` member, and `ToAdminJson` filled the vacated
    // positional slot with a second copy of `RepresentingOrgId`. Both are Guids, so the constructor
    // type-checked and the whole pipeline stayed green: `AdminEventView.OrgName` was populated by the
    // query and then read by nobody. `org_name` simply stopped existing on the wire, and the admin
    // console — whose schema requires it as a non-nullable string — rejected every row and rendered its
    // error state instead of the events table.
    //
    // Asserted over HTTP against the real endpoint rather than by constructing the record: the bug lived
    // in the mapping, so a test that builds an `AdminEventResponse` itself would have passed throughout.
    // The moderation actions are covered in the same test because all seven routes share this one mapper,
    // which is the reason a single omission broke the entire surface at once.

    [Fact]
    public async Task Admin_event_list_and_every_moderation_action_carry_the_organization_name()
    {
        var reviewer = await ReviewerAsync("9960000030");
        var (_, orgId, eventId) = await PublishedEventAsync("9960000031", "Org Name Contract");

        string expected;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            expected = await db.Organizations.Where(o => o.Id == orgId).Select(o => o.Name).FirstAsync();
        }

        var list = await Json(await reviewer.GetAsync("/v1/admin/events?limit=100"));
        var row = list.GetProperty("items").EnumerateArray()
            .First(e => e.GetProperty("event_id").GetGuid() == eventId);
        Assert.True(row.TryGetProperty("org_name", out var listed), "the list row has no org_name at all");
        Assert.Equal(JsonValueKind.String, listed.ValueKind);   // not absent, not null
        Assert.Equal(expected, listed.GetString());

        // `org_id` stays the deprecated D-273a alias for the representation, beside the name rather than
        // instead of it — the pairing the console reads.
        Assert.Equal(orgId, row.GetProperty("representing_org_id").GetGuid());

        /*
         * D-381 — the creator travels BESIDE the organization, never instead of it.
         *
         * Guarded here rather than in a test of its own because this is the method that already knows
         * how this contract breaks: `AdminEventResponse` is a positional record, so a member added or
         * removed in the middle silently re-points every field after it, and the symptom is a correct-
         * looking payload carrying the wrong values. `creator_name` is the field the console prints
         * under "Creator", and the bug this decision fixed was printing a PERSON under "Representing
         * organization" — so the assertion that matters is that the two are distinct keys, both present.
         */
        Assert.True(row.TryGetProperty("creator_name", out var creator), "the list row has no creator_name");
        Assert.Equal(JsonValueKind.String, creator.ValueKind);
        Assert.NotEqual(Guid.Empty, row.GetProperty("creator_id").GetGuid());
        // A real organization, so the console must NOT offer the legacy state for it.
        Assert.False(row.GetProperty("org_is_personal").GetBoolean());
        Assert.Equal(orgId, row.GetProperty("org_id").GetGuid());

        // Paired so the event is returned to its original state; each response is the same DTO.
        (string Route, object Body)[] actions =
        [
            ("feature", new { }), ("unfeature", new { }),
            ("suspend", new { reason = "contract check" }), ("unsuspend", new { }),
            ("hide", new { reason = "contract check" }), ("unhide", new { }),
        ];
        foreach (var (route, body) in actions)
        {
            var res = await reviewer.PostAsJsonAsync($"/v1/admin/events/{eventId}/{route}", body);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var acted = await Json(res);
            Assert.True(acted.TryGetProperty("org_name", out var name), $"{route} response has no org_name");
            Assert.Equal(JsonValueKind.String, name.ValueKind);
            Assert.Equal(expected, name.GetString());
        }
    }

    /// <summary>A self-represented event resolves its name through the same join, because D-268's
    /// self-representation row is a real `organizations` row carrying the person's name. Pinned separately
    /// so that `IsPersonal` can never reintroduce the missing field for the one shape that has no
    /// institution behind it — the case an admin is most likely to meet and least likely to have tested.</summary>
    [Fact]
    public async Task A_self_represented_event_carries_the_name_of_its_representation_row()
    {
        var reviewer = await ReviewerAsync("9960000032");
        var (_, userId) = await LoginAsync("9960000033");

        var expected = "AEM Self Row " + Guid.NewGuid().ToString("N")[..6];
        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var personal = new Organization
            {
                Name = expected,
                Slug = "self-" + Guid.NewGuid().ToString("N")[..12],
                IsPersonal = true,
            };
            personal.CanonicalOrgId = personal.Id;
            db.Organizations.Add(personal);
            var ev = new Event
            {
                RepresentingOrgId = personal.Id,
                Title = "AEM Self Represented",
                Slug = "aem-self-" + Guid.NewGuid().ToString("N")[..8],
                ShortCode = Guid.NewGuid().ToString("N")[..8],
                CategoryId = _categoryId,
                Status = EventStatus.Published,
                CreatedBy = userId,
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            eventId = ev.Id;
        }

        var list = await Json(await reviewer.GetAsync("/v1/admin/events?limit=100"));
        var row = list.GetProperty("items").EnumerateArray()
            .First(e => e.GetProperty("event_id").GetGuid() == eventId);
        Assert.Equal(JsonValueKind.String, row.GetProperty("org_name").ValueKind);
        Assert.Equal(expected, row.GetProperty("org_name").GetString());
    }
}
