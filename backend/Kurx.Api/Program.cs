using System.Security.Claims;
using System.Threading.RateLimiting;
using FluentValidation;
using Hangfire;
using Kurx.Api.Auth;
using Kurx.Api.Endpoints;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.HealthChecks;
using Kurx.Api.Hubs;
using Kurx.Api.Json;
using Kurx.Api.Middleware;
using Kurx.Api.Observability;
using Kurx.Api.OpenApi;
using Kurx.Api.RateLimiting;
using Kurx.Api.Realtime;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Kurx.Infrastructure;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Secrets;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;   // HubOptions.AddFilter<T>() (D-294)
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Enrichers.Span;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    // Structured logs carry the W3C trace/span id (D-100), so a log line found in CloudWatch links
    // straight to its trace instead of leaving an operator to correlate by timestamp.
    .Enrich.WithSpan()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

builder.Services.AddKurxInfrastructure(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddKurxTelemetry(builder.Configuration, builder.Environment);

// D-102 (M3a): audit rows carry the request's correlation id so the trail joins to logs and traces.
// Registered AFTER AddKurxInfrastructure so it overrides the null accessor that jobs/tests fall back to.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationAccessor, HttpContextCorrelationAccessor>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
// Localization: resource files live in Kurx.Infrastructure/Localization/SharedResources.{culture}.resx.
// Accept-Language header selects the culture; falls back to "en" (default).
builder.Services.AddLocalization();

// Validated once here (with the host's real IsProduction() check) and reused as a
// singleton for both JWT-bearer configuration and TokenService (docs/DECISIONS.md secrets policy).
// Resolve the signing secret through the secret provider (D-101a), not straight from configuration.
// This runs before the DI container exists, so the provider is built via the shared factory — the
// same selection logic DI uses. Reading IConfiguration here instead would silently bypass AWS
// Secrets Manager for the single most important secret in the system.
var bootstrapSecrets = SecretProviderFactory.Create(builder.Configuration);
var jwtSecret = await bootstrapSecrets.GetRequiredAsync("JWT_SECRET");
// Same provider as JWT_SECRET, for the same reason: reading TICKET_HMAC_SECRET from IConfiguration would
// bypass AWS Secrets Manager. It is a distinct key from the signing secret and Production refuses to boot
// without it, so gate tickets are never signed with the session key.
var ticketHmacSecret = await bootstrapSecrets.GetAsync("TICKET_HMAC_SECRET");
var jwt = JwtOptions.FromSecret(jwtSecret, builder.Configuration, builder.Environment.IsProduction(), ticketHmacSecret);
builder.Services.AddSingleton(jwt);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            // Accept BOTH signing generations during the cut-over (D-099): ES256 keys resolved live
            // from signing_keys by `kid`, and the legacy HS256 secret for tokens minted before this
            // deploy. Dropping HS256 outright would sign every current user out; it is removed once
            // no HS256 token can still be within its lifetime.
            IssuerSigningKey = jwt.SigningKey,
        };
        // Browser WebSocket/SSE transports used by SignalR can't set an Authorization header,
        // so the JS client sends the access token as a query string param instead (SignalR convention).
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
        };
    });

// ES256 key resolution (D-099). Configured here rather than inline above because the resolver needs
// the built service provider, which does not exist while the bearer options are being declared.
// A `kid` selects the exact key from signing_keys; a token with no `kid` is a legacy HS256 token and
// falls back to the shared secret, so both generations validate during the cut-over.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IServiceProvider>((options, serviceProvider) =>
    {
        options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
        {
            if (string.IsNullOrEmpty(kid)) return [jwt.SigningKey];      // legacy HS256

            // Synchronous by contract — JwtBearerOptions has no async resolver — so this cannot be
            // awaited. SigningKeyCacheWarmer (D-254) keeps the memory cache populated just inside its
            // TTL, which makes this a cache hit that completes synchronously rather than a blocking
            // database round-trip on a thread-pool thread.
            using var scope = serviceProvider.CreateScope();
            var keys = scope.ServiceProvider.GetRequiredService<ISigningKeyService>();
            var published = keys.GetValidationKeysAsync().GetAwaiter().GetResult();
            var match = published.FirstOrDefault(k => k.KeyId == kid);
            if (match is null) return [];    // unknown, retired or compromised kid => token rejected

            // D-355: parse once per key, not once per request. This line used to call ECDsa.Create()
            // inline and never dispose it — ECDsaSecurityKey does not take ownership — so every
            // authenticated request abandoned a native handle to the finalizer and re-ran the same
            // base64 decode and SPKI import. The lookup above stays the authority: a key that is no
            // longer published never reaches the cache, so retirement and compromise are unaffected.
            return [ValidationKeyCache.Get(match.KeyId, match.PublicKeySpki)];
        };
    });

