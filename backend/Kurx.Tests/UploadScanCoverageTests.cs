using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kurx.Tests;

/// <summary>D-338 — the six upload paths that had no malware gate at all.
///
/// <para><c>IFileScanner</c> shipped real in D-298 but was injected into exactly two services, so event
/// media, org-verification evidence, membership-claim evidence, representation-request evidence,
/// institutional-authorization evidence and profile images all persisted a storage key without ever
/// scanning the bytes behind it. Four of those six are documents a VerificationReviewer downloads and
/// opens in the admin console.</para>
///
/// <para><b>Why an unreachable daemon rather than EICAR.</b> <c>ClamAvUploadPathTests</c> already proves
/// the engine detects a real signature; what these tests prove is different and complementary — that the
/// gate is <i>wired at each call site</i> and fails closed. Pointing at a dead port makes every scan return
/// <c>ScanFailed</c>, which a correctly-gated path must refuse. It also means these run anywhere: no clamd,
/// no signature database, no <c>--profile scanning</c>, so a coverage regression is caught on every
/// developer machine and in CI, not only where a daemon happens to be up.</para></summary>
public class UnreachableScannerFactory : KurxApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("FILE_SCANNER", "clamav");
        builder.UseSetting("CLAMAV_HOST", "127.0.0.1");
        builder.UseSetting("CLAMAV_PORT", "1");        // nothing listens here
        builder.UseSetting("CLAMAV_TIMEOUT_SECONDS", "5");
    }
}

public class UploadScanCoverageTests : IClassFixture<UnreachableScannerFactory>
{
    private readonly UnreachableScannerFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public UploadScanCoverageTests(UnreachableScannerFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    /// <summary>Every assertion below is worthless on <c>NoOpFileScanner</c>, which reports Clean without
    /// reading anything — so this runs first and states the premise.</summary>
    [Fact]
    public void The_container_resolves_the_real_scanner()
    {
        using var scope = _factory.Services.CreateScope();
        Assert.Equal("ClamAvFileScanner",
            scope.ServiceProvider.GetRequiredService<IFileScanner>().GetType().Name);
    }

    /// <summary>A refusal must name the scanner, not look like bad input. <c>scan_unavailable</c> is the
    /// same code chat already returns for an unreachable daemon, so operators read one vocabulary.</summary>
    private static void AssertRefusedByScanner(bool ok, string? error)
    {
        Assert.False(ok);
        Assert.Equal("scan_unavailable", error);
    }

    // ── the four reviewer-facing document paths ──────────────────────────────────────────────────

    [Fact]
    public async Task Org_verification_evidence_is_refused_when_it_cannot_be_scanned()
    {
        using var scope = _factory.Services.CreateScope();
        var (ownerId, orgId) = SeedOrg(scope, "9931000001", "Scan Verify Org", OrgVerificationStatus.Unverified);

        var result = await scope.ServiceProvider.GetRequiredService<IOrgVerificationService>()
            .SubmitAsync(ownerId, orgId, [new OrgVerificationEvidence("registration", "orgs/x/verification/a")]);

        AssertRefusedByScanner(result.Ok, result.Error);

        // The refusal is total: no document row, and the org never entered the review queue.
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.False(await db.VerificationDocuments.AnyAsync(d => d.SubjectId == orgId));
        Assert.Equal(OrgVerificationStatus.Unverified,
            (await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == orgId)).VerificationStatus);
    }

    [Fact]
    public async Task Membership_claim_evidence_is_refused_when_it_cannot_be_scanned()
    {
        using var scope = _factory.Services.CreateScope();
        var (_, orgId) = SeedOrg(scope, "9931000002", "Scan Claim Org", OrgVerificationStatus.Verified);
        var claimantId = SeedUser(scope, "9931000003");

        var result = await scope.ServiceProvider.GetRequiredService<IMembershipVerificationService>()
            .SubmitAsync(claimantId, orgId, "Employee", null, [new MembershipEvidence("id_card", "orgs/x/claims/y/a")]);

        AssertRefusedByScanner(result.Ok, result.Error);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.False(await db.MembershipClaims.AnyAsync(c => c.UserId == claimantId && c.OrgId == orgId));
    }

    [Fact]
    public async Task Representation_request_evidence_is_refused_and_leaves_no_placeholder_org()
    {
        using var scope = _factory.Services.CreateScope();
        var userId = SeedUser(scope, "9931000004");

        var result = await scope.ServiceProvider.GetRequiredService<IOrgService>()
            .SubmitRepresentationRequestAsync(userId, "Scan Placeholder Institute", OrganizationType.College,
                null, null, null, null, [new OrgVerificationEvidence("registration", "representation-requests/y/a")]);

        AssertRefusedByScanner(result.Ok, result.Error);

        // The scan runs BEFORE the placeholder org is created, so a refused submission stages nothing —
        // no hidden PendingReview institution and no pending Representative membership behind it.
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.False(await db.Organizations.AnyAsync(o => o.Name == "Scan Placeholder Institute"));
    }

