using Hangfire;
using Microsoft.Extensions.Logging;
using Hangfire.PostgreSql;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Configuration;
using Kurx.Infrastructure.Events;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Messaging;
using Kurx.Infrastructure.Orgs;
using Kurx.Infrastructure.Persistence;
using Kurx.Infrastructure.Providers;
using Kurx.Infrastructure.Chat;
using Kurx.Infrastructure.Orders;
using Kurx.Infrastructure.Ticketing;
using Kurx.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the DbContext, env-selected providers (see .env.example), and auth services.</summary>
    /// <param name="isProduction">When true, rejects a connection string still carrying the committed dev password.</param>
    public static IServiceCollection AddKurxInfrastructure(this IServiceCollection services, IConfiguration config,
        bool isProduction = false)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings__Default is not set");
        if (isProduction)
            SecretValidation.RequireProductionConnectionString("ConnectionStrings__Default", connectionString);

        // DB-2: pool and timeout behaviour is decided once, here, instead of being inherited from Npgsql's
        // defaults. See DatabaseOptions for why the default MaxPoolSize=100 is wrong against this
        // repository's own declared autoscaling range, and docs/deployment/CONNECTION_POOLING.md for the
        // arithmetic. API traffic and Hangfire get SEPARATE pools so neither can starve the other.
        var dbOptions = DatabaseOptions.FromConfiguration(config);
        var apiConnectionString = dbOptions.ApplyForApi(connectionString);
        var jobsConnectionString = dbOptions.ApplyForJobs(connectionString);
        Resolved["Database (api)"] = dbOptions.Describe(apiConnectionString);
        Resolved["Database (jobs)"] = dbOptions.Describe(jobsConnectionString)
            + $", {dbOptions.JobWorkerCount} workers";

        services.AddSingleton(dbOptions);
        services.AddDbContext<KurxDbContext>(o => o.UseNpgsql(apiConnectionString));

        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(jobsConnectionString)));
        // WorkerCount set explicitly: the default is min(ProcessorCount * 5, 20), which on a 2-vCPU
        // Fargate task is 10 workers contending for JobsMaxPoolSize connections plus Hangfire's own
        // polling and heartbeat connections — a self-inflicted pool timeout under normal operation.
        services.AddHangfireServer(o => o.WorkerCount = dbOptions.JobWorkerCount);

        // Background job classes — resolved by Hangfire's built-in DI activator.
        services.AddScoped<ExpireSeatHoldsJob>();
        services.AddScoped<InventoryReconciliationJob>();   // V3 §17.1 (Phase 7)
        services.AddScoped<WalletReconciliationJob>();      // D-240 — money's missing drift detector
        services.AddScoped<RegistrationReconciliationJob>();  // V3 §17.1 registration shadow (Phase 8)
        services.AddScoped<DatabaseHealthProbeJob>();       // DB-9 — deadlocks and bloat, which RDS does not publish
        services.AddScoped<ExpireWaitlistOffersJob>();
        services.AddScoped<CollectedToAvailableLedgerJob>();
        services.AddScoped<LeaderboardRefreshJob>();
        services.AddScoped<NotificationCleanupJob>();
        services.AddScoped<OutboxDispatchJob>();
        services.AddScoped<SearchIndexRefreshJob>();   // V3 §15 discovery ranking signals (Phase 16)
        services.AddScoped<PhoneE164BackfillJob>();
        services.AddScoped<SigningKeyMaintenanceJob>();
        services.AddScoped<EventReminderJob>();
        services.AddScoped<LockExpiredChatRoomsJob>();
        services.AddScoped<ChatNotificationJob>();
        services.AddScoped<ChatAttachmentCleanupJob>();
        services.AddScoped<PostMediaCleanupJob>();
        services.AddScoped<AccountDeletionJob>();
        services.AddScoped<CertificateBatchJob>();
        services.AddScoped<CertificateDeliveryJob>();

        // JwtOptions is registered by the host (Program.cs) so its production-strength
        // validation runs exactly once, using the host's IsProduction() check.
        services.AddSingleton<TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPlatformRoleService, PlatformRoleService>();
        services.AddScoped<IIdentityVerificationService, Identity.IdentityVerificationService>();
        // D-102 (M3a): the typed audit spine. NullCorrelationAccessor is the default for background jobs
        // and tests; the Api layer registers an HttpContext-backed accessor after this call, overriding it.
        services.AddScoped<IAuditWriter, Audit.AuditWriter>();
        services.AddScoped<ICorrelationAccessor, Audit.NullCorrelationAccessor>();

        services.AddScoped<IOrgService, OrgService>();
        services.AddScoped<IOrgUnitService, Orgs.OrgUnitService>();  // V3 §4.1 OrgUnit tree (Phase 4)
        services.AddScoped<IAudienceService, Audience.AudienceService>();  // V3 §4.4 audience/eligibility (Phase 5)
        services.AddScoped<IEventAuthority, Events.EventAuthorityService>();           // D-269 — the single event authorization source
        services.AddScoped<IEventPermissionService, Events.EventPermissionService>();  // V3 §5.4 (Phase 6)
        services.AddScoped<IParticipantService, Events.ParticipantService>();          // V3 §5 participants (Phase 6)
        services.AddScoped<IOrganizationRegistryService, OrganizationRegistryService>();
        services.AddScoped<IOrgVerificationService, OrgVerificationService>();
        services.AddScoped<IMembershipVerificationService, MembershipVerificationService>();
        services.AddScoped<IFraudService, Trust.FraudService>();
        // D-323 — identity-proof bypass for dev/test, off unless asked for and refused outright in
        // Production. Every proof it relaxes is currently mock-backed (MockKycProvider approves anything),
        // so enforcing them establishes nothing while blocking public-event and paid-checkout testing.
        // Fraud-clear is NOT in scope and stays enforced. Full reasoning: IdentityVerificationOptions.
        var identityBypassRequested = (config[Configuration.IdentityVerificationOptions.EnvKey] ?? "false")
            .Trim().ToLowerInvariant() is "true" or "1";
        if (identityBypassRequested && isProduction)
            throw new InvalidOperationException(
                $"{Configuration.IdentityVerificationOptions.EnvKey} is enabled in Production. This bypasses the " +
                "government-ID, PAN and bank-ownership proofs behind publishing a public event, organizing a paid " +
                "event and receiving a payout — the checks that keep the platform's name off an unverified " +
                "organizer and stop money settling to an unidentified person. It exists only because those proofs " +
                "are mock-backed outside Production. Unset it.");
        services.AddSingleton(identityBypassRequested
            ? new Configuration.IdentityVerificationOptions(true)
            : Configuration.IdentityVerificationOptions.Enforced);

        services.AddScoped<ITrustService, Trust.TrustService>();
        services.AddScoped<IAdminVerificationService, Admin.AdminVerificationService>();
        services.AddScoped<IReportService, Moderation.ReportService>();
        services.AddScoped<IUserAdminService, Admin.UserAdminService>();
        services.AddScoped<ISocialService, Social.SocialService>();
        services.AddScoped<IEventReviewService, Social.EventReviewService>();
        services.AddScoped<IOrgInvitationService, Orgs.OrgInvitationService>();
        services.AddScoped<IEventAssignmentService, Events.EventAssignmentService>();
        services.AddScoped<INotificationService, Notifications.NotificationService>();
        services.AddScoped<IInventoryService, Ticketing.InventoryService>();  // V3 §8 inventory pools (Phase 7)
        services.AddScoped<IWaitlistService, Ticketing.WaitlistService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IAttendeeService, AttendeeService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IKindService, KindService>();  // V3 Kind registry (Phase 1)
        services.AddScoped<ICapabilityService, CapabilityService>();
        services.AddScoped<IEventPolicyService, Kurx.Infrastructure.Events.EventPolicyService>();
        services.AddScoped<IEventAuthorizationService, Kurx.Infrastructure.Events.EventAuthorizationService>();  // D-266 M5
        services.AddScoped<IEventReviewChecklistService, Kurx.Infrastructure.Events.EventReviewChecklistService>();  // D-266 M7
        // D-266 M2 — organiser workspace surfaces come from every platform layer, not from the capability
        // catalog alone. Registration order is irrelevant: WorkspaceComposer sorts deterministically.
        services.AddScoped<IWorkspaceContributor, Kurx.Infrastructure.Events.InfrastructureWorkspaceContributor>();
        services.AddScoped<IWorkspaceContributor, Kurx.Infrastructure.Events.CommerceWorkspaceContributor>();
        services.AddScoped<IWorkspaceContributor, Kurx.Infrastructure.Events.CapabilityWorkspaceContributor>();
        services.AddScoped<Kurx.Infrastructure.Events.WorkspaceComposer>();  // V3 Capability registry (Phase 2)
        services.AddScoped<IVenueService, VenueService>();
        services.AddScoped<ISpeakerService, SpeakerService>();
        services.AddScoped<ISponsorService, SponsorService>();
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<ITicketTypeService, TicketTypeService>();
        services.AddScoped<ITicketTransferService, TicketTransferService>();
        services.AddScoped<IGateEntryService, GateEntryService>();
        services.AddScoped<IProfileVisibilityResolver, ProfileVisibilityResolver>();
        // Scoped, and memoised per user inside: one request that projects several profile sections
        // pays for one load (D-224). The derivation engines themselves are static pure functions over
        // the fact-set, so they need no registration at all.
        services.AddScoped<IProfileFactSetLoader, ProfileFactSetLoader>();
        services.AddScoped<IPublicProfileService, PublicProfileService>();
        services.AddScoped<IAllyService, AllyService>();
        services.AddScoped<IWhatsAppLogService, WhatsAppLogService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IPostService, Posts.PostService>();                             // D-262 social feed
        services.AddScoped<IAccountService, Users.AccountService>();                       // D-263 account settings
        services.AddScoped<IDmService, Chat.DmService>();                                  // D-264 direct messages
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IRefundService, Orders.RefundService>();
        services.AddScoped<ICouponService, Orders.CouponService>();   // D-265
        services.AddScoped<IEventRegistrationService, Orders.EventRegistrationService>();  // V3 §7 registration shadow (Phase 8)
        services.AddScoped<ITeamService, TeamService>();                                   // V3 §6 Team subsystem (Phase 10)
        services.AddScoped<ICompetitionService, Events.CompetitionService>();              // V3 §10 Competition engine (Phase 11)
        services.AddScoped<ISeriesService, Events.SeriesService>();                        // V3 §13.2 EventSeries (Phase 12)
        services.AddScoped<ISearchIndexService, Search.SearchIndexService>();              // V3 §15 discovery index projector (Phase 16)
        services.AddScoped<ISearchService, Search.SearchService>();                        // V3 §15 canonical discovery (Phase 16)
        services.AddScoped<IWalkInService, Events.WalkInService>();                        // V3 §7.6 walk-in (Phase 13)
        services.AddScoped<ISeatBlockService, Events.SeatBlockService>();                  // V3 §7.5 delegated/SeatBlock (Phase 13)
        services.AddScoped<IApprovalService, Events.ApprovalService>();                    // V3 §14.3 approval chains (Phase 14)
        // The certificate module (D-355). Scoped: it writes through the request's DbContext.
        services.AddScoped<ICertificateIdAllocator, Certificates.CertificateIdAllocator>();
        services.AddScoped<ICertificateTemplateService, Certificates.CertificateTemplateService>();
        services.AddScoped<ICertificateIssuingService, Certificates.CertificateIssuingService>();
        services.AddScoped<ICertificateBatchService, Certificates.CertificateBatchService>();
        services.AddScoped<ICertificateDeliveryService, Certificates.CertificateDeliveryService>();
        services.AddScoped<ICertificateRevocationService, Certificates.CertificateRevocationService>();
        services.AddScoped<ICertificateParticipantService, Certificates.CertificateParticipantService>();
        services.AddScoped<ICertificateAnalyticsService, Certificates.CertificateAnalyticsService>();
        services.AddScoped<ICertificateSigner, Certificates.CertificateSigner>();
        // Both readers registered; SpreadsheetService picks one by file extension. Adding a format
        // later is a registration here rather than a change to any caller.
        services.AddSingleton<ISpreadsheetReader, Certificates.CsvSpreadsheetReader>();
        services.AddSingleton<ISpreadsheetReader, Certificates.XlsxSpreadsheetReader>();
        services.AddSingleton<ISpreadsheetService, Certificates.SpreadsheetService>();
        services.AddScoped<ICertificateVerificationService, Certificates.CertificateVerificationService>();
        services.AddSingleton<ICertificateVerificationLinks, Certificates.CertificateVerificationLinks>();
        // Singleton like the other renderers: QuestPDF layout is stateless and pays a one-time
        // font-resolution cost on first use.
        services.AddSingleton<ICertificateDocumentRenderer, Certificates.CertificateDocumentRenderer>();
        services.AddScoped<IEntitlementService, Events.EntitlementService>();   // D-334
        services.AddScoped<Analytics.IAnalyticsFactSource, Analytics.LeafFactSource>();   // V3 §16 (Phase 17) internal fact source
        services.AddScoped<IAnalyticsService, Analytics.AnalyticsService>();
        services.AddScoped<IGamificationService, Gamification.GamificationService>();

        // Redis distributed cache (used for profile stats TTL cache)
        var redis = config["REDIS_CONNECTION"];
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(o => o.Configuration = redis);
        }
        else
        {
            // Fail closed in Production (ADR-AM14 — specified, previously never implemented). Falling back
            // to a per-process cache there is silently wrong rather than merely degraded: the SignalR
            // backplane loses cross-instance fan-out, presence disables itself, and the chat rate limiter
            // becomes per-instance, so N tasks serve N times the configured limit. All three look healthy.
            if (isProduction)
                throw new InvalidOperationException(
                    "REDIS_CONNECTION is not set. Production requires Redis: without it the SignalR backplane, " +
                    "presence, and the per-instance rate limiters are all silently incorrect under more than " +
                    "one instance. Set REDIS_CONNECTION, or run a single instance deliberately with it pointed " +
                    "at a local Redis.");
            services.AddDistributedMemoryCache();
        }

        // Provider flags: only the dev implementations exist so far; real ones
        // (ses / cloudapi / razorpay-sandbox / s3 / fcm) slot in behind the same switch.
        // Email (D-284). Same explicit-switch shape as push and SMS, because more than one implementation
        // now genuinely exists. Console stays the dev default; an unrecognised value still throws.
        var emailProvider = config["EMAIL_PROVIDER"] ?? "console";
        switch (emailProvider)
        {
            case "ses":
                services.AddSingleton<IEmailSender, SesEmailSender>();
                break;
            case "console":
                services.AddSingleton<IEmailSender, ConsoleEmailSender>();
                break;
            default:
                throw new NotSupportedException(
                    $"EMAIL_PROVIDER={emailProvider} is not supported; only 'console' or 'ses' are available.");
        }
        AddProvider<IWhatsAppSender, ConsoleWhatsAppSender>(services, config, "WHATSAPP_PROVIDER", "console");

        var pushProvider = config["PUSH_PROVIDER"] ?? "console";
        if (pushProvider == "firebase")
        {
            services.AddSingleton<IPushSender, FirebasePushSender>();
        }
        else if (pushProvider == "console")
        {
            services.AddSingleton<IPushSender, ConsolePushSender>();
        }
        else
        {
            throw new NotSupportedException($"PUSH_PROVIDER={pushProvider} is not supported; only 'console' or 'firebase' are available.");
        }

        // SMS provider (AM1, ADR-A5): console (dev, zero-cred) or AWS SNS (real; India DLT via message
        // attributes). Same env-flag shape as push; dormant-but-tested until SMS_PROVIDER=sns + creds.
        var smsProvider = config["SMS_PROVIDER"] ?? "console";
        switch (smsProvider)
        {
            case "sns":
                services.AddSingleton<ISmsProvider, SnsSmsProvider>();
                break;
            case "console":
                services.AddSingleton<ISmsProvider, ConsoleSmsProvider>();
                break;
            default:
                throw new NotSupportedException($"SMS_PROVIDER={smsProvider} is not supported; only 'console' or 'sns' are available.");
        }
        services.AddScoped<IOtpService, OtpService>();
        // Stateless and thread-safe: a singleton avoids re-allocating per request on a hot path that is
        // already deliberately expensive.
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        // Disposable-email detection (Phase 2). Singleton: it parses one configuration string once and
        // is then a pure lookup. Absent config makes it inert, which is the default.
        services.AddSingleton<DisposableEmailPolicy>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<ISecurityCenterService, SecurityCenterService>();
        services.AddScoped<IChallengeService, ChallengeService>();
        services.AddScoped<ITrustedDeviceService, TrustedDeviceService>();
        services.AddScoped<ITrustedBrowserService, TrustedBrowserService>();
        services.AddScoped<ILoginApprovalService, LoginApprovalService>();
        services.AddScoped<IRecoveryCodeService, RecoveryCodeService>();
        services.AddScoped<IStepUpService, StepUpService>();
        services.AddScoped<IRiskEngine, RiskEngine>();
        services.AddMemoryCache();
        // Signing-key protection at rest (AM10, D-102a).
        var keyProtection = config["SIGNING_KEY_PROTECTION"] ?? (isProduction ? "kms" : "none");
        switch (keyProtection)
        {
            case "kms":
                services.AddSingleton<Amazon.KeyManagementService.IAmazonKeyManagementService>(_ =>
                    new Amazon.KeyManagementService.AmazonKeyManagementServiceClient());
                services.AddSingleton<Auth.IKmsClient, Auth.AwsKmsClientAdapter>();
                services.AddSingleton<ISigningKeyProtector, Auth.KmsSigningKeyProtector>();
                break;
            case "none":
                // Fail closed: an unprotected private signing key in production is a complete
                // authentication bypass the moment the database leaks.
                if (isProduction)
                    throw new InvalidOperationException(
                        "SIGNING_KEY_PROTECTION=none is not permitted in Production. Private signing keys " +
                        "must be wrapped (set SIGNING_KEY_PROTECTION=kms and AWS_KMS_SIGNING_KEY_ID).");
                services.AddSingleton<ISigningKeyProtector, Auth.NullSigningKeyProtector>();
                break;
            default:
                throw new NotSupportedException(
                    $"SIGNING_KEY_PROTECTION={keyProtection} is not supported; use 'kms' or 'none'.");
        }

        services.AddScoped<ISigningKeyService, SigningKeyService>();
        services.AddSingleton<IAuthTelemetry, Telemetry.AuthTelemetry>();

        // Secrets provider (AM10, D-101a). Same env-flag shape as every other provider boundary.
        // `configuration` covers local dev AND any deployment that injects secrets as environment
        // variables (ECS task secrets, Kubernetes secrets) — it is a production answer, not a stub.
        var secretsProvider = config["SECRETS_PROVIDER"] ?? "configuration";
        switch (secretsProvider)
        {
            case "aws":
                services.AddSingleton<Amazon.SecretsManager.IAmazonSecretsManager>(_ =>
                    new Amazon.SecretsManager.AmazonSecretsManagerClient());
                services.AddSingleton<Secrets.ISecretsManagerClient, Secrets.AwsSecretsManagerClientAdapter>();
                services.AddSingleton<ISecretProvider, Secrets.AwsSecretsManagerProvider>();
                break;
            case "configuration":
                services.AddSingleton<ISecretProvider, Secrets.ConfigurationSecretProvider>();
                break;
            default:
                // Fail closed: an unrecognised value must not silently fall back to reading secrets
                // from somewhere the operator did not intend.
                throw new NotSupportedException(
                    $"SECRETS_PROVIDER={secretsProvider} is not supported; use 'configuration' or 'aws'.");
        }

        // WebAuthn / passkeys (AM3). ServerDomain is the RP ID — the registrable domain the passkey is
        // bound to; Origins is the exact allow-list the browser must have been on. Both default to local
        // dev values, and getting them wrong is what makes passkeys phishing-resistant, so they are
        // deliberately explicit config rather than derived from the request.
        services.AddFido2(options =>
        {
            options.ServerDomain = config["WEBAUTHN_RP_ID"] ?? "localhost";
            options.ServerName = config["WEBAUTHN_RP_NAME"] ?? "Kurx";
            options.Origins = (config["WEBAUTHN_ORIGINS"] ?? config["ALLOWED_ORIGINS"] ?? "http://localhost:3000")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet();
            options.TimestampDriftTolerance = 300_000;
        });
        services.AddScoped<IPasskeyService, PasskeyService>();

        AddProvider<IPaymentGateway, MockPaymentGateway>(services, config, "PAYMENT_PROVIDER", "mock");
        AddProvider<IRouteClient, MockRouteClient>(services, config, "PAYMENT_PROVIDER", "mock");
        AddProvider<IKycProvider, MockKycProvider>(services, config, "KYC_PROVIDER", "mock");
        // Certificate OCR (D-355 Phase 12). Deferred by decision: the editor places every field by hand and
        // must keep working with no engine at all. Registered through AddProvider precisely because that
        // helper REFUSES every value but the development one — so TEXT_DETECTOR=tesseract fails loudly at
        // boot rather than silently resolving to a detector that detects nothing.
        AddProvider<ITextDetector, Certificates.UnavailableTextDetector>(
            services, config, "TEXT_DETECTOR", "none");
        // Object storage (D-355 Phase 2). Hand-registered rather than through AddProvider, which exists
        // to REFUSE every value but the development one — there are now two real choices.
        switch ((config["STORAGE_PROVIDER"] ?? "localdisk").Trim().ToLowerInvariant())
        {
            case "localdisk":
                // Fails closed in Production, matching FILE_SCANNER=none (D-338). LocalDiskStorage writes
                // into the container's filesystem, which a redeploy destroys — so a production deployment
                // that simply forgot the variable would come up healthy, accept uploads, issue
                // certificates, and lose every stored document on the next release. Postgres would keep
                // the rows, which makes the loss look like corruption rather than a wipe: the certificate
                // still resolves to a storage key that now 404s.
                if (isProduction)
                    throw new InvalidOperationException(
                        "STORAGE_PROVIDER=localdisk is not permitted in Production. LocalDiskStorage is "
                        + "ephemeral container storage: uploaded designs and issued certificates would be "
                        + "destroyed by the next deploy while their database rows survived. Set "
                        + "STORAGE_PROVIDER=s3 and configure S3_BUCKET, S3_REGION and (for a non-AWS "
                        + "S3-compatible server) S3_ENDPOINT. See .env.example.");
                services.AddSingleton<IStorage, LocalDiskStorage>();
                Resolved[nameof(IStorage)] = "localdisk (LocalDiskStorage — not durable)";
                break;
            case "s3":
                // Validated here, at startup, so a missing bucket or an unparseable endpoint stops the
                // deploy rather than surfacing on the first upload.
                var s3 = S3StorageOptions.FromConfiguration(config);
                services.AddSingleton<IStorage, S3Storage>();
                Resolved[nameof(IStorage)] = s3.PublicEndpoint is { Length: > 0 }
                    ? $"s3 ({s3.Bucket} @ {s3.Endpoint}, browser URLs signed for {s3.PublicEndpoint})"
                    : $"s3 ({s3.Bucket}{(s3.Endpoint is null ? "" : " @ " + s3.Endpoint)})";
                break;
            default:
                throw new NotSupportedException(
                    $"STORAGE_PROVIDER={config["STORAGE_PROVIDER"]} is not implemented. Valid values are "
                    + "'localdisk' (development only — not durable) and 's3'. See .env.example.");
        }
        // Malware scanning (D-110, real provider D-298). `none` stays the default so a dev machine
        // needs no clamd, and it provides NO protection; `clamav` is the production value. Registered
        // by hand rather than through AddProvider because this is the one boundary with a real
        // implementation to choose, and AddProvider exists to REFUSE every value but the dev one.
        switch ((config["FILE_SCANNER"] ?? "none").Trim().ToLowerInvariant())
        {
            case "none":
                // Fail closed (D-338). Every other dangerous default in this file already throws in
                // Production — SIGNING_KEY_PROTECTION=none, IDENTITY_VERIFICATION_BYPASS, a missing
                // REDIS_CONNECTION — and this one did not, so a deployment that simply forgot the variable
                // accepted every upload unscanned and reported healthy. `.env.example` ships
                // FILE_SCANNER=none, which makes forgetting the default path rather than an oversight.
                if (isProduction)
                    throw new InvalidOperationException(
                        "FILE_SCANNER=none is not permitted in Production. NoOpFileScanner reports every " +
                        "file Clean without reading it, so attendee- and organizer-supplied uploads — " +
                        "including the government-ID and organization-proof documents staff open in the " +
                        "admin console — would be served unscanned. Set FILE_SCANNER=clamav and point " +
                        "CLAMAV_HOST/CLAMAV_PORT at a clamd.");
                services.AddSingleton<IFileScanner, NoOpFileScanner>();
                Resolved[nameof(IFileScanner)] = "none (NoOpFileScanner — NO protection)";
                break;
            case "clamav":
                var clamHost = config["CLAMAV_HOST"] ?? "localhost";
                var clamPort = int.TryParse(config["CLAMAV_PORT"], out var p) ? p : 3310;
                var clamTimeout = TimeSpan.FromSeconds(
                    int.TryParse(config["CLAMAV_TIMEOUT_SECONDS"], out var t) ? t : 30);
                services.AddSingleton(new ClamAvOptions(clamHost, clamPort, clamTimeout));
                services.AddSingleton<IFileScanner, ClamAvFileScanner>();
                Resolved[nameof(IFileScanner)] = $"clamav ({clamHost}:{clamPort})";
                break;
            default:
                throw new NotSupportedException(
                    $"FILE_SCANNER={config["FILE_SCANNER"]} is not implemented. Valid values are "
                    + "'none' (development only — no protection) and 'clamav'. See .env.example.");
        }

        // D-338 — the one malware gate for the six upload paths that are not chat or posts. Scoped, not a
        // singleton: it stages audit rows on the caller's unit of work, which is per-request.
        services.AddScoped<Providers.UploadScanGate>();

        // Presence (D-114). The ONE place that decides whether presence is available: Redis is the
        // authoritative shared store, and without it presence is cleanly disabled rather than faked
        // with process-local state that would be wrong under multi-instance. Callers ask
        // IPresenceService.IsEnabled; nothing else checks for Redis.
        if (!string.IsNullOrWhiteSpace(config["REDIS_CONNECTION"]))
            services.AddSingleton<IPresenceService, Presence.RedisPresenceService>();
        else
            services.AddSingleton<IPresenceService, Presence.PresenceDisabledService>();

        // Document generation providers. QrCodeGenerator and CertificateRenderer are real
        // (QRCoder / QuestPDF, D-035). DocumentRasterizerStub stays a stub — only needed for
        // custom uploaded-PDF templates, which are deferred.
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        AddProvider<IDocumentRasterizer, DocumentRasterizerStub>(services, config, "DOCUMENT_RASTERIZER", "stub");
        services.AddSingleton<ICertificateRenderer, CertificateRenderer>();
        // D-199: pays the renderer's one-time SkiaSharp/QuestPDF font-resolution cost (~26.7s, measured)
        // at startup instead of on a real user's first certificate request.
        services.AddHostedService<CertificateRendererWarmupService>();

        return services;
    }

    /// <summary>
    /// Registers the one implementation of a provider boundary that currently exists.
    /// </summary>
    /// <remarks>
    /// Selection is env-driven and <b>fails fast</b>: an unrecognised value throws at startup rather
    /// than silently falling back to the development implementation. Silently degrading is the
    /// dangerous option — a production deployment misconfigured as `STORAGE_PROVIDER=s3` would come
    /// up healthy while writing every upload to a container filesystem that disappears on redeploy.
    ///
    /// Adding a production implementation means: write the adapter, add a case here, document the
    /// variable. No consumer changes, because consumers only ever see <typeparamref name="TService"/>.
    /// </remarks>
    private static void AddProvider<TService, TDevImpl>(IServiceCollection services, IConfiguration config,
        string flag, string devValue)
        where TService : class where TDevImpl : class, TService
    {
        var value = config[flag] ?? devValue;
        if (value != devValue)
            throw new NotSupportedException(
                $"{flag}={value} is not implemented yet: no production implementation of " +
                $"{typeof(TService).Name} exists in this build. The only available value is " +
                $"'{devValue}' ({typeof(TDevImpl).Name}). See .env.example and " +
                "docs/architecture/providers.md.");

        services.AddSingleton<TService, TDevImpl>();
        Resolved[typeof(TService).Name] = $"{value} ({typeof(TDevImpl).Name})";
    }

    /// <summary>
    /// What each provider boundary resolved to, for the startup summary. Static because registration
    /// happens once per process, before any service provider exists to hold it.
    /// </summary>
    private static readonly Dictionary<string, string> Resolved = new();

    /// <summary>
    /// Logs the resolved provider set at startup.
    /// </summary>
    /// <remarks>
    /// Worth the log line: "which providers am I actually running?" is the first question during an
    /// incident, and the answer is otherwise spread across a dozen environment variables. Development
    /// implementations are logged at Warning so they cannot be mistaken for production ones in a
    /// deployed environment.
    /// </remarks>
    public static void LogProviderConfiguration(ILogger logger, IConfiguration config)
    {
        foreach (var (service, implementation) in Resolved.OrderBy(p => p.Key))
            logger.LogInformation("Provider {Service} -> {Implementation}", service, implementation);

        var development = new List<string>();
        if ((config["STORAGE_PROVIDER"] ?? "localdisk") == "localdisk") development.Add("storage (local disk — not durable)");
        if ((config["FILE_SCANNER"] ?? "none") == "none") development.Add("malware scanning (no-op — NO protection)");
        if ((config["PAYMENT_PROVIDER"] ?? "mock") == "mock") development.Add("payments (mock — signatures are not verified)");
        if ((config["KYC_PROVIDER"] ?? "mock") == "mock") development.Add("KYC (mock — checks always approve)");
        if ((config["EMAIL_PROVIDER"] ?? "console") == "console") development.Add("email (console — nothing is delivered)");
        if ((config["WHATSAPP_PROVIDER"] ?? "console") == "console") development.Add("WhatsApp (console — nothing is delivered)");
        if ((config["SMS_PROVIDER"] ?? "console") == "console") development.Add("SMS (console — nothing is delivered)");
        if ((config["PUSH_PROVIDER"] ?? "console") == "console") development.Add("push (console — nothing is delivered)");
        // D-323. Louder than the rest on purpose: the others degrade a side effect, this one opens a gate.
        if ((config[Configuration.IdentityVerificationOptions.EnvKey] ?? "false").Trim().ToLowerInvariant() is "true" or "1")
            development.Add("identity verification (BYPASSED — govt ID, PAN and bank proofs are not enforced; "
                          + "any account can publish a public event, organize paid and receive a payout)");

        if (development.Count > 0)
            logger.LogWarning("Development providers active: {Providers}", string.Join("; ", development));
    }
}