// Redis backplane for SignalR multi-instance fan-out; also exposed as IConnectionMultiplexer
// for the ChatHub sliding-window rate limiter. Gracefully skipped when REDIS_CONNECTION is absent
// (single-instance dev mode — rate limiting falls back to always-allow in ChatHub).
// D-294: hub methods are invisible to UseRateLimiter, which only ever sees /negotiate. Without this
// filter every method on every hub is unthrottled once the socket is up.
var signalRBuilder = builder.Services.AddSignalR(o => o.AddFilter<HubRateLimitFilter>());
builder.Services.AddSingleton<HubRateLimitFilter>();
var redisConn = builder.Configuration["REDIS_CONNECTION"];
if (!string.IsNullOrWhiteSpace(redisConn))
{
    // D-253. Two bugs lived in the three lines this replaces. First, ConnectionMultiplexer.Connect() ran
    // during service REGISTRATION, so it blocked the boot thread and a Redis that was momentarily down
    // took the whole API down with it — with no retry, because the exception escaped before the host
    // existed. Second, AddStackExchangeRedis(connectionString) builds its own multiplexer, so the process
    // held two independent connection pools to the same server.
    //
    // AbortOnConnectFail=false is what turns "Redis is down at boot" into a degraded dependency that
    // reconnects in the background instead of a startup crash. The Lazy defers the connect to first use
    // and is thread-safe by default, and handing SignalR the same instance keeps it to ONE multiplexer.
    var redisOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConn);
    redisOptions.AbortOnConnectFail = false;
    var multiplexer = new Lazy<StackExchange.Redis.IConnectionMultiplexer>(
        () => StackExchange.Redis.ConnectionMultiplexer.Connect(redisOptions));

    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ => multiplexer.Value);
    signalRBuilder.AddStackExchangeRedis(o => o.ConnectionFactory = _ => Task.FromResult(multiplexer.Value));
}
builder.Services.AddSingleton<IRealtimeBroadcaster, SignalRBroadcaster>();

// Org-membership checks are resource-specific (which org, which role) and enforced per-request in
// OrgService against the DB (D-015). PLATFORM roles (SuperAdmin/VerificationReviewer/FinanceOps/
// Support) are also read live per request — PlatformRoleClaimsTransformation (D-040) loads them from
// platform_roles and strips any token-supplied claim, so authority is never trusted from the JWT and
// a revoked role stops working next request. "kurx_admin" is the live SuperAdmin claim the transform
// emits (kept for the existing per-endpoint IsAdmin helpers). SuperAdmin implies every policy.
builder.Services.AddScoped<IClaimsTransformation, PlatformRoleClaimsTransformation>();
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("KurxAdmin", p => p.RequireClaim(PlatformRoleClaimsTransformation.AdminClaim, "true"));
    o.AddPolicy("SuperAdmin", p => p.RequireClaim(PlatformRoleClaimsTransformation.PlatformRoleClaim, nameof(PlatformRole.SuperAdmin)));
    o.AddPolicy("VerificationReviewer", p => p.RequireClaim(PlatformRoleClaimsTransformation.PlatformRoleClaim,
        nameof(PlatformRole.SuperAdmin), nameof(PlatformRole.VerificationReviewer)));
    o.AddPolicy("FinanceOps", p => p.RequireClaim(PlatformRoleClaimsTransformation.PlatformRoleClaim,
        nameof(PlatformRole.SuperAdmin), nameof(PlatformRole.FinanceOps)));
    o.AddPolicy("Support", p => p.RequireClaim(PlatformRoleClaimsTransformation.PlatformRoleClaim,
        nameof(PlatformRole.SuperAdmin), nameof(PlatformRole.Support)));
    // Content moderation (D-059): reviewers and support both triage reports.
    o.AddPolicy("Moderation", p => p.RequireClaim(PlatformRoleClaimsTransformation.PlatformRoleClaim,
        nameof(PlatformRole.SuperAdmin), nameof(PlatformRole.VerificationReviewer), nameof(PlatformRole.Support)));
    // Audit log read (D-062): SuperAdmin + the read-only auditor role.
    o.AddPolicy("Audit", p => p.RequireClaim(PlatformRoleClaimsTransformation.PlatformRoleClaim,
        nameof(PlatformRole.SuperAdmin), nameof(PlatformRole.ReadOnlyAuditor)));
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    var correlationId = ctx.HttpContext.Items[CorrelationIdMiddleware.ItemKey] as string
        ?? ctx.HttpContext.TraceIdentifier;
    ctx.ProblemDetails.Extensions["correlationId"] = correlationId;
});

