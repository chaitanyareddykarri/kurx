using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Person identity verification (M3, D-042). Exercises the real domain (levels, masking,
/// provider rejection, retry cap, audit-review rows, /v1/me summary) against the mock IKycProvider
/// via real HTTP + kurx_test — the same mock-backed pattern org bank/PAN KYC uses (D-016).</summary>
public class IdentityVerificationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public IdentityVerificationTests(KurxApiFactory factory)
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

    [Fact]
    public async Task New_user_identity_defaults_to_phone_not_started()
    {
        var (client, _) = await LoginAsync("9600000001");
        var body = await Json(await client.GetAsync("/v1/me/identity"));
        Assert.Equal("Phone", body.GetProperty("level").GetString());
        Assert.Equal("NotStarted", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Pan_verification_approves_stores_only_last4_and_promotes_level()
    {
        var (client, _) = await LoginAsync("9600000002");
        var res = await client.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Aarav Sharma" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await Json(res);
        Assert.Equal("Approved", body.GetProperty("status").GetString());
        Assert.Equal("GovernmentId", body.GetProperty("level").GetString());
        Assert.Equal("234F", body.GetProperty("pan_last4").GetString());   // only last 4, never the full PAN
    }

    [Fact]
    public async Task Government_id_verification_approves()
    {
        var (client, _) = await LoginAsync("9600000003");
        var body = await Json(await client.PostAsJsonAsync("/v1/me/identity/government-id",
            new { kind = "digilocker", idNumber = "123456789012", name = "Aarav Sharma" }));
        Assert.Equal("Approved", body.GetProperty("status").GetString());
        Assert.Equal("GovernmentId", body.GetProperty("level").GetString());
        Assert.Equal("9012", body.GetProperty("govt_id_last4").GetString());
    }

    [Fact]
    public async Task Bank_penny_drop_ending_0000_is_rejected_and_does_not_promote()
    {
        var (client, _) = await LoginAsync("9600000004");
        var body = await Json(await client.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12340000", ifsc = "HDFC0001234", holderName = "Aarav Sharma" }));
        Assert.Equal("Rejected", body.GetProperty("status").GetString());
        Assert.Equal("Phone", body.GetProperty("level").GetString());
        Assert.True(body.GetProperty("bank_last4").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Bank_penny_drop_approves_to_bank_level()
    {
        var (client, _) = await LoginAsync("9600000005");
        var body = await Json(await client.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Aarav Sharma" }));
        Assert.Equal("Approved", body.GetProperty("status").GetString());
        Assert.Equal("Bank", body.GetProperty("level").GetString());
        Assert.Equal("5678", body.GetProperty("bank_last4").GetString());
    }

    [Fact]
    public async Task Invalid_pan_format_is_rejected_by_the_service()
    {
        var (client, _) = await LoginAsync("9600000006");
        var res = await client.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "not-a-pan", name = "Aarav" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_pan", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Resubmission_is_capped_after_max_attempts()
    {
        var (client, _) = await LoginAsync("9600000007");
        // 5 rejected attempts (account ends 0000), then the 6th is blocked.
        for (var i = 0; i < 5; i++)
        {
            var r = await client.PostAsJsonAsync("/v1/me/identity/bank",
                new { accountNumber = "12340000", ifsc = "HDFC0001234", holderName = "Aarav Sharma" });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }
        var blocked = await client.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12340000", ifsc = "HDFC0001234", holderName = "Aarav Sharma" });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Approval_writes_an_automated_verification_review_row()
    {
        var (client, userId) = await LoginAsync("9600000008");
        await client.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Aarav Sharma" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var identity = await db.UserIdentities.AsNoTracking().SingleAsync(x => x.UserId == userId);
        var review = await db.VerificationReviews.AsNoTracking()
            .SingleOrDefaultAsync(r => r.SubjectType == VerificationSubjectType.UserIdentity && r.SubjectId == identity.Id);
        Assert.NotNull(review);
        Assert.Equal(VerificationDecision.Approve, review!.Decision);
        Assert.Null(review.ReviewerId);                  // automated / provider decision
    }

    [Fact]
    public async Task Me_includes_the_identity_summary()
    {
        var (client, _) = await LoginAsync("9600000009");
        await client.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Aarav Sharma" });
        var me = await Json(await client.GetAsync("/v1/me"));
        Assert.Equal("GovernmentId", me.GetProperty("identity").GetProperty("level").GetString());
        Assert.Equal("Approved", me.GetProperty("identity").GetProperty("status").GetString());
    }
}
