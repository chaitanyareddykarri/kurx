using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Organization verification lifecycle (M5, D-044): Owner submits evidence → PendingReview;
/// a platform VerificationReviewer approves/rejects/requests-changes/suspends. Approval reserves the
/// normalized name (hard dedup vs other verified orgs). All via real HTTP + kurx_test.</summary>
public class OrgVerificationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public OrgVerificationTests(KurxApiFactory factory)
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

    // D-075: seed an Unverified org the caller owns, so the M5 submit → review lifecycle below runs on it
    // (institutions are no longer self-minted via POST /v1/orgs).
    private Task<Guid> CreateOrgAsync(HttpClient client, string name)
        => Task.FromResult(_factory.SeedVerifiedOrgForClient(client, name, status: OrgVerificationStatus.Unverified));

    private static Task<HttpResponseMessage> Submit(HttpClient client, Guid orgId) => client.PostAsJsonAsync(
        $"/v1/orgs/{orgId}/verification/submit",
        new { documents = new[] { new { docType = "registration_cert", storageKey = "private/verif/reg.pdf" } } });

    [Fact]
    public async Task Submit_moves_org_to_pending_review_with_evidence()
    {
        var (owner, _) = await LoginAsync("9920000001");
        var orgId = await CreateOrgAsync(owner, "Pending Institute");
        var body = await Json(await Submit(owner, orgId));
        Assert.Equal("pendingreview", body.GetProperty("status").GetString());
        Assert.Equal(1, body.GetProperty("documents").GetArrayLength());
    }

    [Fact]
    public async Task Submit_without_evidence_is_rejected()
    {
        var (owner, _) = await LoginAsync("9920000002");
        var orgId = await CreateOrgAsync(owner, "No Evidence Institute");
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/verification/submit", new { documents = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_evidence", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Non_member_cannot_submit_and_gets_not_found()
    {
        var (owner, _) = await LoginAsync("9920000003");
        var orgId = await CreateOrgAsync(owner, "Guarded Institute");
        var (outsider, _) = await LoginAsync("9920000004");
        var res = await Submit(outsider, orgId);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);   // never leak existence (D-018)
    }

    [Fact]
    public async Task Non_reviewer_cannot_access_admin_queue()
    {
        var (user, _) = await LoginAsync("9920000005");
        var res = await user.GetAsync("/v1/admin/orgs/pending");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Reviewer_can_approve_a_pending_org()
    {
        var (owner, _) = await LoginAsync("9920000006");
        var orgId = await CreateOrgAsync(owner, "Approvable Institute");
        await Submit(owner, orgId);

        var reviewer = await ReviewerAsync("9920000007");
        var res = await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review",
            new { decision = "approve", reasonCode = "docs_ok" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("verified", (await Json(res)).GetProperty("status").GetString());

        // Owner sees the verified status.
        var view = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/verification"));
        Assert.Equal("verified", view.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Approving_a_duplicate_of_a_verified_name_is_blocked()
    {
        var reviewer = await ReviewerAsync("9920000010");

        var (ownerA, _) = await LoginAsync("9920000008");
        var orgA = await CreateOrgAsync(ownerA, "Twin Name Institute");
        await Submit(ownerA, orgA);
        await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgA}/verification/review", new { decision = "approve" });

        var (ownerB, _) = await LoginAsync("9920000009");
        var orgB = await CreateOrgAsync(ownerB, "Twin Name Institute");   // same normalized name
        await Submit(ownerB, orgB);
        var res = await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgB}/verification/review", new { decision = "approve" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("duplicate_verified_org", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Request_changes_then_resubmit_then_approve()
    {
        var (owner, _) = await LoginAsync("9920000011");
        var orgId = await CreateOrgAsync(owner, "Iterating Institute");
        await Submit(owner, orgId);

        var reviewer = await ReviewerAsync("9920000012");
        var rc = await Json(await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review",
            new { decision = "request_changes", notes = "letterhead missing" }));
        Assert.Equal("changesrequested", rc.GetProperty("status").GetString());

        await Submit(owner, orgId);   // resubmit allowed after changes requested
        var ok = await Json(await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review",
            new { decision = "approve" }));
        Assert.Equal("verified", ok.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Suspend_a_verified_org()
    {
        var (owner, _) = await LoginAsync("9920000013");
        var orgId = await CreateOrgAsync(owner, "Suspendable Institute");
        await Submit(owner, orgId);
        var reviewer = await ReviewerAsync("9920000014");
        await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review", new { decision = "approve" });

        var res = await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/suspend", new { reason = "chargebacks" });
        Assert.Equal("suspended", (await Json(res)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Approval_writes_a_verification_review_row_and_search_shows_verified()
    {
        var (owner, _) = await LoginAsync("9920000015");
        var orgId = await CreateOrgAsync(owner, "Audited Institute Vizag");
        await Submit(owner, orgId);
        var reviewer = await ReviewerAsync("9920000016");
        await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review", new { decision = "approve" });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var review = await db.VerificationReviews.AsNoTracking()
                .FirstOrDefaultAsync(r => r.SubjectType == VerificationSubjectType.Organization
                    && r.SubjectId == orgId && r.Decision == VerificationDecision.Approve);
            Assert.NotNull(review);
            Assert.NotNull(review!.ReviewerId);        // a human reviewer, not automated
        }

        var results = await Json(await owner.GetAsync("/v1/orgs/search?q=Audited Institute Vizag"));
        Assert.Equal("verified", results.EnumerateArray().First().GetProperty("verification_status").GetString());
    }

    // ── Org-document presign (D-055 G1): the URL an owner uses to upload a verification letterhead ──
    private static Task<HttpResponseMessage> Presign(HttpClient client, Guid orgId) => client.PostAsJsonAsync(
        $"/v1/orgs/{orgId}/media/presign", new { contentType = "application/pdf", maxBytes = 1024 });

    [Fact]
    public async Task Owner_can_presign_an_org_document_under_the_org_key()
    {
        var (owner, _) = await LoginAsync("9920000020");
        var orgId = await CreateOrgAsync(owner, "Presign Owner College");
        var res = await Presign(owner, orgId);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var key = (await Json(res)).GetProperty("key").GetString();
        Assert.StartsWith($"orgs/{orgId}/verification/", key);   // org-level, reused across events
    }

    [Fact]
    public async Task Non_member_presign_is_hidden_as_not_found()
    {
        var (owner, _) = await LoginAsync("9920000021");
        var orgId = await CreateOrgAsync(owner, "Presign Guarded College");
        var (outsider, _) = await LoginAsync("9920000022");
        Assert.Equal(HttpStatusCode.NotFound, (await Presign(outsider, orgId)).StatusCode);   // never leak existence (D-018)
    }

    [Fact]
    public async Task Staff_member_presign_is_forbidden()
    {
        var (owner, _) = await LoginAsync("9920000023");
        var orgId = await CreateOrgAsync(owner, "Presign Staffed College");
        await LoginAsync("9920000024");   // the staff user must exist before the owner can add them
        var add = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/members", new { phone = "9920000024", role = "Staff" });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var (staff, _) = await LoginAsync("9920000024");
        Assert.Equal(HttpStatusCode.Forbidden, (await Presign(staff, orgId)).StatusCode);   // member, but not Owner/Manager
    }

    // ── Reviewer evidence-document preview (D-055 G2) ───────────────────────────
    [Fact]
    public async Task Reviewer_can_get_a_short_lived_view_url_for_an_evidence_document()
    {
        var (owner, _) = await LoginAsync("9920000030");
        var orgId = await CreateOrgAsync(owner, "Viewable Docs Institute");
        await Submit(owner, orgId);   // attaches one evidence document

        var reviewer = await ReviewerAsync("9920000031");
        var history = await Json(await reviewer.GetAsync($"/v1/admin/verifications/organization/{orgId}/history"));
        var docId = history.GetProperty("documents").EnumerateArray().First().GetProperty("id").GetGuid();

        var res = await reviewer.GetAsync($"/v1/admin/verification-documents/{docId}/view");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await Json(res)).GetProperty("url").GetString()));
    }

    [Fact]
    public async Task Non_reviewer_cannot_view_evidence_documents()
    {
        var (user, _) = await LoginAsync("9920000032");
        var res = await user.GetAsync($"/v1/admin/verification-documents/{Guid.NewGuid()}/view");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);   // reviewer-gated before any lookup
    }

    [Fact]
    public async Task Unknown_evidence_document_is_not_found_for_a_reviewer()
    {
        var reviewer = await ReviewerAsync("9920000033");
        var res = await reviewer.GetAsync($"/v1/admin/verification-documents/{Guid.NewGuid()}/view");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── Reviewer flag on /v1/me (D-055 G3) — lets the web nav show the console to reviewers ──
    [Fact]
    public async Task Me_reports_platform_reviewer_flag()
    {
        var (user, _) = await LoginAsync("9920000034");
        Assert.False((await Json(await user.GetAsync("/v1/me"))).GetProperty("is_platform_reviewer").GetBoolean());

        var reviewer = await ReviewerAsync("9920000035");
        Assert.True((await Json(await reviewer.GetAsync("/v1/me"))).GetProperty("is_platform_reviewer").GetBoolean());
    }
}