// AllowCredentials is load-bearing, not decoration: the login calls send `withCredentials` so the
// browser will store/return the httpOnly `kurx_tb` trusted-browser cookie. Without this header the
// browser drops the request *after* a successful preflight — the API sees OPTIONS 204 and no POST, and
// the user gets a generic "couldn't sign you in" with nothing in the server log to explain it. Safe here
// only because the origins are an explicit list; the spec forbids pairing credentials with AllowAnyOrigin.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins((builder.Configuration["ALLOWED_ORIGINS"] ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // D-251: go through IProblemDetailsService like every other error on the platform. Hand-writing the
    // JSON here is what made 429 the only response carrying neither `error` nor `correlationId`, breaking
    // the one error model in CLAUDE.md §2 — the service is also what applies CustomizeProblemDetails,
    // which is where correlationId comes from.
    o.OnRejected = async (ctx, ct) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
            .TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = ctx.HttpContext,
                ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "rate_limited",
                    Detail = "Rate limit exceeded. See the Retry-After header.",
                    Extensions = { ["error"] = "rate_limited" },
                },
            });
    };

    // Global per-user limiter — applies to every request automatically.
    // Authenticated: 300 req/min sliding window (50 req per 10-second segment).
    // Anonymous / unauthenticated: 60 req/min per IP (overridable via RATE_LIMIT_ANON_PER_MIN,
    // same pattern as RATE_LIMIT_OTP_PER_MIN below — production is unaffected unless set;
    // D-038 sets a generous test-only value so a growing AuthTests class doesn't trip this on
    // its own request volume, distinct from any actual abuse signal).
    var anonPerMin = int.TryParse(builder.Configuration["RATE_LIMIT_ANON_PER_MIN"], out var anonV) ? anonV : 60;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            // Every authenticated user buckets at the same 300/min. There is deliberately NO elevated
            // admin tier here (D-252): this delegate runs between UseAuthentication and UseAuthorization,
            // so PlatformRoleClaimsTransformation has not run and "kurx_admin" cannot be present — the
            // branch that used to test for it was unreachable and always took the 300 path. Making it
            // real would mean resolving platform roles from the database before the throttle, i.e. an
            // un-throttled DB read per request, which is precisely what the limiter exists to prevent.
            // Admin-console throughput gets dedicated per-endpoint limits when that need is measured.
            return RateLimitPartitions.Window(ctx, $"user:{userId}", 300, TimeSpan.FromMinutes(1));
        }
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartitions.Window(ctx, $"ip:{ip}", anonPerMin, TimeSpan.FromMinutes(1));
    });

    // OTP endpoints: coarse per-IP shield. Authoritative limits are in AuthService (D-005).
    var otpPerMin = int.TryParse(builder.Configuration["RATE_LIMIT_OTP_PER_MIN"], out var v) ? v : 20;
    // The "otp:" / "resume:" prefixes are load-bearing now that these share one Redis keyspace: in-process
    // each policy owned a separate limiter, so partitioning on a bare IP was safe. It no longer is — two
    // policies keyed on the same address would increment the same counter.
    o.AddPolicy("otp", ctx => RateLimitPartitions.Window(
        ctx, $"otp:{ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"}", otpPerMin, TimeSpan.FromMinutes(1)));

    // Resume PDF (D-228): anonymous-allowed and by far the most expensive public operation on the
    // platform — a full fact-set load plus a QuestPDF render. Partitioned by IP because the common
    // caller is unauthenticated, and kept low: a person downloads their own resume occasionally, so
    // anything above this rate is a scraper or an accident.
    o.AddPolicy("resume", ctx => RateLimitPartitions.Window(
        ctx, $"resume:{ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"}", 10, TimeSpan.FromMinutes(1)));

    // Post authoring (D-262): per-user, because the thing being bounded is how fast ONE account can
    // publish, and a shared IP (campus wifi, an office) is exactly where a per-IP cap would punish
    // everyone for one spammer. Generous enough that a burst of replies in a live thread never trips
    // it — this is an anti-flood shield, not a usage quota.
    var postsPerMin = int.TryParse(builder.Configuration["RATE_LIMIT_POSTS_PER_MIN"], out var ppm) ? ppm : 30;
    o.AddPolicy("posts", ctx =>
    {
        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        // Anonymous cannot reach these routes at all (RequireAuthorization), so the IP fallback is
        // only ever exercised by a request the auth middleware is about to reject anyway.
        var key = userId is not null ? $"posts:user:{userId}" : $"posts:ip:{ctx.Connection.RemoteIpAddress}";
        return RateLimitPartitions.Window(ctx, key, postsPerMin, TimeSpan.FromMinutes(1));
    });

    // Public certificate verification (D-355): per IP, because it is anonymous and certificate ids are
    // sequential — enumerable by design, since the mitigation for a guessable id is a minimal public
    // payload rather than secrecy. Generous enough that an organiser checking a stack of printed
    // certificates by hand never trips it, low enough that scraping the id space is not practical.
    var verifyPerMin = int.TryParse(builder.Configuration["RATE_LIMIT_VERIFY_PER_MIN"], out var vpm) ? vpm : 30;
    o.AddPolicy("verify", ctx => RateLimitPartitions.Window(
        ctx, $"verify:ip:{ctx.Connection.RemoteIpAddress}", verifyPerMin, TimeSpan.FromMinutes(1)));

    // Heavy endpoints (CSV import, bulk send, announcement fan-out): stricter concurrency cap.
    // Only 5 of these operations can run concurrently per user; excess requests are queued briefly.
    // Deliberately NOT distributed (D-255): this caps in-flight work on THIS instance, which is what
    // protects this instance's thread pool and memory. A concurrency limiter also has to release its
    // permit when the request ends, so a shared counter would leak permits on any instance that crashed
    // mid-request — strictly worse than a local cap that dies with the process that owned it.
    o.AddPolicy("heavy", ctx =>
    {
        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetConcurrencyLimiter($"heavy:{userId}", _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = 5,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 2,
        });
    });
});

