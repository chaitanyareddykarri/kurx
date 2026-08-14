using Kurx.Api.ExceptionHandling;
using Kurx.Infrastructure.Configuration;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Kurx.Tests;

/// <summary>DB-2: connection-pool and timeout configuration.
///
/// <para><b>What is proven here and what is not.</b> These tests prove the MECHANISM: the configured
/// ceilings actually reach the connection, pool exhaustion fails cleanly instead of hanging, a command
/// timeout is enforced, and the four failure modes are told apart correctly. They do NOT prove that any
/// particular MaxPoolSize is the right number for production — that requires load against real RDS, which
/// does not exist for this repository (infra/terraform is unapplied). See
/// docs/deployment/CONNECTION_POOLING.md for which claims are measured and which remain open.</para></summary>
public class DatabasePoolTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public DatabasePoolTests(KurxApiFactory factory) => _factory = factory;

    private const string BaseCs = "Host=localhost;Database=kurx;Username=kurx;Password=kurx";

    // ── Configuration reaches the connection ────────────────────────────────────────────────────

    [Fact]
    public void Defaults_are_explicit_and_never_the_npgsql_library_defaults()
    {
        var applied = new NpgsqlConnectionStringBuilder(new DatabaseOptions().ApplyForApi(BaseCs));

        // The whole point of DB-2: none of these may be left to Npgsql, whose MaxPoolSize default of 100
        // per process is what put the deployment's ceiling at ~2.3x the database's capacity.
        Assert.Equal(20, applied.MaxPoolSize);
        Assert.Equal(2, applied.MinPoolSize);
        Assert.Equal(30, applied.CommandTimeout);
        Assert.Equal(15, applied.Timeout);
        Assert.Equal(300, applied.ConnectionIdleLifetime);
        Assert.NotEqual(100, applied.MaxPoolSize);   // the default this exists to replace
    }

    [Fact]
    public void Api_and_jobs_get_separate_pools()
    {
        var options = new DatabaseOptions();
        var api = new NpgsqlConnectionStringBuilder(options.ApplyForApi(BaseCs));
        var jobs = new NpgsqlConnectionStringBuilder(options.ApplyForJobs(BaseCs));

        // Npgsql pools per connection string. A DIFFERING Application Name is the mechanism that gives
        // background work its own pool — if these ever match, the two collapse into one shared pool and
        // Hangfire can starve request traffic without anything else changing.
        Assert.Equal("kurx-api", api.ApplicationName);
        Assert.Equal("kurx-jobs", jobs.ApplicationName);
        Assert.NotEqual(api.ConnectionString, jobs.ConnectionString);
        Assert.Equal(options.MaxPoolSize, api.MaxPoolSize);
        Assert.Equal(options.JobsMaxPoolSize, jobs.MaxPoolSize);
    }

    [Fact]
    public void An_operator_supplied_value_is_never_overridden()
    {
        // These are defaults, not a policy an operator cannot escape. Silently rewriting an explicit
        // "Maximum Pool Size=40" would make the connection string a lie about what the process does.
        var applied = new NpgsqlConnectionStringBuilder(
            new DatabaseOptions().ApplyForApi(BaseCs + ";Maximum Pool Size=40;Command Timeout=90"));

        Assert.Equal(40, applied.MaxPoolSize);
        Assert.Equal(90, applied.CommandTimeout);
        Assert.Equal(2, applied.MinPoolSize);   // still defaulted — only the stated ones are honoured
    }

    [Fact]
    public void Configuration_overrides_are_read_without_a_source_change()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:MaxPoolSize"] = "7",
            ["Database:CommandTimeoutSeconds"] = "11",
        }).Build();

        var options = DatabaseOptions.FromConfiguration(config);
        Assert.Equal(7, options.MaxPoolSize);
        Assert.Equal(11, options.CommandTimeoutSeconds);
        Assert.Equal(2, options.MinPoolSize);   // untouched keys keep their default
    }

    [Fact]
    public void A_pool_floor_above_its_ceiling_fails_at_startup_with_a_message_that_names_both()
    {
        var options = new DatabaseOptions { MaxPoolSize = 4, MinPoolSize = 9 };
        var ex = Assert.Throws<InvalidOperationException>(() => options.ApplyForApi(BaseCs));
        Assert.Contains("MinPoolSize", ex.Message);
        Assert.Contains("MaxPoolSize", ex.Message);
    }

    /// <summary>Npgsql rejects an idle lifetime below its pruning interval, but only when the data source is
    /// built — i.e. on the first connection, so the symptom is a failing request rather than a failing
    /// deploy. Caught at startup instead, which is where a configuration error belongs.</summary>
    [Fact]
    public void An_idle_lifetime_below_the_pruning_interval_fails_at_startup_not_at_first_connection()
    {
        var options = new DatabaseOptions { ConnectionIdleLifetimeSeconds = 3 };
        var ex = Assert.Throws<InvalidOperationException>(() => options.ApplyForApi(BaseCs));
        Assert.Contains("pruning interval", ex.Message);
    }

    [Theory]
    [InlineData("Database:MaxPoolSize", "not-a-number")]
    [InlineData("Database:MaxPoolSize", "0")]
    [InlineData("Database:CommandTimeoutSeconds", "-5")]
    public void A_nonsense_configured_value_fails_loudly_rather_than_silently_defaulting(string key, string value)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();
        Assert.Throws<InvalidOperationException>(() => DatabaseOptions.FromConfiguration(config));
    }

    // ── Failure classification ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Server_connection_limit_is_never_reported_as_our_pool_being_exhausted()
    {
        // 53300 means POSTGRES refused: every replica together exceeded the server. The remedy is to LOWER
        // the per-task ceiling. Reporting it as PoolExhausted would send an operator to raise it, making the
        // outage worse — which is the entire reason these two are separate values.
        var serverLimit = BuildPostgresException("53300");
        Assert.Equal(DatabaseFailureKind.ServerConnectionLimit, DatabaseFailureClassifier.Classify(serverLimit));
        Assert.Equal("server_connection_limit", DatabaseFailureClassifier.TelemetryReason(
            DatabaseFailureClassifier.Classify(serverLimit)));
    }

    [Theory]
    [InlineData("57014", DatabaseFailureKind.CommandTimeout)]      // query_canceled
    [InlineData("55P03", DatabaseFailureKind.LockTimeout)]         // lock_not_available
    [InlineData("57P01", DatabaseFailureKind.Unavailable)]         // admin_shutdown
    public void Server_error_codes_map_to_their_operational_meaning(string sqlState, DatabaseFailureKind expected)
        => Assert.Equal(expected, DatabaseFailureClassifier.Classify(BuildPostgresException(sqlState)));

    [Fact]
    public void An_ordinary_constraint_violation_is_not_dressed_up_as_an_infrastructure_failure()
    {
        // 23505 is a bug or a business conflict. Returning 503 "database busy" for it would hide a real
        // defect behind a retry prompt.
        Assert.Equal(DatabaseFailureKind.None, DatabaseFailureClassifier.Classify(BuildPostgresException("23505")));
        Assert.Equal(DatabaseFailureKind.None, DatabaseFailureClassifier.Classify(new InvalidOperationException("nope")));
        Assert.Equal(DatabaseFailureKind.None, DatabaseFailureClassifier.Classify(null));
    }

    [Fact]
    public void Pool_exhaustion_is_recognised_and_outranks_the_timeout_it_surfaces_as()
    {
        // Npgsql reports pool exhaustion as a timeout. Classifying it as CommandTimeout would send an
        // operator hunting a slow query when the answer is the pool ceiling.
        var exhausted = new NpgsqlException(
            "The connection pool has been exhausted, either raise 'Max Pool Size' (currently 20) or "
            + "'Timeout' (currently 15 seconds)", new TimeoutException());
        Assert.Equal(DatabaseFailureKind.PoolExhausted, DatabaseFailureClassifier.Classify(exhausted));
    }

    [Fact]
    public void A_command_timeout_is_recognised_through_the_wrapper()
        => Assert.Equal(DatabaseFailureKind.CommandTimeout,
            DatabaseFailureClassifier.Classify(new NpgsqlException("reading from stream", new TimeoutException())));

    [Fact]
    public void Capacity_failures_are_reported_to_the_caller_without_naming_the_ceiling()
    {
        // Pool and server exhaustion share ONE client-facing code on purpose: the caller's action is
        // identical (retry), and telling an anonymous caller where saturation begins hands them the shape
        // of a cheap denial of service. The operator distinction lives in the telemetry tag instead.
        Assert.Equal("database_busy", DatabaseFailureClassifier.ErrorCode(DatabaseFailureKind.PoolExhausted));
        Assert.Equal("database_busy", DatabaseFailureClassifier.ErrorCode(DatabaseFailureKind.ServerConnectionLimit));
        Assert.NotEqual(
            DatabaseFailureClassifier.TelemetryReason(DatabaseFailureKind.PoolExhausted),
            DatabaseFailureClassifier.TelemetryReason(DatabaseFailureKind.ServerConnectionLimit));
    }

    // ── Live behaviour against real Postgres ────────────────────────────────────────────────────

    /// <summary>Saturation must FAIL, not hang. A pool with no ceiling and no timeout blocks a request
    /// thread indefinitely, which is the failure mode that makes exhaustion look like a hung service rather
    /// than a capacity problem.</summary>
    [Fact]
    public async Task Pool_exhaustion_fails_cleanly_within_the_connection_timeout()
    {
        var cs = new DatabaseOptions { MaxPoolSize = 2, MinPoolSize = 0, ConnectionTimeoutSeconds = 2 }
            .ApplyForApi(BaseCs.Replace("Database=kurx", "Database=kurx") + ";Application Name=kurx-pooltest");

        await using var held1 = new NpgsqlConnection(cs);
        await using var held2 = new NpgsqlConnection(cs);
        await held1.OpenAsync();
        await held2.OpenAsync();   // pool is now at its ceiling

        await using var third = new NpgsqlConnection(cs);
        var started = DateTime.UtcNow;
        var ex = await Assert.ThrowsAsync<NpgsqlException>(() => third.OpenAsync());
        var waited = DateTime.UtcNow - started;

        Assert.Equal(DatabaseFailureKind.PoolExhausted, DatabaseFailureClassifier.Classify(ex));
        // Bounded by the configured timeout rather than waiting forever. The generous upper bound keeps this
        // from being a timing-sensitive test on a loaded CI box.
        Assert.InRange(waited.TotalSeconds, 1, 30);
    }

    /// <summary>A command that outlives CommandTimeout is cancelled rather than holding its connection
    /// indefinitely, and surfaces as a classifiable failure rather than an opaque one.</summary>
    [Fact]
    public async Task A_command_that_exceeds_its_timeout_is_cancelled_and_classified()
    {
        var cs = new DatabaseOptions { MaxPoolSize = 2, MinPoolSize = 0, CommandTimeoutSeconds = 1 }
            .ApplyForApi(BaseCs + ";Application Name=kurx-timeouttest");

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT pg_sleep(10)", conn);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => cmd.ExecuteScalarAsync());
        Assert.Equal(DatabaseFailureKind.CommandTimeout, DatabaseFailureClassifier.Classify(ex));
    }

    /// <summary>No connection leak: repeatedly opening and disposing scopes must not grow the pool past its
    /// ceiling. A leak shows up here as a pool-exhaustion throw partway through the loop.</summary>
    [Fact]
    public async Task Repeated_scopes_do_not_leak_connections()
    {
        for (var i = 0; i < 60; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Users.AsNoTracking().Take(1).CountAsync();
        }
    }

    /// <summary>Builds a PostgresException carrying a given SQLSTATE. Its public constructor takes the
    /// server fields positionally; going through it keeps the test honest about the shape the classifier
    /// receives at runtime rather than asserting against a stand-in type.</summary>
    private static PostgresException BuildPostgresException(string sqlState) =>
        new(messageText: "test", severity: "ERROR", invariantSeverity: "ERROR", sqlState: sqlState);
}
