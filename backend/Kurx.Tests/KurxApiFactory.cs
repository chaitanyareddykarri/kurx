using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Orgs;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Kurx.Tests;

/// <summary>Captures everything sent to a phone number — SMS <b>and</b> WhatsApp — so tests can read the OTP
/// the user would receive.
///
/// <para>One capture for both channels on purpose (D-281). The delivery channel is now the server's decision
/// rather than the caller's, and a test asserting a login flow should not have to know or care which rail
/// carried the code: it cares that the user received one. Splitting the capture per channel would make every
/// login helper in the suite re-encode a policy decision that lives in <c>OtpChannelPolicy</c>.</para></summary>
public class CapturingPhoneSender : IWhatsAppSender, ISmsProvider
{
    private readonly List<(string Phone, string Text)> _sent = new();

    public string Name => "capturing";

    public Task SendTextAsync(string phone, string text, CancellationToken ct = default)
    {
        lock (_sent) _sent.Add((phone, text));
        return Task.CompletedTask;
    }

    public Task SendMediaAsync(string phone, string mediaUrl, string caption, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<SmsSendResult> SendAsync(string e164Phone, string message,
        SmsSendOptions? options = null, CancellationToken ct = default)
    {
        lock (_sent) _sent.Add((e164Phone, message));
        return Task.FromResult(new SmsSendResult(true, $"test-{Guid.NewGuid():N}"));
    }

    public string LastOtpFor(string phone)
    {
        lock (_sent)
        {
            var text = _sent.Last(m => m.Phone.EndsWith(phone[^10..])).Text;
            return Regex.Match(text, @"\d{6}").Value;
        }
    }
}

/// <summary>Captures push notifications so tests can assert a notification was actually attempted.
///
/// <para>Needed because "the outbox row says Dispatched" and "the user was told" are different claims, and
/// conflating them is exactly the defect the outbox regression tests exist to prevent.</para></summary>
public class CapturingPushSender : IPushSender
{
    private readonly List<(string Token, string Title, string Body, IReadOnlyDictionary<string, string>? Data)> _sent = new();

    public Task SendAsync(string fcmToken, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        lock (_sent) _sent.Add((fcmToken, title, body, data));
        return Task.CompletedTask;
    }

    /// <summary>Everything pushed to this token, newest last.</summary>
    public List<(string Token, string Title, string Body, IReadOnlyDictionary<string, string>? Data)> SentTo(string token)
    {
        lock (_sent) return _sent.Where(m => m.Token == token).ToList();
    }

    public void Clear() { lock (_sent) _sent.Clear(); }
}

/// <summary>Records security telemetry so tests can assert an undeliverable message is observable, not just
/// logged. A silent failure that emits no signal is indistinguishable from success to an operator.</summary>
public class CapturingAuthTelemetry : IAuthTelemetry
{
    private readonly List<(string Type, string Severity)> _events = new();

    public IDisposable StartOperation(string operation, Guid? userId = null) => new NoScope();
    public void RecordAuthAttempt(string method, string outcome) { }
    public void RecordFailure(string reason) { }

    public void RecordSecurityEvent(string type, string severity)
    {
        lock (_events) _events.Add((type, severity));
    }

    public bool Recorded(string type, string? severity = null)
    {
        lock (_events) return _events.Any(e => e.Type == type && (severity is null || e.Severity == severity));
    }

    public void Clear() { lock (_events) _events.Clear(); }

    private sealed class NoScope : IDisposable { public void Dispose() { } }
}

/// <summary>Captures outbound emails so tests can read the OTP the user would receive.</summary>
public class CapturingEmailSender : IEmailSender
{
    private readonly List<(string To, string Html)> _sent = new();

    /// <summary>Everything sent, with the parts a delivery test needs — subject line and attachments —
    /// which the OTP capture above has no use for.</summary>
    public record SentEmail(string To, string Subject, string Html, IReadOnlyList<EmailAttachment> Attachments);

    private readonly List<SentEmail> _messages = new();

    /// <summary>Makes the next <paramref name="count"/> sends throw, for exercising retry and
    /// give-up behaviour against a provider having a bad minute.</summary>
    public void FailNext(int count) { lock (_sent) _failuresRemaining = count; }
    private int _failuresRemaining;

    public IReadOnlyList<SentEmail> Messages { get { lock (_sent) return _messages.ToList(); } }