// Real dependency checks: Postgres always, Redis only if a backplane is actually configured
// (D-001/.env.example — dev can run without one), storage only for the localdisk provider that's
// actually implemented today. Email/SMS are console/mock dev-only senders with no external
// dependency to probe, so there's nothing meaningful to health-check until a real provider ships.
// Every dependency check is tagged "ready" (D-299). Liveness must never test a dependency: an orchestrator
// restarts on a failed LIVENESS probe, so a database blip would take out every instance at once and hit the
// recovering database with a fleet of cold starts. Readiness pulls an instance out of the load balancer and
// puts it back — which is the correct response to a dependency being briefly unavailable.
var healthChecks = builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!, name: "postgres", tags: ["ready"]);

var redisConnection = builder.Configuration["REDIS_CONNECTION"];
if (!string.IsNullOrWhiteSpace(redisConnection))
    healthChecks.AddRedis(redisConnection, name: "redis", tags: ["ready"]);

if ((builder.Configuration["STORAGE_PROVIDER"] ?? "localdisk") == "localdisk")
    healthChecks.AddCheck<LocalDiskStorageHealthCheck>("storage", tags: ["ready"]);

// D-250: the strangler-window convergence passes, off the boot path. Registered here rather than in
// Infrastructure's DI module because this is the only consumer and the schedule lives below.
builder.Services.AddScoped<DataBackfillJob>();

// D-254: keeps the JWT validation key set in cache so the synchronous IssuerSigningKeyResolver above
// never blocks a request thread on a database read.
builder.Services.AddHostedService<SigningKeyCacheWarmer>();

