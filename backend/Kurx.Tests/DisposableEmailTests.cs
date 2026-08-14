using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>A host with a configured disposable-domain list. Absent config makes the policy inert, which
/// is the shipped default, so the feature can only be exercised by opting in — exactly as an operator
/// would.</summary>
public class DisposableEmailFactory : KurxApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(DisposableEmailPolicy.ConfigKey, "mailinator.com, Guerrillamail.com ,10minutemail.com");
    }
}

/// <summary>
/// Disposable email is a RISK SIGNAL, never a registration block (Phase 2).
/// </summary>
/// <remarks>
/// <para>The policy question these pin down is not "are throwaway addresses bad" but "what does Kurx do
/// about them". Email is not an authentication factor here — phone OTP is — so a disposable address buys
/// an attacker very little, while refusing one at signup would fall hardest on students using temporary
/// institutional addresses. The capability that actually carries risk, organising paid public events, is
/// already gated on identity + PAN + bank + fraud-clear (D-307). So detection feeds that existing score
/// and the existing gate decides; no second verdict, no new table, no new endpoint.</para>
///
/// <para>The score is deliberately far below the blocking threshold. This phase measures; enforcement is a
/// later decision to be made against observed volume rather than in advance.</para>
/// </remarks>
public class DisposableEmailTests : IClassFixture<DisposableEmailFactory>
{
    private readonly DisposableEmailFactory _factory;

