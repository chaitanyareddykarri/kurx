using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Trust capability composition (M7, D-046). Live-derived from identity (M3), org (M5), and
/// membership (M6) verification. Surfaced on /v1/me (user caps) and /v1/orgs/{id}/my-capabilities
/// (org-context caps). Real HTTP/kurx_test.</summary>
public class TrustTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public TrustTests(KurxApiFactory factory)
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

    private static async Task<JsonElement> Trust(HttpClient client)
        => (await Json(await client.GetAsync("/v1/me"))).GetProperty("trust");

    [Fact]
    public async Task New_user_can_organize_free_but_not_paid()
    {
        var (client, _) = await LoginAsync("9940000001");
        var trust = await Trust(client);
        Assert.True(trust.GetProperty("can_organize_free").GetBoolean());
        Assert.False(trust.GetProperty("can_organize_paid").GetBoolean());
        Assert.Equal("L1", trust.GetProperty("level").GetString());
    }

    // ── D-307 · public event creation requires the full verification gate ──────────────────────
    //
    // The rule these pin is a PRODUCT decision that reverses the platform's previous behaviour: a free
    // public event used to require nothing. It now requires the same set as taking money, because a
    // free public event still carries the platform's name and reaches every user through discovery.
    //
    // These assert the CAPABILITY LOGIC, deliberately independent of the KYC provider: `MockKycProvider`
    // approves unconditionally, so a passing bank submission here proves the state machine, never that
    // a real penny drop succeeded.

    [Fact]
    public async Task New_user_cannot_create_a_public_event_but_can_create_a_private_one()
    {
        var (client, _) = await LoginAsync("9940000010");
        var trust = await Trust(client);

        // The reversal, stated as an assertion: free public is no longer free of verification.
        Assert.False(trust.GetProperty("can_create_public_event").GetBoolean());
        Assert.True(trust.GetProperty("can_create_private_event").GetBoolean());
        // ...and the old capability is untouched, which is what protects the money path.
        Assert.True(trust.GetProperty("can_organize_free").GetBoolean());
    }

    [Fact]
    public async Task Identity_without_bank_still_cannot_create_a_public_event()
    {
        var (client, _) = await LoginAsync("9940000011");
        await client.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Riya Nair" });

        var trust = await Trust(client);
        Assert.True(trust.GetProperty("identity_verified").GetBoolean());
        // Bank ownership — the penny-drop chain — is part of the public bar, so PAN alone is not enough.
        Assert.False(trust.GetProperty("bank_verified").GetBoolean());
        Assert.False(trust.GetProperty("can_create_public_event").GetBoolean());
        // Private is unaffected by anything financial.
        Assert.True(trust.GetProperty("can_create_private_event").GetBoolean());
    }

    [Fact]
    public async Task Full_identity_and_bank_unlocks_public_event_creation()
    {
        var (client, _) = await LoginAsync("9940000012");
        await client.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Kabir Rao" });
        await client.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Kabir Rao" });

        var trust = await Trust(client);
        Assert.True(trust.GetProperty("can_create_public_event").GetBoolean());
        Assert.True(trust.GetProperty("can_create_private_event").GetBoolean());
    }

    [Fact]
    public async Task Public_creation_and_paid_organizing_stay_separate_fields()
    {
        // They share a predicate today and that is a coincidence of current requirements, not an
        // identity. This exists so that a future change to either is forced to be deliberate: whoever
        // widens one and not the other will see this test, rather than discovering the coupling in
        // OrderService.
        var (client, _) = await LoginAsync("9940000013");
        await client.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Meera Iyer" });
        await client.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Meera Iyer" });

        var trust = await Trust(client);
        Assert.True(trust.GetProperty("can_organize_paid").GetBoolean());
        Assert.True(trust.GetProperty("can_create_public_event").GetBoolean());
        Assert.True(trust.GetProperty("can_receive_payout").GetBoolean());
    }

    [Fact]
    public async Task Identity_plus_bank_unlocks_paid_organizing_and_payout()
    {
        var (client, _) = await LoginAsync("9940000002");
        await client.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Aarav Sharma" });
        await client.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Aarav Sharma" });

        var trust = await Trust(client);
        Assert.True(trust.GetProperty("identity_verified").GetBoolean());
        Assert.True(trust.GetProperty("bank_verified").GetBoolean());
        Assert.True(trust.GetProperty("can_organize_paid").GetBoolean());
        Assert.True(trust.GetProperty("can_receive_payout").GetBoolean());
        Assert.Equal("L2", trust.GetProperty("level").GetString());
    }

    [Fact]
    public async Task Identity_without_bank_cannot_organize_paid()
    {
        var (client, _) = await LoginAsync("9940000003");
        await client.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Aarav Sharma" });

        var trust = await Trust(client);
        Assert.True(trust.GetProperty("identity_verified").GetBoolean());
        Assert.False(trust.GetProperty("bank_verified").GetBoolean());
        Assert.False(trust.GetProperty("can_organize_paid").GetBoolean());
    }

    [Fact]
    public async Task Verified_member_of_a_verified_org_is_a_verified_rep()
    {
        var reviewer = await ReviewerAsync("9940000004");

        // D-075: a representation request, approved, yields a Verified org (the submitter is a verified rep).
        var (owner, _) = await LoginAsync("9940000005");
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, "Verified Rep College");

        // Student files + gets a membership claim approved.
        var (student, _) = await LoginAsync("9940000006");
        var claimId = (await Json(await student.PostAsJsonAsync($"/v1/orgs/{orgId}/membership-claims",
                new { claimedRole = "Student", documents = new[] { new { docType = "student_id", storageKey = "k" } } })))
            .GetProperty("id").GetGuid();
        await reviewer.PostAsJsonAsync($"/v1/admin/membership-claims/{claimId}/review", new { decision = "approve" });

        var caps = await Json(await student.GetAsync($"/v1/orgs/{orgId}/my-capabilities"));
        Assert.True(caps.GetProperty("can_represent_org").GetBoolean());
        Assert.True(caps.GetProperty("is_org_verified_rep").GetBoolean());
        Assert.True(caps.GetProperty("is_org_verified").GetBoolean());
    }

    [Fact]
    public async Task Plain_operational_member_cannot_represent_the_org()
    {
        var (owner, _) = await LoginAsync("9940000007");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Plain Member College");
        // Owner adds a plain Staff member (operational, not affiliation-verified).
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/members", new { phone = "9940000008", role = "staff" });

        var (staff, _) = await LoginAsync("9940000008");
        var caps = await Json(await staff.GetAsync($"/v1/orgs/{orgId}/my-capabilities"));
        Assert.False(caps.GetProperty("can_represent_org").GetBoolean());     // operational != verified affiliation
    }
}