// D-259 addendum: the 10 endpoint files that forward an Application record straight out of Results.Ok
// were emitting camelCase while the other 65 emit snake_case. This makes them conform. Write-only —
// request binding stays camelCase, which is what every client already sends.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new SnakeCaseResponseConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Kurx API", Version = "v1" });
    // D-259: without this the spec describes CLR shape only, so a request rule tightening (e.g.
    // CreateOrgBody.Type gaining an enum constraint) was invisible to the contract and to the drift gate.
    o.SchemaFilter<FluentValidationSchemaFilter>();
    // Must mirror SnakeCaseResponseConverter exactly — the converter applies the response convention at
    // runtime and Swashbuckle cannot see it, so without this the spec would advertise camelCase for
    // types the API emits as snake_case.
    o.SchemaFilter<SnakeCaseResponseSchemaFilter>();
    // Without this, Swashbuckle assumes every reference type is nullable and marks NOTHING required —
    // so the published contract asserted `nullable: true` on every string in the API (ChatRoomView.Status
    // is a non-nullable `string` and the spec said otherwise) and emitted an empty `required` on every
    // schema. A contract that misstates nullability is worse than one that omits it: a client written
    // to the spec defends against nulls the server never sends, and the two checks that would catch a
    // renamed or dropped field had nothing to read. This makes C#'s nullable annotations — which the
    // codebase already enforces — the source of truth for both.
    o.SupportNonNullableReferenceTypes();
    // Nullability and requiredness are two separate switches: the call above only stops Swashbuckle
    // claiming every string may be null. Without the second, `required` stays empty on every schema and
    // a validator has nothing to check a client's optionality against — the state that let
    // `can_organize_paid` be declared optional-with-a-default and never questioned. Swashbuckle's own
    // switch for it (`NonNullableReferenceTypesAsRequired`) landed in 6.7.0 and this project pins 6.6.2,
    // so the filter re-implements it. See the filter for why that beat bumping the package.
    o.SchemaFilter<RequiredFromNonNullableSchemaFilter>();
    // File downloads declare byte[], which Swashbuckle renders as `format: byte` — base64, in OpenAPI 3.0.
    // These endpoints stream raw bytes. Applied per response content type, so a byte[] inside a JSON body
    // keeps its (correct) base64 description. See the filter.
    o.OperationFilter<BinaryResponseSchemaFilter>();
    // D-313 described success bodies but left the error contract silent: five error responses declared
    // across 518 operations, against 634 error returns that all answer the same RFC7807 shape. Derived
    // from endpoint metadata (auth requirement, named policy, presence of a request body) rather than
    // written out per route — the error contract is uniform, so restating it 600 times would only give
    // it 600 chances to drift. See the filter for what it deliberately does NOT infer.
    o.OperationFilter<ProblemResponseOperationFilter>();
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
        }] = Array.Empty<string>(),
    });
});

var app = builder.Build();

// One line per provider boundary, plus a warning naming every development implementation still in
// play. "Which providers is this instance actually running?" is the first question in an incident.
Kurx.Infrastructure.DependencyInjection.LogProviderConfiguration(
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Kurx.Providers"),
    app.Configuration);