    [Fact]
    public async Task Institutional_authorization_letterhead_is_refused_when_it_cannot_be_scanned()
    {
        using var scope = _factory.Services.CreateScope();
        var (ownerId, orgId) = SeedOrg(scope, "9931000005", "Scan Authz Org", OrgVerificationStatus.Verified);
        var eventId = SeedEvent(scope, ownerId, orgId, "scan-authz");

        var result = await scope.ServiceProvider.GetRequiredService<IEventAuthorizationService>()
            .SubmitAsync(ownerId, eventId, false, new EventAuthorizationInput(
                "Dr Head", "Principal", "head@example.edu", "+919931000009",
                LetterheadDocumentKey: "events/x/authorization/a",
                SignatureDocumentKey: null,
                SupportingDocumentKeys: null,
                RepresentativeRole: "Principal"));

        AssertRefusedByScanner(result.Ok, result.Error);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.False(await db.EventAuthorizations.AnyAsync(a => a.EventId == eventId));
    }

    // ── the two content paths ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Event_media_attach_is_refused_when_it_cannot_be_scanned()
    {
        using var scope = _factory.Services.CreateScope();
        var (ownerId, orgId) = SeedOrg(scope, "9931000006", "Scan Media Org", OrgVerificationStatus.Verified);
        var eventId = SeedEvent(scope, ownerId, orgId, "scan-media");

        var result = await scope.ServiceProvider.GetRequiredService<IMediaService>()
            .AttachAsync(ownerId, eventId, false, "Gallery", "events/x/media/a", null);

        AssertRefusedByScanner(result.Ok, result.Error);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.False(await db.EventMedia.AnyAsync(m => m.EventId == eventId));
    }

