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

/// <summary>V3 §5 (Phase 6) — participants. The platform role registry, event participant assign/respond/
/// remove, the §5.4 permission union (org grant OR an event ORGANISER participant grant, event-scoped), and
/// the one-time backfill from V2 EventAssignment. The legacy EventAssignment surface is untouched.</summary>
public class ParticipantTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public ParticipantTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    // ── Registry ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Participant_role_registry_is_seeded_with_class_and_capacity_properties()
    {
        var client = _factory.CreateClient();
        var roles = (await Json(await client.GetAsync("/v1/participant-roles"))).EnumerateArray().ToList();
        Assert.True(roles.Count >= 30);

        var judge = roles.Single(r => r.GetProperty("slug").GetString() == "judge");
        Assert.Equal("Evaluation", judge.GetProperty("class").GetString());
        Assert.False(judge.GetProperty("counts_toward_capacity").GetBoolean());

        var attendee = roles.Single(r => r.GetProperty("slug").GetString() == "attendee");
        Assert.True(attendee.GetProperty("counts_toward_capacity").GetBoolean());
        Assert.Equal("general", attendee.GetProperty("inventory_segment").GetString());

        var vip = roles.Single(r => r.GetProperty("slug").GetString() == "vip");
        Assert.Equal("vip", vip.GetProperty("inventory_segment").GetString());   // §5.2 — VIP consumes the VIP segment
    }

    // ── Assign / respond / remove ───────────────────────────────────────────

    [Fact]
    public async Task Owner_assigns_a_person_who_accepts_then_is_removed()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003001", "Participant Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (subject, subjectId) = await LoginAsync("9700003002");

        var p = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer", phone = "9700003002" }));
        var participantId = p.GetProperty("id").GetGuid();
        Assert.Equal("Invited", p.GetProperty("state").GetString());
        Assert.Equal("Person", p.GetProperty("subject_type").GetString());

        var mine = (await Json(await subject.GetAsync("/v1/me/participations"))).EnumerateArray().ToList();
        Assert.Single(mine);

        var accepted = await Json(await subject.PostAsJsonAsync($"/v1/participants/{participantId}/respond", new { accept = true }));
        Assert.Equal("Active", accepted.GetProperty("state").GetString());

        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"/v1/orgs/{orgId}/events/{eventId}/participants/{participantId}")).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(ParticipantState.Removed, await db.EventParticipants.Where(x => x.Id == participantId).Select(x => x.State).SingleAsync());
    }

    [Fact]
    public async Task Assign_validation_rejects_bad_input()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003003", "Validate Participant Org");
        var eventId = await CreateEventAsync(owner, orgId);
        await LoginAsync("9700003004");   // ensure the phone resolves to a user for the valid case

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "wizard", phone = "9700003004" })).StatusCode);                         // invalid_role
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer", phone = "9700003004", orgUnitId = Guid.NewGuid() })).StatusCode); // both subjects
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer" })).StatusCode);                                             // no subject
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer", phone = "9700009998" })).StatusCode);                       // user_not_found
    }

    [Fact]
    public async Task Assign_is_idempotent_and_reactivates_after_removal()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003005", "Idempotent Participant Org");
        var eventId = await CreateEventAsync(owner, orgId);
        await LoginAsync("9700003006");

        var first = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "security", phone = "9700003006" }));
        var id1 = first.GetProperty("id").GetGuid();
        var second = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "security", phone = "9700003006" }));
        Assert.Equal(id1, second.GetProperty("id").GetGuid());   // idempotent — same row

        await owner.DeleteAsync($"/v1/orgs/{orgId}/events/{eventId}/participants/{id1}");
        var reactivated = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "security", phone = "9700003006" }));
        Assert.Equal(id1, reactivated.GetProperty("id").GetGuid());          // same row reactivated, not a duplicate
        Assert.Equal("Invited", reactivated.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Respond_is_restricted_to_the_subject_and_only_when_pending()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003007", "Respond Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (subject, _) = await LoginAsync("9700003008");
        var (other, _) = await LoginAsync("9700003009");

        var participantId = (await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "judge", phone = "9700003008" }))).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync($"/v1/participants/{participantId}/respond", new { accept = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await subject.PostAsJsonAsync($"/v1/participants/{participantId}/respond", new { accept = true })).StatusCode);
        // Already responded → not_pending.
        Assert.Equal(HttpStatusCode.BadRequest, (await subject.PostAsJsonAsync($"/v1/participants/{participantId}/respond", new { accept = false })).StatusCode);
    }

    // ── §5.4 permission union ───────────────────────────────────────────────

    [Fact]
    public async Task Non_manager_cannot_assign_but_an_active_coordinator_participant_can()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003010", "Grant Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (coordinator, _) = await LoginAsync("9700003011");
        var (stranger, _) = await LoginAsync("9700003012");
        await LoginAsync("9700003013");   // target volunteer

        // A stranger (no org grant, no participation) cannot manage participants.
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer", phone = "9700003013" })).StatusCode);

        // Owner appoints a Coordinator (ORGANISER class → participants:manage); they accept → Active.
        var coordId = (await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "coordinator", phone = "9700003011" }))).GetProperty("id").GetGuid();
        await coordinator.PostAsJsonAsync($"/v1/participants/{coordId}/respond", new { accept = true });

        // §5.4: the coordinator — not an org member — can now manage this event's participants.
        Assert.Equal(HttpStatusCode.OK, (await coordinator.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer", phone = "9700003013" })).StatusCode);
    }

    [Fact]
    public async Task An_active_attendee_participant_gets_no_management_grant()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003014", "Attendee Grant Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (attendee, _) = await LoginAsync("9700003015");
        await LoginAsync("9700003016");

        var pid = (await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "attendee", phone = "9700003015" }))).GetProperty("id").GetGuid();
        await attendee.PostAsJsonAsync($"/v1/participants/{pid}/respond", new { accept = true });

        // Attendee role has no participants:manage → forbidden.
        Assert.Equal(HttpStatusCode.Forbidden, (await attendee.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer", phone = "9700003016" })).StatusCode);
    }

    [Fact]
    public async Task Participant_grant_is_scoped_to_its_own_event()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003017", "Scope Org");
        var eventA = await CreateEventAsync(owner, orgId);
        var eventB = await CreateEventAsync(owner, orgId);
        var (coordinator, _) = await LoginAsync("9700003018");
        await LoginAsync("9700003019");

        var coordId = (await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventA}/participants",
            new { roleSlug = "coordinator", phone = "9700003018" }))).GetProperty("id").GetGuid();
        await coordinator.PostAsJsonAsync($"/v1/participants/{coordId}/respond", new { accept = true });

        Assert.Equal(HttpStatusCode.OK, (await coordinator.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventA}/participants",
            new { roleSlug = "volunteer", phone = "9700003019" })).StatusCode);         // grants on event A
        Assert.Equal(HttpStatusCode.Forbidden, (await coordinator.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventB}/participants",
            new { roleSlug = "volunteer", phone = "9700003019" })).StatusCode);         // …not event B (§5.4 event-scoped)
    }

    // ── OrgUnit subject + backfill ──────────────────────────────────────────

    [Fact]
    public async Task Org_unit_can_be_assigned_as_a_participant()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003020", "Unit Participant Org");
        var eventId = await CreateEventAsync(owner, orgId);   // materialises the org root unit

        Guid rootUnitId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            rootUnitId = await db.OrgUnits.Where(u => u.OrgId == orgId && u.ParentId == null).Select(u => u.Id).SingleAsync();
        }

        var p = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "sponsor", orgUnitId = rootUnitId }));
        Assert.Equal("OrgUnit", p.GetProperty("subject_type").GetString());
        Assert.Equal("Active", p.GetProperty("state").GetString());   // a unit doesn't accept
    }

    [Fact]
    public async Task Backfill_migrates_event_assignments_to_participants()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700003021", "Backfill Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, userId) = await LoginAsync("9700003022");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.EventAssignments.Add(new EventAssignment
            {
                EventId = eventId, OrgId = orgId, UserId = userId, InvitedBy = ownerId,
                Role = "Judge", Status = AssignmentStatus.Accepted, ShowOnProfile = true,
            });
            db.EventAssignments.Add(new EventAssignment
            {
                EventId = eventId, OrgId = orgId, UserId = userId, InvitedBy = ownerId,
                Role = "Custom", CustomRole = "Rangoli Judge", Status = AssignmentStatus.Invited,
            });
            await db.SaveChangesAsync();

            var migrated = await scope.ServiceProvider.GetRequiredService<IParticipantService>().BackfillFromAssignmentsAsync();
            Assert.True(migrated >= 2);

            var judge = await db.EventParticipants.SingleAsync(p => p.EventId == eventId && p.SubjectId == userId && p.RoleSlug == "judge");
            Assert.Equal(ParticipantState.Active, judge.State);                 // Accepted → Active
            var custom = await db.EventParticipants.SingleAsync(p => p.EventId == eventId && p.SubjectId == userId && p.RoleSlug == "staff");
            Assert.Equal("Rangoli Judge", custom.CustomLabel);                  // the free-text V2 role is preserved

            // Idempotent: a second run migrates nothing new.
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IParticipantService>().BackfillFromAssignmentsAsync());
        }
    }

    // ── Review fix: anti-amplification (§5.4) + lifecycle grant states ───────

    [Fact]
    public async Task Coordinator_cannot_assign_roles_more_powerful_than_their_own()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003030", "Amplify Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (coord, _) = await LoginAsync("9700003031");
        await AppointActiveAsync(owner, orgId, eventId, coord, "9700003031", "coordinator");
        await LoginAsync("9700003032");   // target

        // A coordinator holds participants:manage but NOT event:manage → cannot mint manager/owner (which confer it).
        Assert.Equal(HttpStatusCode.Forbidden, (await coord.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "manager", phone = "9700003032" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await coord.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "owner", phone = "9700003032" })).StatusCode);
        // …but a subordinate role that confers nothing is fine.
        Assert.Equal(HttpStatusCode.OK, (await coord.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer", phone = "9700003032" })).StatusCode);
    }

    [Fact]
    public async Task Coordinator_cannot_self_escalate()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003033", "SelfEsc Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (coord, _) = await LoginAsync("9700003034");
        await AppointActiveAsync(owner, orgId, eventId, coord, "9700003034", "coordinator");

        // Appointing themselves owner would confer event:manage they don't hold → forbidden.
        Assert.Equal(HttpStatusCode.Forbidden, (await coord.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "owner", phone = "9700003034" })).StatusCode);
    }

    [Fact]
    public async Task Owner_can_assign_organiser_roles()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003035", "Owner Assign Org");
        var eventId = await CreateEventAsync(owner, orgId);
        await LoginAsync("9700003036");
        await LoginAsync("9700003037");

        // The org owner holds event:manage → may appoint the management roles.
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "manager", phone = "9700003036" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "coordinator", phone = "9700003037" })).StatusCode);
    }

    [Fact]
    public async Task Removed_coordinator_loses_the_management_grant()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003038", "Revoke Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (coord, _) = await LoginAsync("9700003039");
        var coordId = await AppointActiveAsync(owner, orgId, eventId, coord, "9700003039", "coordinator");
        await LoginAsync("9700003040");

        Assert.Equal(HttpStatusCode.OK, (await coord.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer", phone = "9700003040" })).StatusCode);
        await owner.DeleteAsync($"/v1/orgs/{orgId}/events/{eventId}/participants/{coordId}");
        // The grant is revoked with the participation.
        Assert.Equal(HttpStatusCode.Forbidden, (await coord.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer", phone = "9700003040" })).StatusCode);
    }

    [Fact]
    public async Task Pending_or_declined_coordinator_has_no_grant()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003041", "Pending Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (pending, _) = await LoginAsync("9700003042");
        var (decliner, _) = await LoginAsync("9700003043");
        await LoginAsync("9700003044");   // target

        // Invited but not yet accepted → no grant.
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants", new { roleSlug = "coordinator", phone = "9700003042" });
        Assert.Equal(HttpStatusCode.Forbidden, (await pending.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer", phone = "9700003044" })).StatusCode);

        // Declined → still no grant.
        var pid = (await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "coordinator", phone = "9700003043" }))).GetProperty("id").GetGuid();
        await decliner.PostAsJsonAsync($"/v1/participants/{pid}/respond", new { accept = false });
        Assert.Equal(HttpStatusCode.Forbidden, (await decliner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug = "volunteer", phone = "9700003044" })).StatusCode);
    }

    [Fact]
    public async Task A_person_can_hold_multiple_roles_on_one_event()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700003045", "MultiRole Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, personId) = await LoginAsync("9700003046");

        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants", new { roleSlug = "volunteer", phone = "9700003046" });
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants", new { roleSlug = "judge", phone = "9700003046" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var roles = await db.EventParticipants.Where(p => p.EventId == eventId && p.SubjectId == personId).Select(p => p.RoleSlug).ToListAsync();
        Assert.Equal(2, roles.Count);
        Assert.Contains("volunteer", roles);
        Assert.Contains("judge", roles);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<Guid> AppointActiveAsync(HttpClient owner, Guid orgId, Guid eventId, HttpClient subject, string phone, string roleSlug)
    {
        var id = (await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/participants",
            new { roleSlug, phone }))).GetProperty("id").GetGuid();
        await subject.PostAsJsonAsync($"/v1/participants/{id}/respond", new { accept = true });
        return id;
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "P " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
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