// Applying migrations also proves DB connectivity; a connection failure or a migration that
// can't run throws here and the host refuses to start, instead of serving traffic against a
// database it can't actually use.
using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
    try
    {
        // Fail closed before touching the database: a missing secret must stop the deploy, not
        // surface as an intermittent failure on the first login afterwards (D-101a).
        await RequiredSecrets.ValidateAsync(
            scope.ServiceProvider.GetRequiredService<ISecretProvider>(),
            startupLogger,
            app.Environment.IsProduction());

        startupLogger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync();
        startupLogger.LogInformation("Database migrations applied; connectivity confirmed.");
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Database migration/connectivity check failed at startup; refusing to start.");
        throw;
    }

    // D-355: one advisory lock around the whole seeding block, because every replica of a rolling deploy
    // runs it at once. EF Core 9 takes its own lock for MigrateAsync above, but that lock ends with the
    // migration and the seeders are outside it. They are read-then-insert — KindRegistrySeeder SELECTs the
    // existing slugs, diffs the catalog, then inserts the difference — so two replicas booting together
    // both observe the same slug missing and both insert it, and the unique index on event_kinds.Slug
    // turns the loser into a 23505 inside this try block, which refuses to start that replica. Narrow (it
    // needs a cold database or a deploy that adds catalog rows) but a bad failure: a boot crash mid-deploy.
    //
    // Serialising is the fix rather than making nine seeders individually conflict-tolerant: the hazard is
    // "N replicas run this block concurrently", not any one seeder, so the lock also covers seeders added
    // later. The second replica then finds everything present and its already-populated checks correctly
    // do nothing. Transaction-scoped, so it releases on commit or on throw — no leak if a seeder fails.
    // Same pg_advisory_xact_lock idiom as SeatBlockService and OrderService.
    const int seedLockNamespace = 0x53_45_45_44;    // "SEED"
    const int seedLockKey = 1;                      // one block, one key
    await using var seedTx = await db.Database.BeginTransactionAsync();
    await db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock({seedLockNamespace}, {seedLockKey})");

    // Reference-data seeders only (D-250). These write a fixed, bounded set of platform rows — the taxonomy,
    // the Kind/Capability/ParticipantRole registries, the system + design templates — that the API cannot
    // serve a single request without, so they stay on the boot path and stay fail-closed. Each is guarded by
    // an "already populated" check and is cheap on a warm database.
    //
    // The per-row CONVERGENCE passes that used to run here (backfilling KindSlug, capability sets,
    // participants, pools, the registration chain, teams, the search index) have moved to DataBackfillJob:
    // they scale with table size and every replica of a rolling deploy ran them over the same rows at once.
    // Ordering constraints are preserved inside that job.
    await Kurx.Infrastructure.Events.SystemTemplateSeeder.SeedAsync(db);
    await Kurx.Infrastructure.Events.EventTaxonomySeeder.SeedAsync(db);
    // After the taxonomy seeder — the archetype backfill reads the seeded Type rows. Unlike the taxonomy
    // seeder this one also runs on populated databases: the column is new, so existing types have nulls.
    await Kurx.Infrastructure.Events.ArchetypeSeeder.SeedAsync(db);
    // After the archetypes — the matrix rows key on archetype slugs.
    await Kurx.Infrastructure.Events.ArchetypeCapabilitySeeder.SeedAsync(db);
    // D-266 M3: per-type registration-policy allow-lists. After the archetype seeder — it derives from
    // the archetype each Type was just mapped to.
    await Kurx.Infrastructure.Events.RegistrationPolicySeeder.SeedAsync(db);
    // D-266 M2: workspace contributors must declare unique Order values and resolvable dependencies.
    // Validated here so a wiring mistake fails the deploy, not an organiser's page load.
    await scope.ServiceProvider.GetRequiredService<Kurx.Infrastructure.Events.WorkspaceComposer>().ValidateAsync();
    // After the taxonomy seeder — the 145 Kind aliases derive from the seeded Type rows.
    await Kurx.Infrastructure.Events.KindRegistrySeeder.SeedAsync(db);
    // After the Kind registry — the Kind×Capability defaults key on Kind slugs.
    await Kurx.Infrastructure.Events.CapabilityRegistrySeeder.SeedAsync(db);
    await Kurx.Infrastructure.Events.ParticipantRoleSeeder.SeedAsync(db);
    await Kurx.Infrastructure.Events.DesignTemplateSeeder.SeedAsync(db);

    // First-SuperAdmin bootstrap (D-274). No-op unless SUPERADMIN_BOOTSTRAP_PHONE is set AND the
    // platform has no SuperAdmin yet; it only grants the role to an account that already registered
    // through the real OTP flow, never creates one.
    await Kurx.Infrastructure.Auth.SuperAdminBootstrap.RunAsync(
        db,
        scope.ServiceProvider.GetRequiredService<IPlatformRoleService>(),
        app.Configuration,
        startupLogger);

    // Releases the advisory lock as well as publishing the rows. Anything thrown above leaves this
    // unreached, so the transaction rolls back and the next replica seeds from a clean state rather
    // than inheriting a half-seeded one — the startup catch already refuses to serve traffic either way.
    await seedTx.CommitAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
{
    // Runs at request completion, after routing/auth have populated User and route values,
    // so the one-line-per-request summary log also carries who/what/where for this request.
    diagnosticContext.Set("CorrelationId",
        httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? httpContext.TraceIdentifier);
    var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is not null) diagnosticContext.Set("UserId", userId);
    var orgId = httpContext.GetRouteValue("orgId");
    if (orgId is not null) diagnosticContext.Set("OrgId", orgId);
});
app.UseCors();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures("en", "hi")
    .AddSupportedUICultures("en", "hi"));
// Authentication must run before the rate limiter: the GlobalLimiter partitions by
// ctx.User's NameIdentifier claim (D-034), and that claim is only populated once JWT
// bearer authentication has run. With the old order every request — including ones
// carrying a valid Bearer token — was bucketed as anonymous (60 req/min shared per IP)
// instead of the intended 300 req/min per authenticated user.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Dashboard gated to dev; no auth needed for local debugging.
    app.UseHangfireDashboard("/hangfire");
}