    public IReadOnlyList<SentEmail> MessagesTo(string to)
    {
        lock (_sent) return _messages.Where(m => m.To.Equals(to, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public void Clear() { lock (_sent) { _sent.Clear(); _messages.Clear(); _failuresRemaining = 0; } }

    public Task<string?> SendAsync(string to, string subject, string htmlBody,
        IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken ct = default)
    {
        lock (_sent)
        {
            if (_failuresRemaining > 0)
            {
                _failuresRemaining--;
                throw new InvalidOperationException("Simulated provider outage.");
            }
            _sent.Add((to, htmlBody));
            _messages.Add(new SentEmail(to, subject, htmlBody, attachments ?? []));
        }
        return Task.FromResult<string?>("provider-" + Guid.NewGuid().ToString("N")[..12]);
    }

    public string LastOtpFor(string email)
    {
        lock (_sent)
        {
            var html = _sent.Last(m => m.To.Equals(email, StringComparison.OrdinalIgnoreCase)).Html;
            return Regex.Match(html, @"\d{6}").Value;
        }
    }
}

/// <summary>Boots the API against a per-class database with capturing WhatsApp + email senders.</summary>
public class KurxApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public KurxApiFactory() => Current = this;

    /// <summary>The one capture for anything sent to a phone. <see cref="WhatsApp"/> and <see cref="Sms"/>
    /// are the same object — the channel is the server's choice (D-281), so a test reads "what did this
    /// number receive" rather than "what did this rail send".</summary>
    public CapturingPhoneSender Phone { get; } = new();

    public CapturingPhoneSender WhatsApp => Phone;
    public CapturingPhoneSender Sms => Phone;
    public CapturingEmailSender Email { get; } = new();
    public CapturingPushSender Push { get; } = new();
    public CapturingAuthTelemetry Telemetry { get; } = new();
    public RecordingBackgroundJobClient BackgroundJobs { get; } = new();

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    // WebApplicationFactory's synchronous Dispose() doesn't wait for hosted services (Hangfire's
    // BackgroundJobServer, worker count 20) to fully drain — it signals shutdown and returns while
    // those workers are still mid-flight against kurx_test. Because IClassFixture<KurxApiFactory>
    // gives every test class its own factory, xUnit tears this one down right before the next
    // class's constructor calls ResetDatabase(); if the old workers haven't actually stopped yet,
    // their reconnect attempts race the DROP/CREATE DATABASE and fail with "does not exist" /
    // aborted-connection errors that surface as unrelated test failures in the next class.
    // Implementing xUnit's IAsyncLifetime makes xUnit await this DisposeAsync (not just the
    // synchronous Dispose) before moving on, so we explicitly stop every hosted service here first.
    async Task IAsyncLifetime.DisposeAsync()
    {
        // NOTE: WebApplicationFactory.Server is a getter that STARTS the host if it isn't running. By the
        // time a factory is disposed, the next test class may already have dropped/recreated kurx_test, so
        // merely *probing* Server here can boot a host against a database that no longer matches and throw
        // — which surfaces as a cascade of unrelated failures across the suite. Draining is best-effort by
        // design, so the whole probe is guarded: if there's no live host to drain, there's nothing to do.
        try
        {
            if (Server is not null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                foreach (var hosted in Services.GetServices<IHostedService>())
                {
                    try { await hosted.StopAsync(cts.Token); }
                    catch { /* best-effort drain before teardown */ }
                }
            }
        }
        catch { /* host was never started, or its database is already gone — nothing to drain */ }

        await base.DisposeAsync();

        // Drop this class's private database so postgres does not accumulate one per class across runs.
        // Best-effort: a leftover kurx_test_* is harmless and a later run just uses a fresh name.
        try
        {
            NpgsqlConnection.ClearAllPools();
            using var admin = new NpgsqlConnection(AdminCs);
            admin.Open();
            using var cmd = admin.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE)";
            cmd.ExecuteNonQuery();
        }
        catch { /* best-effort cleanup */ }
    }

    // Each test class (IClassFixture → one factory) gets its OWN database, so one class dropping its
    // database in teardown can never race another class's migration on a shared one. This eliminates the
    // whole family of cross-class DB-lifecycle failures (57P01 / 42P01 / 3D000 / 42701) that a single
    // shared kurx_test suffered under load, at the cost of one migration per class (D-128 hardening).
    // Host port of the test Postgres. Defaults to 5432, which is what the documented container path
    // (sharing the postgres container's network namespace) requires — so that path is unchanged.
    // Overridable via KURX_TEST_PG_PORT for a machine whose 5432 is already held by another cluster,
    // which otherwise fails every test with 28P01 against the wrong server.
    private static readonly string PgPort = Environment.GetEnvironmentVariable("KURX_TEST_PG_PORT") ?? "5432";
    private static readonly string AdminCs =
        $"Host=localhost;Port={PgPort};Database=kurx;Username=kurx;Password=kurx";
    private readonly string _dbName = "kurx_test_" + Guid.NewGuid().ToString("N")[..16];
    private string TestCs => $"Host=localhost;Port={PgPort};Database={_dbName};Username=kurx;Password=kurx";

    // A single migrated template database, built once per run. Every per-class database is a fast
    // CREATE DATABASE ... TEMPLATE copy of it, so the ~50-migration schema is applied once per run instead
    // of once per class — which is the difference between a ~6-minute suite and an 80-minute one.
    // Per-PROCESS unique, so a concurrent test run (e.g. another workstream's suite) creating its own
    // template can never DROP/recreate ours mid-clone — the shared name was the one remaining cross-process
    // collision point.
    private static readonly string TemplateDb = "kurx_test_tmpl_" + Guid.NewGuid().ToString("N")[..8];
    private static readonly object TemplateLock = new();
    private static bool _templateReady;

    private static void EnsureTemplate()
    {
        if (_templateReady) return;
        lock (TemplateLock)
        {
            if (_templateReady) return;
            using (var admin = new NpgsqlConnection(AdminCs))
            {
                admin.Open();
                using var cmd = admin.CreateCommand();
                cmd.CommandText = $"DROP DATABASE IF EXISTS \"{TemplateDb}\" WITH (FORCE)";
                cmd.ExecuteNonQuery();
                cmd.CommandText = $"CREATE DATABASE \"{TemplateDb}\"";
                cmd.ExecuteNonQuery();
            }
            // Migrate the template to head once with a bare context — no host boot needed.
            var opts = new DbContextOptionsBuilder<KurxDbContext>()
                .UseNpgsql($"Host=localhost;Port={PgPort};Database={TemplateDb};Username=kurx;Password=kurx")
                .Options;
            using (var ctx = new KurxDbContext(opts))
                ctx.Database.Migrate();
            // CREATE DATABASE ... TEMPLATE fails if any session is connected to the template.
            NpgsqlConnection.ClearAllPools();
            _templateReady = true;
        }
    }

    /// <summary>Serialises <see cref="ResetDatabase"/> across factories in this process. Cross-class
    /// parallelisation is already disabled (AssemblyInfo), so this only guards the overlap between a
    /// class starting up and the previous one still tearing down.</summary>
    private static readonly object ResetLock = new();

    /// <summary>Postgres states that mean "another teardown or creation is racing us", not "the code
    /// under test is broken". Retrying these is correct; retrying anything else would hide real bugs.</summary>
    private static bool IsDatabaseLifecycleRace(PostgresException ex) =>
        ex.SqlState is "55006"      // database is being accessed by other users
                    or "42P04"      // database already exists
                    or "23505"      // duplicate key in pg_database catalog
                    or "3D000";     // database does not exist

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", TestCs);
        // Pin the JWT secret so CI's JWT_SECRET env var doesn't override appsettings.Development.json,
        // which would break AdminToken() in EventTests (both must sign/verify with the same secret).
        builder.UseSetting("JWT_SECRET", "dev-only-secret-change-me-0123456789abcdef");
        // OtpService resolves this through ISecretProvider with no fallback (D-115), so the test host
        // must supply it exactly as a real deployment does.
        builder.UseSetting("OTP_PEPPER", "dev-only-otp-pepper-change-me");
        builder.UseSetting("RATE_LIMIT_OTP_PER_MIN", "10000"); // service-level DB limits are what tests assert
        builder.UseSetting("RATE_LIMIT_ANON_PER_MIN", "10000"); // a growing test class shares one fake IP (D-038)
        builder.UseSetting("RATE_LIMIT_OTP_IP_PER_HOUR", "10000"); // AuthService's own per-IP OTP cap (D-038)
        builder.UseSetting("RATE_LIMIT_POSTS_PER_MIN", "10000"); // a posts test class authors far more than a human would (D-262)
        // DB-2 pool sizing for the suite, which has the OPPOSITE shape to production. Every test class owns
        // its own database (see _dbName above), and Npgsql pools per connection string — so the suite holds
        // one pool PER CLASS, not one per process. Against the container's max_connections=100 the
        // production defaults (20 + 8, MinPoolSize=2) would have a handful of overlapping factories exhaust
        // the SERVER, producing 53300 failures that look like test flakiness and are not.
        //
        // MinPoolSize=0 is the load-bearing one: a warm minimum means every pool ever created holds
        // connections open whether or not that class is still running. No test needs more than a few
        // connections — the widest concurrency assertion in the suite fires 6 parallel requests.
        builder.UseSetting("Database:MaxPoolSize", "8");
        builder.UseSetting("Database:JobsMaxPoolSize", "4");
        builder.UseSetting("Database:JobWorkerCount", "2");
        builder.UseSetting("Database:MinPoolSize", "0");
        // 15s, not lower: Npgsql refuses an idle lifetime below its 10s connection-pruning interval, and
        // DatabaseOptions now fails at startup rather than at the first connection when that happens.
        builder.UseSetting("Database:ConnectionIdleLifetimeSeconds", "15");
        // Pin the WebAuthn relying party (AM3) so SoftwareAuthenticator's rpIdHash/origin match whatever
        // ALLOWED_ORIGINS happens to be in the environment.
        builder.UseSetting("WEBAUTHN_RP_ID", "localhost");
        builder.UseSetting("WEBAUTHN_ORIGINS", "https://localhost");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWhatsAppSender>();
            services.AddSingleton<IWhatsAppSender>(WhatsApp);
            // D-281 routes every phone OTP to SMS (OtpChannelPolicy.For → OtpChannel.Sms; WhatsApp is
            // deliberately never selected for authentication). Without this replacement the code goes to the
            // real provider, CapturingPhoneSender captures nothing, and LastOtpFor throws "Sequence contains
            // no matching element" from the constructor of every test class that logs in — which is almost
            // all of them. CapturingPhoneSender already implements ISmsProvider for exactly this reason.
            services.RemoveAll<ISmsProvider>();
            services.AddSingleton<ISmsProvider>(Sms);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
            // Push and telemetry are captured so the outbox regression suite can assert that a
            // notification was actually attempted and that an undeliverable one is observable — neither
            // is provable from the outbox row's own status.
            services.RemoveAll<IPushSender>();
            services.AddSingleton<IPushSender>(Push);
            services.RemoveAll<IAuthTelemetry>();
            services.AddSingleton<IAuthTelemetry>(Telemetry);

            // Record enqueued Hangfire jobs rather than running them. The host registers a real
            // Hangfire server, so an executing job holds connections to kurx_test and races the
            // drop/re-migrate the next test class performs. Tests invoke job bodies explicitly.
            services.RemoveAll<Hangfire.IBackgroundJobClient>();
            services.AddSingleton<Hangfire.IBackgroundJobClient>(BackgroundJobs);

            // ...and stop the SERVER too, because the stub above does not cover recurring jobs.
            //
            // `Program.cs` registers ~18 recurring jobs through IRecurringJobManager. Those never pass
            // through IBackgroundJobClient: Hangfire's own scheduler enqueues them straight into storage
            // and this host's workers execute them — against the test's data, while the test is running.
            // That is a real defect, not a theoretical one. It was diagnosed from
            // `23505: duplicate key value violates unique constraint "IX_registrations_OrderId"` in
            // RefundLedgerTests, where RegistrationReconciliationJob inserted the order's registration
            // row a moment before the test inserted its own; the same race moved wallet ledger totals
            // under WalletConcurrencyTests and mutated rows between the two reads that
            // ReconciliationEquivalenceTests compares. All three passed alone and failed under the full
            // suite, because only a loaded run is slow enough for a scheduled job to land mid-test.
            //
            // Removing the hosted service is the honest fix: no assertion is relaxed, and nothing here
            // is under test — every job that IS tested is resolved and invoked directly. It also removes
            // the 20 workers whose draining the teardown below has to fight.
            var hangfireHosted = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && (ImplementationTypeOf(d)?.FullName ?? "").StartsWith("Hangfire", StringComparison.Ordinal))
                .ToList();
            // Fail loudly rather than silently doing nothing if Hangfire changes how it registers the
            // server — a filter that quietly matches zero descriptors would restore the flakiness while
            // looking fixed.
            if (hangfireHosted.Count == 0)
                throw new InvalidOperationException(
                    "Expected a Hangfire IHostedService to remove; Hangfire's registration shape changed. "
                    + "Recurring jobs would run during tests again — fix this filter rather than deleting it.");
            foreach (var descriptor in hangfireHosted) services.Remove(descriptor);
        });
    }

    /// <summary>Drops and re-migrates kurx_test. Call once per test class.</summary>
    public void ResetDatabase()
    {
        // Drop and recreate kurx_test BEFORE accessing Services (which starts WebApplicationFactory).
        //
        // Root cause of "42P01: relation hangfire.lock does not exist" in CI:
        //   Accessing Services triggers EnsureServer() → runs Program.cs startup →
        //   GetRequiredService<IRecurringJobManager>() initialises the PostgreSqlStorage
        //   singleton which runs PrepareSchemaIfNecessary, creating the hangfire schema.
        //   The old EnsureDeleted() then drops the entire database including that schema.
        //   Migrate() recreates only EF tables; the already-initialised singleton never
        //   retries PrepareSchemaIfNecessary → hangfire.lock is queried and 42P01 is thrown.
        //
        // Fix: reset the database via a direct admin connection first. When Services is
        // accessed afterward, Program.cs sees a fresh database and MigrateAsync() +
        // GetRequiredService<IRecurringJobManager>() both succeed on the correct database
        // in the correct order.
        // Build the migrated template once, then clone it per class (fast copy instead of re-migrating).
        EnsureTemplate();

        var adminCs = AdminCs;

        // The old sequence was "terminate connections, then DROP" — two statements with a gap between
        // them. That gap is the whole bug: xUnit disposes the *previous* class's factory asynchronously,
        // so its Hangfire workers (20 of them) reconnect to kurx_test in that window and the DROP then
        // fails with 55006, or the CREATE collides as 42P04/23505 and every test in the class fails on
        // infrastructure rather than on its own assertions.
        //
        // DROP DATABASE ... WITH (FORCE) (PostgreSQL 13+) terminates the sessions and drops the database
        // as one operation, so there is no window to lose. The retry is belt-and-braces for the residual
        // case where a teardown is still in flight when we arrive; the lock keeps two resets in this
        // process from interleaving.
        lock (ResetLock)
        {
            const int maxAttempts = 6;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var admin = new NpgsqlConnection(adminCs);
                    admin.Open();
                    using var cmd = admin.CreateCommand();
                    cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = $"CREATE DATABASE \"{_dbName}\" TEMPLATE \"{TemplateDb}\"";
                    cmd.ExecuteNonQuery();
                    break;
                }
                catch (PostgresException ex) when (attempt < maxAttempts && IsDatabaseLifecycleRace(ex))
                {
                    Thread.Sleep(250 * attempt);
                }
            }
        }

        // Npgsql pools physical connections per connection string at the process level, shared
        // across every KurxApiFactory instance (i.e. every test class) that connects to kurx_test.
        // The DROP DATABASE above invalidates any connections a previous test class's factory left
        // in that pool; without clearing it, the next Services access can be handed one of those
        // now-dead sockets and fail with "connection aborted" / "relation ... does not exist" mid
        // migration. Clear it here so this factory always opens fresh connections to the new DB.
        using (var probe = new NpgsqlConnection(TestCs))
            NpgsqlConnection.ClearPool(probe);

        // Starting the factory now: Program.cs MigrateAsync() runs against the fresh
        // kurx_test (creating EF tables), then GetRequiredService<IRecurringJobManager>()
        // initialises PostgreSqlStorage (creating the hangfire schema) — in that order,
        // with nothing wiped between them.
        using var _ = Services.CreateScope();
    }

    // ── D-074/D-075 event-first test fixtures ────────────────────────────────
    // Institutions are no longer self-minted via POST /v1/orgs (now personal-only). Two fixtures replace
    // the old "POST /v1/orgs then use it" pattern:
    //   • SeedVerifiedOrg — the fast path most tests want: a Verified org the user already represents/owns
    //     (mirrors what admin approval materializes), seeded directly so events publish and search finds it.
    //   • CreateVerifiedOrgAsync — drives the real HTTP flow (representation request → admin approve) for
    //     tests that exercise the flow itself.

    /// <summary>Seeds a Verified org the given user manages (default Owner), plus wallet + T1 payout —
    /// the post-approval end state — so fixture tests get a usable, searchable, publishable org directly.</summary>
    public Guid SeedVerifiedOrg(Guid managerUserId, string name, OrgRole role = OrgRole.Owner,
        OrganizationType type = OrganizationType.College, string? primaryDomain = null,
        OrgVerificationStatus status = OrgVerificationStatus.Verified)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        var org = new Organization
        {
            Name = name,
            Slug = $"{slug}-{Guid.NewGuid():N}"[..Math.Min(slug.Length + 9, 40)],
            Type = type,
            PrimaryDomain = primaryDomain,
            NormalizedName = OrganizationRegistryService.Normalize(name),
            IsPersonal = false,
            VerificationStatus = status,
        };
        org.CanonicalOrgId = org.Id;
        db.Organizations.Add(org);
        db.Memberships.Add(new Membership { OrgId = org.Id, UserId = managerUserId, Role = role, IsVerified = true });
        db.OrganizationWallets.Add(new OrganizationWallet { OrgId = org.Id });
        db.PayoutSchedules.Add(new PayoutSchedule
        {
            OrgId = org.Id, Tier = 1,
            AdvancePct = OrgService.Tier1AdvancePct, CapPaise = OrgService.Tier1CapPaise, ReservePct = OrgService.Tier1ReservePct,
        });
        db.SaveChanges();
        return org.Id;
    }

    /// <summary>D-266 M5 — seeds an APPROVED institutional authorization for an event, the fixture
    /// counterpart of <see cref="SeedVerifiedOrg"/>.
    ///
    /// <para>Every Public event representing a non-personal organization now carries the
    /// <c>event_authorization_required</c> publish blocker until an approved authorization exists, so a
    /// fixture whose subject is something else entirely (orders, certificates, search…) needs one line of
    /// setup to reach Published. The alternative — exempting seeded orgs — would mean the rule was never
    /// exercised by the suite that publishes most often.</para>
    ///
    /// <para>Writes the row directly rather than driving the API: the file-then-review round trip is
    /// <see cref="EventAuthorizationDocumentTests"/>'s subject, and re-running it in eighty fixtures would
    /// test it eighty times while making every one of them slower and harder to read.</para></summary>
    public void SeedApprovedEventAuthorization(Guid eventId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        if (db.EventAuthorizations.Any(a => a.EventId == eventId)) return;

        var submittedBy = db.Events.Where(e => e.Id == eventId).Select(e => e.CreatedBy).First();
        db.EventAuthorizations.Add(new EventAuthorization
        {
            EventId = eventId,
            SubmittedBy = submittedBy,
            HeadName = "Fixture Head",
            HeadDesignation = "Principal",
            OfficialEmail = "head@fixture.test",
            OfficialPhone = "+919700000000",
            RepresentativeRole = "Principal",
            LetterheadDocumentKey = $"events/{eventId}/authorization/fixture",
            Status = EventAuthorizationStatus.Approved,
            ReviewedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    /// <summary>Seeds a Verified org managed by whoever <paramref name="client"/> is authenticated as
    /// (user id read from the bearer token's <c>sub</c>). Drop-in for the old "POST /v1/orgs then use it".</summary>
    /*
     * D-379 — the factory the currently-running test class is using.
     *
     * `CreateEventRequest.CreateEventAsync` is an HttpClient extension with no way to reach a DbContext,
     * but every event it creates now needs its authorization APPROVED before the event can publish —
     * 214 tests, none of them about who approves a letter. This hook lets that one helper do it without
     * threading a factory through 80-odd call sites.
     *
     * Safe ONLY because cross-class parallelisation is disabled (see testing-standards.md): each class
     * has its own `kurx_test_<guid>` database, so a static pointing at the wrong factory would approve
     * in the wrong database. One class runs at a time, so `Current` is always the running one.
     */
    internal static KurxApiFactory? Current { get; private set; }

    /// <summary>D-379 — approve an event's authorization, as a reviewer would.
    ///
    /// <para>`PolicyResolver` raises `event_authorization_required` as a PUBLISH blocker for every event
    /// now, and it reads <c>Status == Approved</c> — filing a letter is the organiser's act, approving it
    /// is the reviewer's. Tests that publish directly are not about that review, so they take the
    /// reviewer's decision from here rather than staging an admin to make it.</para></summary>
    public void ApproveEventAuthorization(Guid eventId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var auth = db.EventAuthorizations.FirstOrDefault(a => a.EventId == eventId);
        if (auth is null) return;
        auth.Status = EventAuthorizationStatus.Approved;
        db.SaveChanges();
    }

    public Guid SeedVerifiedOrgForClient(HttpClient client, string name, string type = "College",
        string? primaryDomain = null, OrgVerificationStatus status = OrgVerificationStatus.Verified)
    {
        var t = Enum.TryParse<OrganizationType>(type, ignoreCase: true, out var parsed) ? parsed : OrganizationType.Other;
        return SeedVerifiedOrg(UserIdFromClient(client), name, OrgRole.Owner, t, primaryDomain, status);
    }

    private static Guid UserIdFromClient(HttpClient client)
    {
        var jwt = client.DefaultRequestHeaders.Authorization?.Parameter
            ?? throw new InvalidOperationException("client has no bearer token");
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    private HttpClient? _reviewerClient;
    private readonly SemaphoreSlim _reviewerLock = new(1, 1);

    /// <summary>A cached platform VerificationReviewer client, for approving representation requests.</summary>
    public async Task<HttpClient> ReviewerClientAsync()
    {
        await _reviewerLock.WaitAsync();
        try
        {
            if (_reviewerClient is not null) return _reviewerClient;
            const string phone = "9998887777";
            var c = CreateClient();
            await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
            var code = WhatsApp.LastOtpFor(phone);
            var tok = await (await c.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }))
                .Content.ReadFromJsonAsync<JsonElement>();
            c.DefaultRequestHeaders.Authorization = new("Bearer", tok.GetProperty("access_token").GetString());
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
                .GrantAsync(tok.GetProperty("user_id").GetGuid(), PlatformRole.VerificationReviewer, grantedBy: null);
            return _reviewerClient = c;
        }
        finally { _reviewerLock.Release(); }
    }

    private HttpClient? _secondReviewerClient;
    private readonly SemaphoreSlim _secondReviewerLock = new(1, 1);

    /// <summary>A SECOND platform reviewer, distinct from <see cref="ReviewerClientAsync"/>. Needed for the
    /// claim-ownership rules (D-266 M4), which are only expressible with two different reviewers: one
    /// holding an item and another attempting to decide it.</summary>
    public async Task<HttpClient> SecondReviewerClientAsync()
    {
        await _secondReviewerLock.WaitAsync();
        try
        {
            if (_secondReviewerClient is not null) return _secondReviewerClient;
            const string phone = "9998886666";
            var c = CreateClient();
            await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
            var code = WhatsApp.LastOtpFor(phone);
            var tok = await (await c.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }))
                .Content.ReadFromJsonAsync<JsonElement>();
            c.DefaultRequestHeaders.Authorization = new("Bearer", tok.GetProperty("access_token").GetString());
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
                .GrantAsync(tok.GetProperty("user_id").GetGuid(), PlatformRole.VerificationReviewer, grantedBy: null);
            return _secondReviewerClient = c;
        }
        finally { _secondReviewerLock.Release(); }
    }

    /// <summary>Runs the real event-first flow: the submitter files a representation request, then a
    /// platform reviewer approves it (materializing the org into the registry + verifying the rep).</summary>
    public async Task<Guid> CreateVerifiedOrgAsync(HttpClient submitter, string name,
        string type = "College", string? primaryDomain = null)
    {
        var submit = await submitter.PostAsJsonAsync("/v1/orgs/representation-requests", new
        {
            name, type, primaryDomain,
            documents = new[] { new { docType = "registration_cert", storageKey = "private/verif/reg.pdf" } },
        });
        if (submit.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"representation request failed: {submit.StatusCode} {await submit.Content.ReadAsStringAsync()}");
        var orgId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var reviewer = await ReviewerClientAsync();
        var approve = await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review",
            new { decision = "approve", reasonCode = "test" });
        if (approve.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"approve failed: {approve.StatusCode} {await approve.Content.ReadAsStringAsync()}");
        return orgId;
    }

    /// <summary>A descriptor's implementation type, whether it was registered by type or by factory.
    /// Hangfire registers its server through a factory in some versions, and a type-only check silently
    /// misses it.</summary>
    private static Type? ImplementationTypeOf(ServiceDescriptor d) =>
        d.ImplementationType
        ?? d.ImplementationInstance?.GetType()
        ?? d.ImplementationFactory?.Method.ReturnType;
}
