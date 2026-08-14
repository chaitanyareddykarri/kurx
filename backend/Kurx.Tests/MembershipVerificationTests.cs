using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Membership-affiliation verification (M6, D-045). A user's evidence-backed claim to represent
/// an org is reviewed independently of profile bio; approval marks their operational membership verified
/// (read-only Staff seat if none). Fast-track when the email domain matches the org. Real HTTP/kurx_test.</summary>
public class MembershipVerificationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public MembershipVerificationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

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

    // D-075: a membership claim targets an existing Verified institution — seed one the "owner" manages.
    private Task<Guid> CreateOrgAsync(HttpClient client, string name, string? domain = null)
        => Task.FromResult(_factory.SeedVerifiedOrgForClient(client, name, "College", domain));

    private static Task<HttpResponseMessage> SubmitClaim(HttpClient client, Guid orgId, string role = "Student") =>
        client.PostAsJsonAsync($"/v1/orgs/{orgId}/membership-claims", new
        {
            claimedRole = role,
            documents = new[] { new { docType = "student_id", storageKey = "private/verif/id.jpg" } },
        });

    private async Task SetEmailAsync(Guid userId, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.Email = email;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Submit_creates_a_pending_claim()
    {
        var (owner, _) = await LoginAsync("9930000001");
        var orgId = await CreateOrgAsync(owner, "Claim Target College A");
        var (student, _) = await LoginAsync("9930000002");
        var body = await Json(await SubmitClaim(student, orgId));
        Assert.Equal("submitted", body.GetProperty("status").GetString());
        Assert.Equal("student", body.GetProperty("claimed_role").GetString());
    }

    [Fact]
    public async Task Invalid_role_and_missing_evidence_are_rejected()
    {
        var (owner, _) = await LoginAsync("9930000003");
        var orgId = await CreateOrgAsync(owner, "Claim Target College B");
        var (student, _) = await LoginAsync("9930000004");

        var badRole = await student.PostAsJsonAsync($"/v1/orgs/{orgId}/membership-claims",
            new { claimedRole = "supreme-leader", documents = new[] { new { docType = "x", storageKey = "y" } } });
        Assert.Equal(HttpStatusCode.BadRequest, badRole.StatusCode);
        Assert.Equal("invalid_role", (await Json(badRole)).GetProperty("error").GetString());

        var noEvidence = await student.PostAsJsonAsync($"/v1/orgs/{orgId}/membership-claims",
            new { claimedRole = "Student", documents = Array.Empty<object>() });
        Assert.Equal("invalid_evidence", (await Json(noEvidence)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Duplicate_active_claim_is_rejected()
    {
        var (owner, _) = await LoginAsync("9930000005");
        var orgId = await CreateOrgAsync(owner, "Claim Target College C");
        var (student, _) = await LoginAsync("9930000006");
        Assert.Equal(HttpStatusCode.OK, (await SubmitClaim(student, orgId)).StatusCode);
        var dup = await SubmitClaim(student, orgId);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("already_claimed", (await Json(dup)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Claim_is_fast_tracked_when_email_domain_matches_org()
    {
        var (owner, _) = await LoginAsync("9930000007");
        var orgId = await CreateOrgAsync(owner, "Domain Match College", domain: "dmc.ac.in");
        var (student, studentId) = await LoginAsync("9930000008");
        await SetEmailAsync(studentId, "roll123@dmc.ac.in");
        var body = await Json(await SubmitClaim(student, orgId));
        Assert.True(body.GetProperty("fast_track").GetBoolean());
    }

    [Fact]
    public async Task Non_reviewer_cannot_work_the_queue()
    {
        var (user, _) = await LoginAsync("9930000009");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/v1/admin/membership-claims/pending")).StatusCode);
    }

    [Fact]
    public async Task Approval_grants_a_verified_staff_membership_and_audits()
    {
        var (owner, _) = await LoginAsync("9930000010");
        var orgId = await CreateOrgAsync(owner, "Approve Grant College");
        var (student, studentId) = await LoginAsync("9930000011");
        var claimId = (await Json(await SubmitClaim(student, orgId))).GetProperty("id").GetGuid();

        var reviewer = await ReviewerAsync("9930000012");
        var res = await reviewer.PostAsJsonAsync($"/v1/admin/membership-claims/{claimId}/review",
            new { decision = "approve", reasonCode = "id_valid" });
        Assert.Equal("approved", (await Json(res)).GetProperty("status").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var membership = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.OrgId == orgId && m.UserId == studentId);
        Assert.NotNull(membership);
        Assert.True(membership!.IsVerified);
        Assert.Equal(OrgRole.Staff, membership.Role);            // read-only seat; organizer rights need Owner action
        Assert.Equal(claimId, membership.SourceClaimId);

        var review = await db.VerificationReviews.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SubjectType == VerificationSubjectType.Membership
                && r.SubjectId == claimId && r.Decision == VerificationDecision.Approve);
        Assert.NotNull(review);
        Assert.NotNull(review!.ReviewerId);
    }

    [Fact]
    public async Task Rejection_does_not_grant_membership()
    {
        var (owner, _) = await LoginAsync("9930000013");
        var orgId = await CreateOrgAsync(owner, "Reject College");
        var (student, studentId) = await LoginAsync("9930000014");
        var claimId = (await Json(await SubmitClaim(student, orgId))).GetProperty("id").GetGuid();

        var reviewer = await ReviewerAsync("9930000015");
        var res = await reviewer.PostAsJsonAsync($"/v1/admin/membership-claims/{claimId}/review", new { decision = "reject" });
        Assert.Equal("rejected", (await Json(res)).GetProperty("status").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.False(await db.Memberships.AnyAsync(m => m.OrgId == orgId && m.UserId == studentId));
    }

    // ── Claim-evidence upload (D-055 G4): a claimant is NOT a member, so this presign has no role gate ──
    private static Task<HttpResponseMessage> PresignClaim(HttpClient client, Guid orgId) => client.PostAsJsonAsync(
        $"/v1/orgs/{orgId}/membership-claims/media/presign", new { contentType = "image/jpeg", maxBytes = 2048 });

    [Fact]
    public async Task Claimant_can_presign_evidence_for_an_existing_org()
    {
        var (owner, _) = await LoginAsync("9930000020");
        var orgId = await CreateOrgAsync(owner, "Claim Presign College");
        var (student, _) = await LoginAsync("9930000021");   // not a member — may still upload their own proof
        var res = await PresignClaim(student, orgId);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.StartsWith($"orgs/{orgId}/claims/", (await Json(res)).GetProperty("key").GetString());
    }

    [Fact]
    public async Task Claim_presign_for_unknown_org_is_not_found()
    {
        var (student, _) = await LoginAsync("9930000022");
        Assert.Equal(HttpStatusCode.NotFound, (await PresignClaim(student, Guid.NewGuid())).StatusCode);
    }
}