// D-299 — three endpoints, one report writer.
//
//   /health/live   process only. NO dependency checks, by design. A failing liveness probe means "restart
//                  this container", and restarting an instance never fixes a database outage — it only adds
//                  a cold start to a system already struggling. This is what an orchestrator's liveness
//                  probe and the container healthcheck should point at.
//   /health/ready  dependencies. A failing readiness probe pulls the instance out of the load balancer and
//                  puts it back when the dependency returns — the correct, reversible response.
//   /health        everything, unchanged, for humans and dashboards.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthReportWriter.WriteAsync,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthReportWriter.WriteAsync,
});
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthReportWriter.WriteAsync });
app.MapAuthEndpoints();
app.MapPasswordEndpoints();
app.MapRegistrationEndpoints();
app.MapSecurityCenterEndpoints();
app.MapTrustedDeviceEndpoints();
app.MapTrustedBrowserEndpoints();
app.MapDeviceLoginEndpoints();
app.MapRecoveryEndpoints();
app.MapStepUpEndpoints();
app.MapPasskeyEndpoints();
app.MapPhoneMigrationEndpoints();
app.MapJwksEndpoints();
app.MapIdentityEndpoints();
app.MapOrgEndpoints();
app.MapAdminOrgEndpoints();
app.MapAdminFraudEndpoints();
app.MapAdminStaffEndpoints();
app.MapAdminEventEndpoints();
app.MapAdminDashboardEndpoints();
app.MapReportEndpoints();
app.MapAdminUserEndpoints();
app.MapAdminAuditEndpoints();
app.MapAdminAnalyticsEndpoints();
app.MapSocialEndpoints();
app.MapEventReviewEndpoints();
app.MapOrgInvitationEndpoints();
app.MapEventAssignmentEndpoints();
app.MapMeNotificationEndpoints();
app.MapWaitlistEndpoints();
app.MapMembershipClaimEndpoints();
app.MapEventEndpoints();
app.MapCategoryEndpoints();
app.MapKindEndpoints();
app.MapCapabilityEndpoints();
app.MapAudienceEndpoints();
app.MapParticipantEndpoints();
app.MapInventoryEndpoints();
app.MapEventRegistrationEndpoints();
app.MapTeamEndpoints();
app.MapCompetitionEndpoints();
app.MapSeriesEndpoints();
app.MapDelegatedRegistrationEndpoints();
app.MapApprovalEndpoints();
app.MapVenueEndpoints();
app.MapSpeakerEndpoints();
app.MapSponsorEndpoints();
app.MapScheduleEndpoints();
app.MapMediaEndpoints();
app.MapTemplateEndpoints();
app.MapTicketTypeEndpoints();
app.MapTicketTransferEndpoints();
app.MapTicketQrEndpoints();
app.MapCertificateTemplateEndpoints();
app.MapCertificateVerificationEndpoints();
app.MapSpreadsheetEndpoints();
app.MapCertificateBatchEndpoints();
app.MapCertificateDeliveryEndpoints();
app.MapCertificateRevocationEndpoints();
app.MapCertificateParticipantEndpoints();
// D-362 — organizer-only event badge printing. No holder-facing route by design.
app.MapIdCardEndpoints();
app.MapCertificateAnalyticsEndpoints();
app.MapGateEndpoints();
app.MapPublicProfileEndpoints();
app.MapAllyEndpoints();
app.MapWebhookEndpoints();
app.MapInvitationEndpoints();
app.MapAnnouncementEndpoints();
app.MapEntitlementEndpoints();
app.MapChatEndpoints();
app.MapPostEndpoints();
app.MapAccountEndpoints();   // D-263 account settings
app.MapDmEndpoints();        // D-264 direct messages
// Receiver for LocalDiskStorage presigned URLs (D-110). Self-disables for real object storage,
// where clients PUT straight to the bucket.
app.MapStorageEndpoints();
app.MapOrderEndpoints();
app.MapWalletEndpoints();
app.MapRefundEndpoints();
app.MapCouponEndpoints();   // D-265
// The dead duplicate KycEndpoints/IKycService/KycService were deleted in M9 (D-048). Org bank/PAN
// verification lives in OrgEndpoints (/v1/orgs/{orgId}/kyc/*); person identity KYC is /v1/me/identity (M3).
app.MapHub<ScanHub>("/hubs/scan");
app.MapHub<SalesHub>("/hubs/sales");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<NotificationHub>("/hubs/notifications");
// Anonymous by design (AM9): the waiting sign-in screen has no session yet — the poll token authorizes
// the subscription inside the hub. See LoginHub.
app.MapHub<LoginHub>("/hubs/login");

app.MapDeviceEndpoints();
app.MapAnalyticsEndpoints();
app.MapGamificationEndpoints();

// Register recurring background jobs.
var jobs = app.Services.GetRequiredService<IRecurringJobManager>();
jobs.AddOrUpdate<ExpireSeatHoldsJob>(
    "expire-seat-holds",
    job => job.RunAsync(CancellationToken.None),
    Cron.Minutely());
jobs.AddOrUpdate<ExpireWaitlistOffersJob>(
    "expire-waitlist-offers",
    job => job.RunAsync(CancellationToken.None),
    "*/5 * * * *");
