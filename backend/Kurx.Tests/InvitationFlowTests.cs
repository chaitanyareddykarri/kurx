using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-266 M6 (D9) — <c>Invite Only</c> as ONE registration policy with two delivery methods.
///
/// <para>A policy answers "who may register"; a method answers "how were they told". The tests below assert
/// that distinction holds: both methods produce the same outcome (permission to register), neither produces
/// a ticket, and neither bypasses payment.</para></summary>
public class InvitationFlowTests(KurxApiFactory factory) : IClassFixture<KurxApiFactory>
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

    private async Task<string> ClaimUsernameAsync(Guid userId, string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var u = await db.Users.FirstAsync(x => x.Id == userId);
        u.Username = username;
        await db.SaveChangesAsync();
        return username;
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId, bool inviteOnly = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == "hackathon")
            .Select(c => new { c.Id, c.ParentId }).FirstAsync();

        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Inv " + Guid.NewGuid().ToString("N")[..6], description = "a real description",
            categoryId = t.ParentId!.Value, typeId = t.Id, venueName = "Hall", city = "C",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(3),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var id = (await Json(res)).GetProperty("id").GetGuid();

        if (inviteOnly)
        {
            // Set directly: the wizard surface for the policy is M8's, and this suite is about what the
            // policy DOES once selected, not how it is chosen.
            using var s2 = _factory.Services.CreateScope();
            var db2 = s2.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ev = await db2.Events.FirstAsync(e => e.Id == id);
            ev.RegistrationPolicy = EventRegistrationPolicy.InviteOnly;
            await db2.SaveChangesAsync();
        }
        return id;
    }

    // ── Method A: invite by Kurx username ────────────────────────────────────────────────────

    [Fact]
    public async Task Inviting_by_username_needs_no_email_or_phone_and_notifies_the_invitee()
    {
        var (owner, _) = await LoginAsync("9704000001");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Method A Org");
        var id = await CreateEventAsync(owner, orgId);
        var (guest, guestId) = await LoginAsync("9704000002");
        await ClaimUsernameAsync(guestId, "chaitanya_a");

        // No email, no phone — the whole point of Method A.
        var res = await owner.PostAsJsonAsync($"/v1/events/{id}/invitations",
            new { name = (string?)null, email = (string?)null, phone = (string?)null, channel = "Email", username = "chaitanya_a" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var inbox = await Json(await guest.GetAsync("/v1/me/invitations"));
        Assert.Equal(1, inbox.GetArrayLength());
        Assert.Equal("None", inbox[0].GetProperty("rsvp_status").GetString());

        using var scope = _factory.Services.CreateScope();
        var notified = await scope.ServiceProvider.GetRequiredService<KurxDbContext>()
            .Notifications.AsNoTracking().AnyAsync(n => n.UserId == guestId && n.Kind == "invitation_received");
        Assert.True(notified, "Method A delivers in-app; the notification IS the delivery.");
    }

    [Fact]
    public async Task An_invitation_addressed_to_nobody_is_refused()
    {
        var (owner, _) = await LoginAsync("9704000003");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "No Target Org");
        var id = await CreateEventAsync(owner, orgId);

        var res = await owner.PostAsJsonAsync($"/v1/events/{id}/invitations",
            new { name = "Someone", email = (string?)null, phone = (string?)null, channel = "Email" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("invite_target_required", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Accepting_and_declining_are_recorded_and_the_organiser_is_told()
    {
        var (owner, ownerId) = await LoginAsync("9704000004");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Respond Org");
        var id = await CreateEventAsync(owner, orgId);
        var (guest, guestId) = await LoginAsync("9704000005");
        await ClaimUsernameAsync(guestId, "rahul_a");

        await owner.PostAsJsonAsync($"/v1/events/{id}/invitations",
            new { name = (string?)null, email = (string?)null, phone = (string?)null, channel = "Email", username = "rahul_a" });

        var inbox = await Json(await guest.GetAsync("/v1/me/invitations"));
        var invId = inbox[0].GetProperty("id").GetGuid();

        var accepted = await guest.PostAsJsonAsync($"/v1/invitations/{invId}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("Accepted", (await Json(accepted)).GetProperty("rsvp_status").GetString());

        // Settled means settled — a second response would let a guest flip the count after the organiser read it.
        var again = await guest.PostAsJsonAsync($"/v1/invitations/{invId}/decline", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("invitation_already_responded", await again.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var told = await scope.ServiceProvider.GetRequiredService<KurxDbContext>()
            .Notifications.AsNoTracking().AnyAsync(n => n.UserId == ownerId && n.Kind == "invitation_accepted");
        Assert.True(told);
    }

    /// <summary>404, never 403: confirming an invitation exists to someone who was not invited discloses the
    /// guest list one probe at a time (D-018).</summary>
    [Fact]
    public async Task A_stranger_cannot_respond_to_someone_elses_invitation()
    {
        var (owner, _) = await LoginAsync("9704000006");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Stranger Org");
        var id = await CreateEventAsync(owner, orgId);
        var (guest, guestId) = await LoginAsync("9704000007");
        await ClaimUsernameAsync(guestId, "sai_a");
        var (stranger, _) = await LoginAsync("9704000008");

        await owner.PostAsJsonAsync($"/v1/events/{id}/invitations",
            new { name = (string?)null, email = (string?)null, phone = (string?)null, channel = "Email", username = "sai_a" });
        var invId = (await Json(await guest.GetAsync("/v1/me/invitations")))[0].GetProperty("id").GetGuid();

        var res = await stranger.PostAsJsonAsync($"/v1/invitations/{invId}/accept", new { });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task The_same_user_cannot_be_invited_twice_to_one_event()
    {
        var (owner, _) = await LoginAsync("9704000009");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Dup Org");
        var id = await CreateEventAsync(owner, orgId);
        var (_, guestId) = await LoginAsync("9704000010");
        await ClaimUsernameAsync(guestId, "dup_a");

        var body = new { name = (string?)null, email = (string?)null, phone = (string?)null, channel = "Email", username = "dup_a" };
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"/v1/events/{id}/invitations", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/v1/events/{id}/invitations", body)).StatusCode);
    }

    // ── Method B: invite links ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_links_preflight_reports_usability_without_authentication()
    {
        var (owner, _) = await LoginAsync("9704000011");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Preflight Org");
        var id = await CreateEventAsync(owner, orgId);

        var token = (await Json(await owner.PostAsJsonAsync($"/v1/events/{id}/invite-links",
            new { maxSeats = 2, singleUse = false, expiresAt = (DateTime?)null, passcode = "hunter2" })))
            .GetProperty("token").GetString()!;

        var anon = _factory.CreateClient();
        var pre = await Json(await anon.GetAsync($"/v1/public/invite-links/{token}"));
        Assert.True(pre.GetProperty("is_usable").GetBoolean());
        Assert.True(pre.GetProperty("requires_passcode").GetBoolean());
        Assert.Equal(2, pre.GetProperty("seats_remaining").GetInt32());
    }

    [Fact]
    public async Task A_revoked_link_is_refused_and_says_why()
    {
        var (owner, _) = await LoginAsync("9704000012");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Revoke Org");
        var id = await CreateEventAsync(owner, orgId);

        var created = await Json(await owner.PostAsJsonAsync($"/v1/events/{id}/invite-links",
            new { maxSeats = (int?)null, singleUse = false, expiresAt = (DateTime?)null, passcode = (string?)null }));
        var linkId = created.GetProperty("id").GetGuid();
        var token = created.GetProperty("token").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"/v1/invite-links/{linkId}")).StatusCode);

        var (guest, _) = await LoginAsync("9704000013");
        var res = await guest.PostAsJsonAsync($"/v1/invite-links/{token}/redeem", new { passcode = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("invite_link_revoked", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_expired_link_is_refused()
    {
        var (owner, _) = await LoginAsync("9704000014");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Expiry Org");
        var id = await CreateEventAsync(owner, orgId);

        var token = (await Json(await owner.PostAsJsonAsync($"/v1/events/{id}/invite-links",
            new { maxSeats = (int?)null, singleUse = false, expiresAt = DateTime.UtcNow.AddMinutes(5), passcode = (string?)null })))
            .GetProperty("token").GetString()!;

        // Reach past the expiry rather than sleeping: the rule under test is the comparison, not the clock.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var link = await db.EventInviteLinks.FirstAsync(l => l.Token == token);
            link.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var (guest, _) = await LoginAsync("9704000015");
        var res = await guest.PostAsJsonAsync($"/v1/invite-links/{token}/redeem", new { passcode = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("invite_link_expired", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Creating_a_link_requires_manage_authority()
    {
        var (owner, _) = await LoginAsync("9704000016");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "LinkAuthz Org");
        var id = await CreateEventAsync(owner, orgId);
        var (stranger, _) = await LoginAsync("9704000017");

        var res = await stranger.PostAsJsonAsync($"/v1/events/{id}/invite-links",
            new { maxSeats = (int?)null, singleUse = false, expiresAt = (DateTime?)null, passcode = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>The passcode is a secret. It must never come back out of the API, in any projection.</summary>
    [Fact]
    public async Task A_passcode_is_never_returned_by_any_surface()
    {
        var (owner, _) = await LoginAsync("9704000018");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Secret Org");
        var id = await CreateEventAsync(owner, orgId);

        var created = await owner.PostAsJsonAsync($"/v1/events/{id}/invite-links",
            new { maxSeats = (int?)null, singleUse = false, expiresAt = (DateTime?)null, passcode = "s3cret-pass" });
        Assert.DoesNotContain("s3cret-pass", await created.Content.ReadAsStringAsync());

        var list = await (await owner.GetAsync($"/v1/events/{id}/invite-links")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("s3cret-pass", list);
        Assert.DoesNotContain("passcode_hash", list);

        var token = (await Json(created)).GetProperty("token").GetString()!;
        var anon = _factory.CreateClient();
        Assert.DoesNotContain("s3cret-pass",
            await (await anon.GetAsync($"/v1/public/invite-links/{token}")).Content.ReadAsStringAsync());
    }

    // ── The policy the two methods serve ─────────────────────────────────────────────────────

    /// <summary>D9 rule 1 — an Invite Only event refuses an uninvited registrant, and both methods let a
    /// person through. Asserted at the eligibility engine every ticket path routes through, so it cannot be
    /// satisfied on one path and missed on another.</summary>
    [Fact]
    public async Task Invite_only_admits_through_either_method_and_refuses_everyone_else()
    {
        var (owner, _) = await LoginAsync("9704000019");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Policy Org");
        var id = await CreateEventAsync(owner, orgId, inviteOnly: true);

        var (uninvited, _) = await LoginAsync("9704000020");
        var (viaUsername, viaUsernameId) = await LoginAsync("9704000021");
        await ClaimUsernameAsync(viaUsernameId, "guest_policy");
        var (viaLink, viaLinkId) = await LoginAsync("9704000022");

        // Method A — invited then accepted.
        await owner.PostAsJsonAsync($"/v1/events/{id}/invitations",
            new { name = (string?)null, email = (string?)null, phone = (string?)null, channel = "Email", username = "guest_policy" });
        var invId = (await Json(await viaUsername.GetAsync("/v1/me/invitations")))[0].GetProperty("id").GetGuid();
        await viaUsername.PostAsJsonAsync($"/v1/invitations/{invId}/accept", new { });

        // Method B — redeemed a link.
        var token = (await Json(await owner.PostAsJsonAsync($"/v1/events/{id}/invite-links",
            new { maxSeats = (int?)null, singleUse = false, expiresAt = (DateTime?)null, passcode = (string?)null })))
            .GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.OK,
            (await viaLink.PostAsJsonAsync($"/v1/invite-links/{token}/redeem", new { passcode = (string?)null })).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var audience = scope.ServiceProvider.GetRequiredService<Kurx.Application.Abstractions.IAudienceService>();

        Assert.True((await audience.EvaluateAsync(viaUsernameId, id)).Allowed);
        Assert.True((await audience.EvaluateAsync(viaLinkId, id)).Allowed);

        var denied = await audience.EvaluateAsync(
            (await Json(await uninvited.GetAsync("/v1/me"))).GetProperty("id").GetGuid(), id);
        Assert.False(denied.Allowed);
        Assert.Equal("not_invited", denied.Reason);

        // A guest has no identity to match a named guest list against.
        Assert.False((await audience.EvaluateAsync(null, id)).Allowed);
    }

    /// <summary>D9 — an invitation grants PERMISSION TO REGISTER, never a ticket and never a discount. If
    /// accepting ever issued something, the paid flow would have been bypassed.</summary>
    [Fact]
    public async Task Accepting_an_invitation_issues_no_ticket_and_no_order()
    {
        var (owner, _) = await LoginAsync("9704000023");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "NoTicket Org");
        var id = await CreateEventAsync(owner, orgId, inviteOnly: true);
        var (guest, guestId) = await LoginAsync("9704000024");
        await ClaimUsernameAsync(guestId, "noticket_a");

        await owner.PostAsJsonAsync($"/v1/events/{id}/invitations",
            new { name = (string?)null, email = (string?)null, phone = (string?)null, channel = "Email", username = "noticket_a" });
        var invId = (await Json(await guest.GetAsync("/v1/me/invitations")))[0].GetProperty("id").GetGuid();
        await guest.PostAsJsonAsync($"/v1/invitations/{invId}/accept", new { });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.False(await db.Orders.AsNoTracking().AnyAsync(o => o.EventId == id && o.UserId == guestId));
        Assert.False(await db.Tickets.AsNoTracking().AnyAsync(t => t.EventId == id && t.UserId == guestId));
    }

    [Fact]
    public async Task An_open_event_is_unaffected_by_the_invitation_gate()
    {
        var (owner, _) = await LoginAsync("9704000025");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Open Org");
        var id = await CreateEventAsync(owner, orgId);   // default policy: Open
        var (anyone, anyoneId) = await LoginAsync("9704000026");

        using var scope = _factory.Services.CreateScope();
        var audience = scope.ServiceProvider.GetRequiredService<Kurx.Application.Abstractions.IAudienceService>();
        Assert.True((await audience.EvaluateAsync(anyoneId, id)).Allowed);
        Assert.True((await audience.EvaluateAsync(null, id)).Allowed);
    }
}