    public DisposableEmailTests(DisposableEmailFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _seq;
    private static string NextPhone() => $"9191{Interlocked.Increment(ref _seq):D6}";

    // ── the policy in isolation ─────────────────────────────────────────────

    private static DisposableEmailPolicy Policy(string? configured) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DisposableEmailPolicy.ConfigKey] = configured })
            .Build());

    [Theory]
    [InlineData("someone@mailinator.com")]
    [InlineData("SOMEONE@MAILINATOR.COM")]          // the address's case is irrelevant
    [InlineData("someone@Guerrillamail.com")]       // the LIST's case is irrelevant too
    [InlineData("someone@team.mailinator.com")]     // a per-user subdomain is the point of these services
    [InlineData("someone@a.b.10minutemail.com")]    // arbitrarily deep
    public void A_listed_domain_is_recognised(string email)
        => Assert.True(Policy("mailinator.com,guerrillamail.com,10minutemail.com").IsDisposable(email));

    [Theory]
    [InlineData("someone@gmail.com")]
    [InlineData("student@iitb.ac.in")]
    [InlineData("someone@notmailinator.com")]       // a DIFFERENT registrable domain — must not match
    [InlineData("someone@mailinator.com.evil.net")] // listed name as a LABEL, not the suffix
    [InlineData("mailinator.com")]                  // no '@' at all
    [InlineData("someone@")]
    [InlineData("")]
    public void An_unlisted_or_malformed_address_is_not(string email)
        => Assert.False(Policy("mailinator.com,guerrillamail.com,10minutemail.com").IsDisposable(email));

    [Fact]
    public void With_no_configuration_the_policy_is_inert()
    {
        var policy = Policy(null);
        Assert.True(policy.IsEmpty);
        Assert.False(policy.IsDisposable("someone@mailinator.com"));
    }

    [Fact]
    public void The_score_stays_below_the_blocking_threshold()
        // The whole premise of this phase. If these ever cross, a disposable address alone starts denying
        // paid organising, which is a product decision that must be taken explicitly.
        => Assert.True(DisposableEmailPolicy.SignalScore < Kurx.Infrastructure.Trust.FraudService.HighRiskThreshold);

    // ── end to end, through the real verification ceremony ──────────────────

    [Fact]
    public async Task A_verified_disposable_address_records_exactly_one_signal_and_still_authenticates()
    {
        var (client, userId, phone) = await SignInAsync();
        var email = $"throwaway{userId:N}@mailinator.com";

        await VerifyEmailAsync(client, email);

        var signals = await SignalsFor(userId);
        Assert.Single(signals);
        Assert.Equal(FraudSignalKind.DisposableContact, signals[0].Kind);
        Assert.Equal(email, signals[0].Value);
        Assert.Equal(DisposableEmailPolicy.SignalScore, signals[0].Score);

        // Not a block: the address verified, and the account signs in exactly as before.
        Assert.True(await EmailIsVerified(userId));
        var again = await SignInWithAsync(phone);
        Assert.NotNull(again);
    }

    [Fact]
    public async Task A_normal_address_records_no_signal()
    {
        var (client, userId, _) = await SignInAsync();

        await VerifyEmailAsync(client, $"real{userId:N}@gmail.com");

        Assert.Empty(await SignalsFor(userId));
        Assert.True(await EmailIsVerified(userId));
    }

    [Fact]
    public async Task Re_verifying_the_same_address_does_not_add_a_second_signal()
    {
        var (client, userId, _) = await SignInAsync();
        var email = $"repeat{userId:N}@mailinator.com";

        await VerifyEmailAsync(client, email);
        await ReVerifyEmailAsync(client, userId, email);   // a second device, or simply doing it again

        // Doing a legitimate thing twice must not push someone toward the threshold.
        Assert.Single(await SignalsFor(userId));
    }

    [Fact]
    public async Task Changing_to_a_second_disposable_address_records_it_separately()
    {
        var (client, userId, _) = await SignInAsync();

        await VerifyEmailAsync(client, $"first{userId:N}@mailinator.com");
        await VerifyEmailAsync(client, $"second{userId:N}@10minutemail.com");

        // Two distinct addresses are two distinct facts. The de-duplication is per address, not per user,
        // or someone could cycle throwaway addresses forever behind one recorded signal.
        var signals = await SignalsFor(userId);
        Assert.Equal(2, signals.Count);
        Assert.Equal(2, signals.Select(s => s.Value).Distinct().Count());
    }

    [Fact]
    public async Task The_signal_does_not_by_itself_make_an_account_high_risk()
    {
        var (client, userId, _) = await SignInAsync();
        await VerifyEmailAsync(client, $"lowrisk{userId:N}@mailinator.com");

        using var scope = _factory.Services.CreateScope();
        var fraud = scope.ServiceProvider.GetRequiredService<Kurx.Application.Abstractions.IFraudService>();

        // The existing gate still says clear: detection without enforcement is the entire decision here.
        Assert.True(await fraud.IsUserClearAsync(userId));
    }

    [Fact]
    public async Task An_existing_verified_account_is_not_re_scored_by_the_policy_arriving()
    {
        // Grandfathering: the signal is emitted by the verification path, so an address verified before the
        // policy existed is never revisited. Simulated by writing the verified state directly, which is
        // what such a row looks like.
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = new Kurx.Domain.Entities.User
            {
                Name = "Grandfathered",
                Phone = NextPhone(),
                Email = $"legacy{Guid.NewGuid():N}@mailinator.com",
                EmailVerifiedAt = DateTime.UtcNow.AddDays(-30),
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        Assert.Empty(await SignalsFor(userId));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<(HttpClient Client, Guid UserId, string Phone)> SignInAsync()
    {
        var phone = NextPhone();
        var token = await SignInWithAsync(phone);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Value.Token);
        return (client, token.Value.UserId, phone);
    }

    private async Task<(string Token, Guid UserId)?> SignInWithAsync(string phone)
    {
        var c = _factory.CreateClient();
        var req = await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        Assert.Equal(HttpStatusCode.OK, req.StatusCode);
        var code = _factory.Phone.LastOtpFor(phone);
        var res = await c.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("access_token").GetString()!, body.GetProperty("user_id").GetGuid());
    }

    private async Task VerifyEmailAsync(HttpClient client, string email)
    {
        var start = await client.PostAsJsonAsync("/v1/auth/email/verify/start", new { email });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var code = _factory.Email.LastOtpFor(email);
        var complete = await client.PostAsJsonAsync("/v1/auth/email/verify/complete", new { email, code });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
    }

    /// <summary>A second verification of an address already verified moments ago.
    ///
    /// <para>The HTTP start endpoint cannot be used twice in a row here: <c>IOtpService</c> enforces a
    /// 30-second resend spacing per destination — correct behaviour, and not something to weaken for a
    /// test. The code is therefore minted through the same service with the spacing flag the platform
    /// already exposes for exactly this (<c>enforceResendCooldown: false</c>), and completion still goes
    /// through the real endpoint, which is the path whose de-duplication is under test.</para></summary>
    private async Task ReVerifyEmailAsync(HttpClient client, Guid userId, string email)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var otp = scope.ServiceProvider.GetRequiredService<Kurx.Application.Abstractions.IOtpService>();
            var issued = await otp.IssueAsync(email, OtpChannel.Email, OtpPurpose.EmailVerification,
                userId, requestIp: null, enforceResendCooldown: false);
            Assert.True(issued.Ok, $"could not re-issue: {issued.Error}");
        }

        var code = _factory.Email.LastOtpFor(email);
        var complete = await client.PostAsJsonAsync("/v1/auth/email/verify/complete", new { email, code });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
    }

    private async Task<List<Kurx.Domain.Entities.FraudSignal>> SignalsFor(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.FraudSignals.AsNoTracking()
            .Where(s => s.SubjectId == userId && s.Kind == FraudSignalKind.DisposableContact)
            .ToListAsync();
    }

    private async Task<bool> EmailIsVerified(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.EmailVerifiedAt != null);
    }
}