    /// <summary>The one path that is an endpoint rather than a service, so it is driven over HTTP. The
    /// avatar key is rendered directly by every client, which is why it is gated at all.</summary>
    [Fact]
    public async Task Profile_image_key_is_refused_when_it_cannot_be_scanned()
    {
        var client = _factory.CreateClient();
        const string phone = "9931000007";
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await (await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            tokens.GetProperty("access_token").GetString());

        // The caller's OWN prefix (D-354). It used to be `users/x/avatar/a`, which since D-354 is
        // refused as `invalid_storage_key` before the scanner is ever consulted — so the assertion below
        // would have passed for the wrong reason and stopped covering the scan gate at all. The
        // cross-user case has its own test, immediately after this one.
        var userId = (await (await client.GetAsync("/v1/me")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        // camelCase IN, snake_case OUT — the convention that has bitten more than one client. Sending
        // `avatar_key` here binds nothing, the gate sees a null key, and the request succeeds.
        var res = await client.PatchAsJsonAsync("/v1/me/profile",
            new { avatarKey = $"users/{userId}/avatar/a" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("scan_unavailable", problem.GetProperty("error").GetString());
    }

    /// <summary>D-354 — a key under ANOTHER user's prefix is refused outright, before the scanner.
    ///
    /// <para>The hole this closes: nothing checked the prefix, so any account could PATCH another user's
    /// avatar key onto its own profile. Every projection then presigned that key and served a private
    /// upload under the wrong person's name — and it would have read as a caching bug, not a breach.</para></summary>
    [Fact]
    public async Task A_profile_image_key_belonging_to_another_user_is_refused()
    {
        var client = _factory.CreateClient();
        const string phone = "9931000009";
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await (await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            tokens.GetProperty("access_token").GetString());

        var res = await client.PatchAsJsonAsync("/v1/me/profile",
            new { avatarKey = $"users/{Guid.NewGuid()}/avatar/a" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        // Not `scan_unavailable`: the refusal is authorization, and it must not depend on a scanner
        // being reachable.
        Assert.Equal("invalid_storage_key", problem.GetProperty("error").GetString());
    }

    /// <summary>The gate must not fire on a request that claims no key — otherwise every profile edit
    /// (a name, a bio) would need a reachable scanner, which would turn a scanner outage into a total
    /// profile outage. Guards the null/blank branch in <c>UploadScanGate</c>.</summary>
    [Fact]
    public async Task A_profile_update_that_claims_no_image_is_unaffected_by_the_scanner()
    {
        var client = _factory.CreateClient();
        const string phone = "9931000008";
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await (await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            tokens.GetProperty("access_token").GetString());

        var res = await client.PatchAsJsonAsync("/v1/me/profile", new { headline = "no image here" });

        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    // ── seeding ──────────────────────────────────────────────────────────────────────────────────

    private static Guid SeedUser(IServiceScope scope, string phone)
    {
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = new User { Name = "Scan User " + phone, Phone = phone };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    private (Guid OwnerId, Guid OrgId) SeedOrg(IServiceScope scope, string phone, string name,
        OrgVerificationStatus status)
    {
        var ownerId = SeedUser(scope, phone);
        return (ownerId, _factory.SeedVerifiedOrg(ownerId, name, OrgRole.Owner, status: status));
    }

    private static Guid SeedEvent(IServiceScope scope, Guid ownerId, Guid orgId, string slug)
    {
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var category = new EventCategory { Level = CategoryLevel.Category, Name = "Scan " + slug, Slug = slug };
        db.EventCategories.Add(category);
        db.SaveChanges();

        var ev = new Event
        {
            RepresentingOrgId = orgId, CreatedBy = ownerId, CategoryId = category.Id,
            Title = "Scan Event", Slug = slug + "-event",
            // Unique per call: `events.ShortCode` is unique and two slugs sharing a three-letter prefix
            // ("scan-authz", "scan-media") collided into one code.
            ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Description = "d", VenueName = "v",
            StartsAt = DateTime.UtcNow.AddDays(3), EndsAt = DateTime.UtcNow.AddDays(4),
            Status = EventStatus.Draft,
        };
        db.Events.Add(ev);
        db.SaveChanges();
        return ev.Id;
    }
}

/// <summary>D-338 — <c>FILE_SCANNER=none</c> resolves <c>NoOpFileScanner</c>, which reports every file
/// Clean without reading it. Every other dangerous default in <c>AddKurxInfrastructure</c> already refuses
/// Production — <c>SIGNING_KEY_PROTECTION=none</c>, <c>IDENTITY_VERIFICATION_BYPASS</c>, a missing
/// <c>REDIS_CONNECTION</c> — and this one did not, so a deployment that simply omitted the variable served
/// every upload unscanned and looked healthy. <c>.env.example</c> ships <c>FILE_SCANNER=none</c>, which
/// makes omitting it the default path rather than an unusual mistake.</summary>
public class FileScannerProductionGuardTests
{
    [Theory]
    [InlineData(null)]      // unset — the case that actually happens
    [InlineData("none")]
    [InlineData("NONE")]
    [InlineData(" none ")]
    public void No_scanner_in_production_throws_at_startup(string? value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddKurxInfrastructure(Config(value), isProduction: true));
        Assert.Contains("FILE_SCANNER", ex.Message);
    }

    /// <summary>Production accepts the real scanner, so the guard refuses a posture and not the whole
    /// boundary — a test that only proved "throws" would also pass if it always threw.</summary>
    [Fact]
    public void Clamav_in_production_is_accepted()
        => Assert.Equal("ClamAvFileScanner", ResolveScanner(Config("clamav"), isProduction: true));

    /// <summary>Dev and test must still boot with no daemon, which is the entire reason `none` exists.</summary>
    [Fact]
    public void No_scanner_outside_production_is_permitted()
        => Assert.Equal("NoOpFileScanner", ResolveScanner(Config("none"), isProduction: false));

    /// <summary>Logging and <c>IConfiguration</c> come from the host in the real app and not from
    /// <c>AddKurxInfrastructure</c>, so a bare <c>ServiceCollection</c> has to supply both: the scanners
    /// take an <c>ILogger&lt;T&gt;</c>, and resolving <c>ClamAvFileScanner</c> pulls <c>IStorage</c> →
    /// <c>LocalDiskStorage</c>, which reads configuration.</summary>
    private static string ResolveScanner(IConfiguration config, bool isProduction)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(config);
        services.AddKurxInfrastructure(config, isProduction);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IFileScanner>().GetType().Name;
    }

    /// <summary>An unrecognised value is still refused everywhere — the pre-existing behaviour this must
    /// not have disturbed.</summary>
    [Fact]
    public void An_unrecognised_scanner_is_refused_even_outside_production()
        => Assert.Throws<NotSupportedException>(() =>
            new ServiceCollection().AddKurxInfrastructure(Config("virustotal"), isProduction: false));

    /// <summary>Production-shaped configuration for everything the guards ahead of the scanner check —
    /// a non-dev password (line 32) and Redis (line 196). <c>SIGNING_KEY_PROTECTION</c> is left unset
    /// because it already defaults to <c>kms</c> under Production, and its AWS client is registered
    /// lazily, so nothing here reaches AWS.</summary>
    private static IConfiguration Config(string? scanner)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=kurx_scanner_cfg;Username=kurx;Password=not-the-committed-dev-password",
            ["REDIS_CONNECTION"] = "localhost:6379",
            ["JWT_SECRET"] = "a-unique-test-secret-that-is-definitely-long-enough-0123456789",
            ["TICKET_HMAC_SECRET"] = "a-unique-test-ticket-hmac-secret-0123456789-abcdef",
            ["OTP_PEPPER"] = "a-unique-test-otp-pepper-0123456789-abcdefghijklmnop",
            // D-355: STORAGE_PROVIDER=localdisk is refused under Production, and that guard runs BEFORE
            // this one — so without a durable provider configured here these tests would assert the
            // storage refusal instead of the scanner refusal they exist for. Not a weakening: it is the
            // rest of a valid Production configuration, so the only thing left under test is the scanner.
            ["STORAGE_PROVIDER"] = "s3",
            ["S3_BUCKET"] = "kurx-scanner-cfg-test",
            ["S3_REGION"] = "ap-south-1",
        };
        if (scanner is not null) values["FILE_SCANNER"] = scanner;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