jobs.AddOrUpdate<CollectedToAvailableLedgerJob>(
    "ledger-collected-to-available",
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily());
jobs.AddOrUpdate<InventoryReconciliationJob>(       // V3 §17.1 inventory drift alerting (Phase 7)
    "inventory-reconciliation",
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily());
jobs.AddOrUpdate<WalletReconciliationJob>(         // D-240 — wallet vs ledger drift alerting
    "wallet-reconciliation",
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily());
jobs.AddOrUpdate<RegistrationReconciliationJob>(    // V3 §17.1 registration shadow drift alerting (Phase 8)
    "registration-reconciliation",
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily());
// DB-9 — samples pg_stat_database/pg_stat_user_tables for the two signals RDS PostgreSQL does not publish
// to CloudWatch (deadlocks, bloat). Every 5 minutes: fast enough that the deadlock alarm's "sustained over
// three periods" window is under 20 minutes, slow enough that two cheap catalog reads are not a load source.
// A Hangfire recurring job rather than a hosted service precisely because Hangfire already guarantees one
// runner across every replica — N replicas each sampling one shared counter would multiply it by N.
jobs.AddOrUpdate<DatabaseHealthProbeJob>(
    "database-health-probe",
    job => job.RunAsync(CancellationToken.None),
    "*/5 * * * *");
jobs.AddOrUpdate<LeaderboardRefreshJob>(
    "leaderboard-refresh",
    job => job.RunAsync(CancellationToken.None),
    Cron.Hourly());
jobs.AddOrUpdate<SearchIndexRefreshJob>(               // V3 §15 discovery ranking signals (Phase 16)
    "search-index-refresh",
    job => job.RunAsync(CancellationToken.None),
    "*/15 * * * *");                                    // every 15 minutes — velocity/conversion + RECURRING primary
jobs.AddOrUpdate<NotificationCleanupJob>(
    "notification-cleanup",
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily());
jobs.AddOrUpdate<EventReminderJob>(
    "event-reminder",
    job => job.RunAsync(CancellationToken.None),
    Cron.Hourly());
// Security alerts must not wait an hour (AM8, ADR-AM16) — drain the outbox every minute.
// Retire signing keys past their grace period (D-099). Retirement only, never rotation — see the job.
jobs.AddOrUpdate<SigningKeyMaintenanceJob>(
    "signing-key-maintenance",
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily());
jobs.AddOrUpdate<OutboxDispatchJob>(
    "outbox-dispatch",
    job => job.RunAsync(CancellationToken.None),
    Cron.Minutely());
// Chat rooms go read-only 7 days after the event ends (D-104). Cancellation and archive lock
// immediately from EventService.TransitionAsync; this sweep catches events that merely ended.
jobs.AddOrUpdate<LockExpiredChatRoomsJob>(
    "lock-expired-chat-rooms",
    job => job.RunAsync(CancellationToken.None),
    Cron.Hourly());
// Attachments outlive their message only until this runs: deleted-message objects and uploads that
// were never attached to a message are both swept here (D-110).
jobs.AddOrUpdate<ChatAttachmentCleanupJob>(
    "chat-attachment-cleanup",
    job => job.RunAsync(CancellationToken.None),
    Cron.Hourly());
// Same sweep for post media, for the same reason: PostMedia.PostId is nullable because confirm happens
// before the post exists, so anything never claimed would otherwise sit in storage forever (D-262).
jobs.AddOrUpdate<PostMediaCleanupJob>(
    "post-media-cleanup",
    job => job.RunAsync(CancellationToken.None),
    Cron.Hourly());
// Anonymises accounts past their 30-day grace window (D-263). Without it, "scheduled deletion" is a
// date nothing ever acts on — the DPDP obligation behind the feature would go unmet in silence.
jobs.AddOrUpdate<AccountDeletionJob>(
    "account-deletion",
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily());
// Drains queued certificate emails (D-355, Phase 8). Minutely: the queue is written the moment an
// organiser presses send, and a certificate arriving a minute later is fine — one arriving an hour later
// looks broken.
jobs.AddOrUpdate<CertificateDeliveryJob>(
    "certificate-delivery",
    job => job.RunAsync(CancellationToken.None),
    Cron.Minutely());
// V3 strangler-window convergence (D-250), formerly inline at boot. Hourly is the safety net; the
// trigger below is what keeps deploy-time convergence — it ENQUEUES, so the host finishes starting
// while a worker does the scan, and DisableConcurrentExecution keeps concurrent replicas off each
// other. Idempotent, so an extra run costs one no-op pass.
jobs.AddOrUpdate<DataBackfillJob>(
    "data-backfill",
    job => job.RunAsync(CancellationToken.None),
    Cron.Hourly());
jobs.Trigger("data-backfill");

app.Run();

public partial class Program; // WebApplicationFactory hook for integration tests
