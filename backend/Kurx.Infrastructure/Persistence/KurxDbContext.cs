using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Microsoft.EntityFrameworkCore;
// PropertySaveBehavior — used to make IssuedCertificate.CertificateId immutable after insert (D-355).
using Microsoft.EntityFrameworkCore.Metadata;

namespace Kurx.Infrastructure.Persistence;

public class KurxDbContext : DbContext
{
    public KurxDbContext(DbContextOptions<KurxDbContext> options) : base(options) { }

    // ── Users & Auth ─────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UsernameHistory> UsernameHistory => Set<UsernameHistory>();
    public DbSet<UsernameChangeLog> UsernameChangeLogs => Set<UsernameChangeLog>();
    public DbSet<OtpRequest> OtpRequests => Set<OtpRequest>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Report> Reports => Set<Report>();

    // ── Trusted Device Authentication (AM0-AM10) — live on every auth request ──
    public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<AuthChallenge> AuthChallenges => Set<AuthChallenge>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();

    // ── Password + trusted browser (target architecture: factor 1 and factor 2) ──
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();
    public DbSet<TrustedBrowser> TrustedBrowsers => Set<TrustedBrowser>();

    // ── Platform roles (M2) ──────────────────────────────────────────────────
    public DbSet<PlatformRoleAssignment> PlatformRoles => Set<PlatformRoleAssignment>();

    // ── Person identity verification (M3) ────────────────────────────────────
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();

    // ── Trust & Verification substrate (M0) ──────────────────────────────────
    public DbSet<VerificationDocument> VerificationDocuments => Set<VerificationDocument>();
    public DbSet<VerificationReview> VerificationReviews => Set<VerificationReview>();

    // ── Fraud prevention (M13) ────────────────────────────────────────────────
    public DbSet<BlacklistEntry> BlacklistEntries => Set<BlacklistEntry>();
    public DbSet<FraudSignal> FraudSignals => Set<FraudSignal>();

    // ── Organizations ────────────────────────────────────────────────────────
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();                // V3 §4.1 structural tree (Phase 4)
    public DbSet<OrganizationAlias> OrganizationAliases => Set<OrganizationAlias>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<AudienceRule> AudienceRules => Set<AudienceRule>();  // V3 §4.4 audience rules (Phase 5)
    public DbSet<MembershipClaim> MembershipClaims => Set<MembershipClaim>();
    public DbSet<OrgInvitation> OrgInvitations => Set<OrgInvitation>();
    public DbSet<OrganizationWallet> OrganizationWallets => Set<OrganizationWallet>();
    public DbSet<OrganizationFollower> OrganizationFollowers => Set<OrganizationFollower>();
    public DbSet<AllyConnection> AllyConnections => Set<AllyConnection>();     // D-201
    public DbSet<OrgBankVerification> OrgBankVerifications => Set<OrgBankVerification>();
    public DbSet<RiskFlag> RiskFlags => Set<RiskFlag>();
    public DbSet<PayoutSchedule> PayoutSchedules => Set<PayoutSchedule>();

    // ── Events & Taxonomy ────────────────────────────────────────────────────
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();
    // D-188 (Platform Taxonomy Management) — reuses the Capability catalog below, keyed by EventCategory.Id.
    public DbSet<CategoryCapabilityDefault> CategoryCapabilityDefaults => Set<CategoryCapabilityDefault>();
    // V3 Kind registry (Event Architecture V3 §2, Phase 1) — the closed 20-Kind catalog + 145 aliases.
    public DbSet<EventKind> EventKinds => Set<EventKind>();
    public DbSet<KindAlias> KindAliases => Set<KindAlias>();
    // V3 Capability registry (Event Architecture V3 §11, Phase 2) — ~45 capabilities + Kind defaults + per-event set.
    public DbSet<Capability> Capabilities => Set<Capability>();
    public DbSet<KindCapabilityDefault> KindCapabilityDefaults => Set<KindCapabilityDefault>();
    public DbSet<EventCapability> EventCapabilities => Set<EventCapability>();
    public DbSet<Event> Events => Set<Event>();
    // V3 §14.3 Approval chains (Phase 14)
    public DbSet<ApprovalChain> ApprovalChains => Set<ApprovalChain>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalStepDecision> ApprovalStepDecisions => Set<ApprovalStepDecision>();
    public DbSet<EventSeries> EventSeries => Set<EventSeries>();                     // V3 §13.2 Series (Phase 12)
    public DbSet<EventSeriesFollower> EventSeriesFollowers => Set<EventSeriesFollower>();
    public DbSet<EventSearchDocument> EventSearchDocuments => Set<EventSearchDocument>();   // V3 §15 discovery index (Phase 16)
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<EventTag> EventTags => Set<EventTag>();
    public DbSet<SavedEvent> SavedEvents => Set<SavedEvent>();
    public DbSet<EventReview> EventReviews => Set<EventReview>();
    public DbSet<EventAssignment> EventAssignments => Set<EventAssignment>();
    public DbSet<ParticipantRole> ParticipantRoles => Set<ParticipantRole>();       // V3 §5.3 (Phase 6)
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();     // V3 §5.1 (Phase 6)

    // ── Event Content ────────────────────────────────────────────────────────
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueImage> VenueImages => Set<VenueImage>();
    public DbSet<EventTemplate> EventTemplates => Set<EventTemplate>();
    public DbSet<Speaker> Speakers => Set<Speaker>();
    public DbSet<EventSpeaker> EventSpeakers => Set<EventSpeaker>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<EventSponsor> EventSponsors => Set<EventSponsor>();
    public DbSet<EventSession> EventSessions => Set<EventSession>();
    public DbSet<EventSessionSpeaker> EventSessionSpeakers => Set<EventSessionSpeaker>();
    public DbSet<EventMedia> EventMedia => Set<EventMedia>();

    // ── Ticketing ────────────────────────────────────────────────────────────
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<EventArchetype> EventArchetypes => Set<EventArchetype>();   // D-266 M1
    public DbSet<ArchetypeCapabilityDefault> ArchetypeCapabilityDefaults => Set<ArchetypeCapabilityDefault>();
    public DbSet<EventAuthorization> EventAuthorizations => Set<EventAuthorization>();   // D-266 M5
    public DbSet<EventReviewChecklistItem> EventReviewChecklistItems => Set<EventReviewChecklistItem>();  // D-266 M7
    public DbSet<EventDraftSnapshot> EventDraftSnapshots => Set<EventDraftSnapshot>();                    // D-266 M8
    // D-265 — event creation
    public DbSet<RegistrationConsent> RegistrationConsents => Set<RegistrationConsent>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();
    public DbSet<FormField> FormFields => Set<FormField>();
    public DbSet<FieldPreset> FieldPresets => Set<FieldPreset>();
    public DbSet<SeatHold> SeatHolds => Set<SeatHold>();
    public DbSet<TicketTransfer> TicketTransfers => Set<TicketTransfer>();
    public DbSet<GateEntry> GateEntries => Set<GateEntry>();
    public DbSet<TicketWaitlist> TicketWaitlists => Set<TicketWaitlist>();
    public DbSet<InventoryPool> InventoryPools => Set<InventoryPool>();      // V3 §8.1 inventory (Phase 7)
    public DbSet<EventCheckinDevice> EventCheckinDevices => Set<EventCheckinDevice>();

    // ── Orders & Payments ────────────────────────────────────────────────────
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    // V3 §7 registration → admission → credential chain + policy (Phase 8, authoritative from Phase 9)
    public DbSet<RegistrationPolicy> RegistrationPolicies => Set<RegistrationPolicy>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<Admission> Admissions => Set<Admission>();
    public DbSet<Credential> Credentials => Set<Credential>();
    // V3 §7.5 Delegated registration (Phase 13)
    public DbSet<SeatBlock> SeatBlocks => Set<SeatBlock>();
    public DbSet<SeatBlockSeat> SeatBlockSeats => Set<SeatBlockSeat>();
    // V3 §9.2/§9.5 Pass + AdmissionRight + Value Allocation Record (Phase 9 authority cut-over)
    public DbSet<Pass> Passes => Set<Pass>();
    public DbSet<AdmissionRight> AdmissionRights => Set<AdmissionRight>();
    public DbSet<ValueAllocationRecord> ValueAllocationRecords => Set<ValueAllocationRecord>();
    // V3 §6 Team subsystem (Phase 10) — additive; the purchase Group stays as a legacy mirror
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<TeamInvite> TeamInvites => Set<TeamInvite>();
    public DbSet<TeamJoinRequest> TeamJoinRequests => Set<TeamJoinRequest>();
    public DbSet<TeamPolicy> TeamPolicies => Set<TeamPolicy>();
    // V3 §10 Competition engine (Phase 11) — Stage · Fixture · ScoringPolicy · Result
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<StageParticipant> StageParticipants => Set<StageParticipant>();
    public DbSet<Fixture> Fixtures => Set<Fixture>();
    public DbSet<FixtureParticipant> FixtureParticipants => Set<FixtureParticipant>();
    public DbSet<FixtureOfficial> FixtureOfficials => Set<FixtureOfficial>();
    public DbSet<ScoringPolicy> ScoringPolicies => Set<ScoringPolicy>();
    public DbSet<JudgeScore> JudgeScores => Set<JudgeScore>();
    public DbSet<PublicVote> PublicVotes => Set<PublicVote>();
    public DbSet<StageResult> StageResults => Set<StageResult>();
    public DbSet<ResultCorrection> ResultCorrections => Set<ResultCorrection>();

    // ── Groups & Tickets ─────────────────────────────────────────────────────
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Transfer> Transfers => Set<Transfer>();

    // ── Money ────────────────────────────────────────────────────────────────
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Withdrawal> Withdrawals => Set<Withdrawal>();

    // ── Certificates & Design ────────────────────────────────────────────────
    public DbSet<DesignTemplate> DesignTemplates => Set<DesignTemplate>();
    public DbSet<GeneratedCard> GeneratedCards => Set<GeneratedCard>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<IdCard> IdCards => Set<IdCard>();

    // ── The certificate module (D-355). New tables; the three dormant ones above are untouched. ──
    public DbSet<CertificateTemplate> CertificateTemplates => Set<CertificateTemplate>();
    public DbSet<CertificateTemplateField> CertificateTemplateFields => Set<CertificateTemplateField>();
    public DbSet<CertificateIdRule> CertificateIdRules => Set<CertificateIdRule>();
    public DbSet<CertificateBatch> CertificateBatches => Set<CertificateBatch>();
    public DbSet<CertificateRecipient> CertificateRecipients => Set<CertificateRecipient>();
    public DbSet<IssuedCertificate> IssuedCertificates => Set<IssuedCertificate>();
    public DbSet<CertificateRevocation> CertificateRevocations => Set<CertificateRevocation>();
    public DbSet<CertificateAccessLink> CertificateAccessLinks => Set<CertificateAccessLink>();
    public DbSet<CertificateDelivery> CertificateDeliveries => Set<CertificateDelivery>();
    public DbSet<CertificateEvent> CertificateEvents => Set<CertificateEvent>();
    public DbSet<CertificateSigningKey> CertificateSigningKeys => Set<CertificateSigningKey>();

    // ── Entitlements — food, meals, merch, access (D-334). Distinct from Coupons (D-265 discounts).
    public DbSet<EntitlementProduct> EntitlementProducts => Set<EntitlementProduct>();
    public DbSet<EntitlementGrant> EntitlementGrants => Set<EntitlementGrant>();
    public DbSet<EntitlementRedemption> EntitlementRedemptions => Set<EntitlementRedemption>();

    // ── Invitations, Announcements & Chat ────────────────────────────────────
    public DbSet<EventInvitation> EventInvitations => Set<EventInvitation>();
    public DbSet<EventInviteLink> EventInviteLinks => Set<EventInviteLink>();                       // D-266 M6
    public DbSet<EventInviteLinkRedemption> EventInviteLinkRedemptions => Set<EventInviteLinkRedemption>();
    public DbSet<EventAnnouncement> EventAnnouncements => Set<EventAnnouncement>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatMember> ChatMembers => Set<ChatMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatMessageHide> ChatMessageHides => Set<ChatMessageHide>();   // D-293 delete-for-me
    public DbSet<ChatMessageReaction> ChatMessageReactions => Set<ChatMessageReaction>();   // D-295
    public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();

    // ── Posts (D-262) ────────────────────────────────────────────────────────
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<PostPoll> PostPolls => Set<PostPoll>();
    public DbSet<PostPollOption> PostPollOptions => Set<PostPollOption>();
    public DbSet<PostPollBallot> PostPollBallots => Set<PostPollBallot>();
    public DbSet<PostPollVote> PostPollVotes => Set<PostPollVote>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<PostComment> PostComments => Set<PostComment>();
    public DbSet<PostCommentLike> PostCommentLikes => Set<PostCommentLike>();
    public DbSet<PostSave> PostSaves => Set<PostSave>();
    public DbSet<PostHashtag> PostHashtags => Set<PostHashtag>();
    public DbSet<PostMention> PostMentions => Set<PostMention>();

    // ── Account settings (D-263) ─────────────────────────────────────────────
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();

    // ── Messaging & Logs ─────────────────────────────────────────────────────
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();

    // ── Analytics & Gamification (V2) ────────────────────────────────────────
    public DbSet<EventView> EventViews => Set<EventView>();
    public DbSet<PointsLedger> PointsLedger => Set<PointsLedger>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<Leaderboard> Leaderboards => Set<Leaderboard>();
    public DbSet<ReferralReward> ReferralRewards => Set<ReferralReward>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Enums stored as text (enum member name) for readability in SQL.
        foreach (var entityType in b.Model.GetEntityTypes())
            foreach (var prop in entityType.GetProperties())
                if (prop.ClrType.IsEnum)
                    prop.SetProviderClrType(typeof(string));

        // V3 §9.1 (Phase 3): every money-bearing currency column is ISO-4217 (3 chars), default INR — so
        // the currency dimension exists on every money table even while only the degenerate INR case is
        // used. Applied uniformly so a new money entity gets it automatically (no per-table config).
        foreach (var entityType in b.Model.GetEntityTypes())
            foreach (var prop in entityType.GetProperties())
                if (prop.ClrType == typeof(string) && prop.Name is "Currency" or "SettlementCurrency")
                {
                    prop.SetDefaultValue(Kurx.Domain.Money.DefaultCurrency);
                    prop.SetMaxLength(3);
                }

        // Trigram fuzzy search for the organization registry (M4, D-043).
        b.HasPostgresExtension("pg_trgm");

        // ════════════════════════════════════════════════════════════════════
        // USERS & AUTH
        // ════════════════════════════════════════════════════════════════════

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(x => x.Phone).IsUnique();
            // Canonical phone identity (D-089). Filtered to non-null so the not-yet-backfilled rows
            // during the staged migration neither collide nor bloat the index.
            e.HasIndex(x => x.PhoneE164)
                .IsUnique()
                .HasDatabaseName("ix_users_phone_e164")
                .HasFilter("\"PhoneE164\" IS NOT NULL");
            e.HasIndex(x => x.Username).IsUnique();
            // Email uniqueness is case-insensitive (D-038): a functional unique index on
            // lower("Email") is created via raw SQL in the AddEmailUniqueIndex migration,
            // not here — EF Core's fluent HasIndex can't express an expression index, and
            // declaring a second plain HasIndex(x => x.Email) here would create a redundant,
            // case-sensitive index alongside it.
            // People search (D-209/D-211): same pg_trgm GIN approach as Organization.NormalizedName
            // above — a trigram index natively accelerates ILIKE '%term%', not just the `%` similarity
            // operator, so SearchUsersAsync's existing query needed no rewrite, just these two indexes.
            // The explicit-name overload (not the lambda-selector one) is required for Username: a
            // second `HasIndex(x => x.Username)` call would otherwise be matched to — and silently
            // rewrite — the existing unique btree index above rather than adding a distinct one.
            e.HasIndex(new[] { nameof(User.Name) }, "ix_users_name_trgm")
                .HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasIndex(new[] { nameof(User.Username) }, "ix_users_username_trgm")
                .HasMethod("gin").HasOperators("gin_trgm_ops");
            e.Property(x => x.EducationJson).HasColumnType("jsonb");
            e.Property(x => x.LinksJson).HasColumnType("jsonb");
            e.Property(x => x.SectionVisibilityJson).HasColumnType("jsonb");
            // `date`, not `timestamptz` — a birth date carries no time and no zone, and the conversion
            // would shift the calendar day for every user outside UTC.
            e.Property(x => x.DateOfBirth).HasColumnType("date");
        });

        b.Entity<SigningKey>(e =>
        {
            e.ToTable("signing_keys");
            e.HasIndex(x => x.KeyId).IsUnique();
            // Exactly one Active key may exist at a time — enforced by the database, not just by
            // service logic, because two active signers would publish tokens the other instances'
            // JWKS cache might not yet know about (D-099).
            e.HasIndex(x => x.State)
                .HasDatabaseName("ix_signing_keys_single_active")
                .IsUnique()
                .HasFilter("\"State\" = 'Active'");
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId)
                .HasDatabaseName("ix_refresh_tokens_user_active")
                .HasFilter("\"RevokedAt\" IS NULL");
            // AM0 (additive): link to a device-bound session (AM5). Nullable → SetNull, filtered index.
            e.HasIndex(x => x.SessionId)
                .HasDatabaseName("ix_refresh_tokens_session")
                .HasFilter("\"SessionId\" IS NOT NULL");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AuthSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<UsernameHistory>(e =>
        {
            e.ToTable("username_history");
            e.HasIndex(x => x.Username);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UsernameChangeLog>(e =>
        {
            e.ToTable("username_change_log");
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.ChangedAt });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OtpRequest>(e =>
        {
            e.ToTable("otp_requests");
            e.HasIndex(x => new { x.Phone, x.CreatedAt });
            e.HasIndex(x => new { x.Phone, x.ExpiresAt })
                .HasDatabaseName("ix_otp_requests_phone_unexpired")
                .HasFilter("\"Consumed\" = false");
        });

        b.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.HasIndex(x => x.FcmToken).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => x.UserId)
                .HasDatabaseName("ix_notifications_user_unread")
                .HasFilter("\"ReadAt\" IS NULL");
            // DB-3: the retention sweep filters on CreatedAt alone, with no user predicate. The composite
            // above is led by UserId and cannot serve that, so the daily cleanup sequentially scanned the
            // platform's highest-volume user-facing table. This is the index that makes it a range seek.
            e.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("ix_notifications_created_at");
            // DB-3: idempotency for the two kinds that must not repeat. PARTIAL — a null DedupKey is
            // unconstrained, which is what keeps every legitimately-repeatable kind (material change,
            // re-invitation, re-requested ally, re-reviewed authorization) working untouched. The database
            // holds this rather than the application, so two replicas racing the same announcement fan-out
            // converge on one row instead of relying on both having read the same pre-check.
            // Column order is DedupKey FIRST, deliberately. Uniqueness is identical either way —
            // UNIQUE(a,b) and UNIQUE(b,a) constrain the same thing — but the read shapes are not. The
            // announcement fan-out asks "who already received THIS announcement?", i.e. `WHERE DedupKey = x`
            // with no user predicate. Led by UserId that cannot seek and degrades to scanning the whole
            // index (measured: 1,153 buffers on 500k rows); led by DedupKey it is a prefix seek, and the
            // reminder job's `DedupKey = x AND UserId = ANY(...)` still seeks on both columns (3 buffers).
            e.HasIndex(x => new { x.DedupKey, x.UserId })
                .IsUnique()
                .HasDatabaseName("ix_notifications_dedup")
                .HasFilter("\"DedupKey\" IS NOT NULL");
            e.Property(x => x.DedupKey).HasMaxLength(128);
            e.Property(x => x.DataJson).HasColumnType("jsonb");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_log");
            e.HasIndex(x => new { x.Entity, x.EntityId });
            e.HasIndex(x => x.ActorId);
            e.HasIndex(x => x.CreatedAt);
            e.Property(x => x.DetailsJson).HasColumnType("jsonb");
        });

        b.Entity<Report>(e =>
        {
            e.ToTable("reports");
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            // Partial index: only open reports matter for moderation queue
            e.HasIndex(x => x.Status)
                .HasDatabaseName("ix_reports_open")
                .HasFilter("\"Status\" = 'open'");
            e.HasIndex(x => x.ReporterId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterId);
        });

        // ════════════════════════════════════════════════════════════════════
        // TRUSTED DEVICE AUTHENTICATION (AM0-AM10) — live; always on (D-115).
        // Design: docs/auth/AUTHENTICATION_DATABASE.md §2. Enums persist as text.
        // ════════════════════════════════════════════════════════════════════

        b.Entity<TrustedDevice>(e =>
        {
            e.ToTable("trusted_devices");
            e.HasIndex(x => new { x.UserId, x.LifecycleState });
            e.Property(x => x.AttestationJson).HasColumnType("jsonb");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DeviceCredential>(e =>
        {
            e.ToTable("device_credentials");
            e.HasIndex(x => x.TrustedDeviceId);
            // A WebAuthn credential id is globally unique; device-key rows leave it null.
            e.HasIndex(x => x.WebAuthnCredentialId)
                .IsUnique()
                .HasDatabaseName("ix_device_credentials_webauthn")
                .HasFilter("\"WebAuthnCredentialId\" IS NOT NULL");
            e.HasOne<TrustedDevice>().WithMany().HasForeignKey(x => x.TrustedDeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuthChallenge>(e =>
        {
            e.ToTable("auth_challenges");
            e.HasIndex(x => x.Nonce).IsUnique();
            e.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAt });
            e.Property(x => x.ContextJson).HasColumnType("jsonb");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TrustedDevice>().WithMany().HasForeignKey(x => x.ApprovedByDeviceId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<AuthSession>(e =>
        {
            e.ToTable("auth_sessions");
            e.HasIndex(x => new { x.UserId, x.FamilyId });
            e.HasIndex(x => x.TrustedDeviceId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TrustedDevice>().WithMany().HasForeignKey(x => x.TrustedDeviceId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<SecurityEvent>(e =>
        {
            e.ToTable("security_events");
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => new { x.Type, x.CreatedAt });
            e.Property(x => x.ContextJson).HasColumnType("jsonb");
            // UserId nullable (pre-auth events) → SetNull, not Cascade.
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<RecoveryCode>(e =>
        {
            e.ToTable("recovery_codes");
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserCredential>(e =>
        {
            e.ToTable("user_credentials");
            // One password per user. Unique (not just indexed) so a concurrent "set password" race
            // cannot leave an account with two credential rows and an ambiguous verifier.
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PasswordHistory>(e =>
        {
            e.ToTable("password_history");
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TrustedBrowser>(e =>
        {
            e.ToTable("trusted_browsers");
            // Lookup is always "find the live browser for this cookie", so the hash is the access path.
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UserId, x.RevokedAt });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OtpCode>(e =>
        {
            e.ToTable("otp_codes");
            // Latest pending code for a destination+purpose (mirrors the legacy otp_requests partial index).
            e.HasIndex(x => new { x.Destination, x.Purpose, x.ExpiresAt })
                .HasDatabaseName("ix_otp_codes_dest_unexpired")
                .HasFilter("\"Consumed\" = false");
            // No FK: first-registration OTPs precede the user row (same as legacy otp_requests).
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            // Dispatcher scans only undispatched rows.
            e.HasIndex(x => new { x.Status, x.CreatedAt })
                .HasDatabaseName("ix_outbox_pending")
                .HasFilter("\"Status\" = 'Pending'");
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });

        // ════════════════════════════════════════════════════════════════════
        // PLATFORM ROLES (M2)
        // ════════════════════════════════════════════════════════════════════

        b.Entity<PlatformRoleAssignment>(e =>
        {
            e.ToTable("platform_roles");
            e.HasIndex(x => new { x.UserId, x.Role }).IsUnique();  // one row per (user, role)
            e.HasIndex(x => x.UserId);                             // live per-request role lookup
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.GrantedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // ════════════════════════════════════════════════════════════════════
        // PERSON IDENTITY VERIFICATION (M3)
        // ════════════════════════════════════════════════════════════════════

        b.Entity<UserIdentity>(e =>
        {
            e.ToTable("user_identity_verifications");
            e.HasIndex(x => x.UserId).IsUnique();               // one identity record per user
            e.Property(x => x.ProviderRefsJson).HasColumnType("jsonb");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // ════════════════════════════════════════════════════════════════════
        // TRUST & VERIFICATION SUBSTRATE (M0)
        // ════════════════════════════════════════════════════════════════════

        b.Entity<VerificationDocument>(e =>
        {
            e.ToTable("verification_documents");
            // Primary access path: "all documents for this subject".
            e.HasIndex(x => new { x.SubjectType, x.SubjectId });
            // Forgery / duplicate detection lookups by content hash (M13).
            e.HasIndex(x => x.Sha256);
            e.HasIndex(x => x.UploadedBy);
            e.Property(x => x.ExtractedJson).HasColumnType("jsonb");
            // SubjectId is polymorphic (users/organizations/memberships/events) — no FK.
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<VerificationReview>(e =>
        {
            e.ToTable("verification_reviews");
            // Admin console + audit trail: subject history newest-first.
            e.HasIndex(x => new { x.SubjectType, x.SubjectId, x.CreatedAt });
            e.HasIndex(x => x.ReviewerId);
            // SubjectId polymorphic — no FK. ReviewerId nullable (system decisions).
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        });

        // ════════════════════════════════════════════════════════════════════
        // FRAUD PREVENTION (M13)
        // ════════════════════════════════════════════════════════════════════

        b.Entity<BlacklistEntry>(e =>
        {
            e.ToTable("blacklist_entries");
            e.HasIndex(x => new { x.Kind, x.Value }).IsUnique();   // exact hard-block lookups
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<FraudSignal>(e =>
        {
            e.ToTable("fraud_signals");
            e.HasIndex(x => new { x.SubjectType, x.SubjectId });   // risk-score aggregation per subject
            e.Property(x => x.DetailsJson).HasColumnType("jsonb");
            // SubjectId is polymorphic (same subjects as the verification substrate) — no FK.
        });

        // ════════════════════════════════════════════════════════════════════
        // ORGANIZATIONS
        // ════════════════════════════════════════════════════════════════════

        b.Entity<Organization>(e =>
        {
            e.ToTable("organizations");
            e.Property(x => x.LinksJson).HasColumnType("jsonb");
            e.HasIndex(x => x.Slug)
                .IsUnique()
                .HasDatabaseName("ix_organizations_slug_active")
                .HasFilter("\"DeletedAt\" IS NULL");
            // Registry (M4): fuzzy dedup search + globally-unique domain + canonical resolution.
            e.HasIndex(x => x.NormalizedName).HasMethod("gin").HasOperators("gin_trgm_ops");
            // D-325: the admin event search matches the DISPLAY name, not the normalized one, so the
            // registry's index above cannot serve it. Second branch of the same split predicate.
            e.HasIndex(new[] { nameof(Organization.Name) }, "ix_organizations_name_trgm")
                .HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasIndex(x => x.PrimaryDomain)
                .IsUnique()
                .HasDatabaseName("ix_organizations_domain_active")
                .HasFilter("\"PrimaryDomain\" IS NOT NULL AND \"DeletedAt\" IS NULL");
            e.HasIndex(x => x.CanonicalOrgId);
        });

        b.Entity<OrgUnit>(e =>
        {
            e.ToTable("org_units");
            e.Property(x => x.Kind).HasMaxLength(40);
            e.HasIndex(x => x.OrgId);
            // "Every org has exactly one root unit" (V3 §4.1) is a DB guarantee, not just service logic:
            // a partial unique index rejects a second parentless unit for the same org.
            e.HasIndex(x => x.OrgId)
                .IsUnique()
                .HasDatabaseName("ix_org_units_one_root_per_org")
                .HasFilter("\"ParentId\" IS NULL");
            // No index on Path yet: nothing queries it this phase (ancestor lookup parses the path in-app),
            // and a plain btree can't serve a left-anchored LIKE prefix scan under a UTF-8 collation anyway.
            // Phase 5 adds the subtree query and the matching `text_pattern_ops` index together.
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.ParentId);
        });

        b.Entity<OrganizationAlias>(e =>
        {
            e.ToTable("organization_aliases");
            e.HasIndex(x => x.OrgId);
            e.HasIndex(x => new { x.OrgId, x.NormalizedAlias }).IsUnique();
            e.HasIndex(x => x.NormalizedAlias).HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Membership>(e =>
        {
            e.ToTable("memberships");
            e.HasIndex(x => new { x.UserId, x.OrgId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.OrgId);                   // needed for "all members of this org"
            e.Property(x => x.AttributesJson).HasColumnType("jsonb");   // V3 §4.3 (Phase 5)
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<AudienceRule>(e =>                     // V3 §4.4 (Phase 5)
        {
            e.ToTable("audience_rules");
            e.HasIndex(x => x.EventId).IsUnique();      // exactly one rule per event
            e.Property(x => x.UnitSubtreeInJson).HasColumnType("jsonb");
            e.Property(x => x.RoleInJson).HasColumnType("jsonb");
            e.Property(x => x.CohortYearInJson).HasColumnType("jsonb");
            e.Property(x => x.AttributeMatchesJson).HasColumnType("jsonb");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MembershipClaim>(e =>
        {
            e.ToTable("membership_claims");
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.OrgId, x.Status });
            e.HasIndex(x => x.Status);                  // reviewer pending queue
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrgInvitation>(e =>
        {
            e.ToTable("org_invitations");
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => new { x.OrgId, x.Status });
            e.HasIndex(x => new { x.InvitedUserId, x.Status });
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.InvitedBy);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.InvitedUserId);
        });

        b.Entity<OrganizationWallet>(e =>
        {
            e.ToTable("organization_wallet", t =>
            {
                t.HasCheckConstraint("ck_org_wallet_collected", "\"CollectedPaise\" >= 0");
                t.HasCheckConstraint("ck_org_wallet_available", "\"AvailablePaise\" >= 0");
                t.HasCheckConstraint("ck_org_wallet_advanced", "\"AdvancedPaise\" >= 0");
                t.HasCheckConstraint("ck_org_wallet_reserved", "\"ReservedPaise\" >= 0");
                t.HasCheckConstraint("ck_org_wallet_settled", "\"SettledPaise\" >= 0");
                t.HasCheckConstraint("ck_org_wallet_lifetime_earned", "\"LifetimeEarnedPaise\" >= 0");
                t.HasCheckConstraint("ck_org_wallet_lifetime_withdrawn", "\"LifetimeWithdrawnPaise\" >= 0");
            });
            e.HasIndex(x => x.OrgId).IsUnique();
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<LedgerEntry>().WithMany().HasForeignKey(x => x.LastLedgerEntryId);
        });

        b.Entity<OrganizationFollower>(e =>
        {
            e.ToTable("organization_followers");
            e.HasKey(x => new { x.UserId, x.OrgId });
            e.HasIndex(x => x.OrgId);                   // "how many followers does this org have?"
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AllyConnection>(e =>                   // D-201 — mutual professional connections
        {
            e.ToTable("ally_connections", t => t.HasCheckConstraint(
                "ck_ally_connections_pair_order", "\"UserLowId\" < \"UserHighId\""));
            // One row per unordered pair, ever — re-requesting after Decline/Revoke reactivates it.
            e.HasIndex(x => new { x.UserLowId, x.UserHighId }).IsUnique().HasDatabaseName("ix_ally_connections_pair");
            e.HasIndex(x => x.RequesterId);              // outgoing-requests inbox
            e.HasIndex(x => x.AddresseeId);               // incoming-requests inbox
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserLowId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserHighId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.FirstSharedEventId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<OrgBankVerification>(e =>
        {
            e.ToTable("org_bank_verifications");     // renamed from kyc_records (M9, D-048)
            e.HasIndex(x => x.OrgId);
            e.HasIndex(x => new { x.OrgId, x.Kind, x.Status });
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RiskFlag>(e =>
        {
            e.ToTable("risk_flags");
            e.HasIndex(x => new { x.OrgId, x.Status });
            e.Property(x => x.DetailsJson).HasColumnType("jsonb");
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<PayoutSchedule>(e =>
        {
            e.ToTable("payout_schedules");
            e.HasIndex(x => x.OrgId).IsUnique();
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
        });

        // ════════════════════════════════════════════════════════════════════
        // EVENTS & TAXONOMY
        // ════════════════════════════════════════════════════════════════════

        b.Entity<EventCategory>(e =>
        {
            e.ToTable("event_categories");
            e.HasIndex(x => x.Slug).IsUnique();
            // The global enum→text loop above tests prop.ClrType.IsEnum, which is false for a nullable
            // enum, so this one is configured by hand. Without it the column would land as integer while
            // events.Product and event_archetypes.Product are text — the same enum stored two ways.
            // The two pre-existing nullable enums (ApproverRole, DmRequestState) stay integer; converting
            // them is a data migration on unrelated tables and no part of D-266.
            e.Property(x => x.ProductClass).HasConversion<string>();
            e.Property(x => x.AllowedRegistrationPoliciesJson).HasColumnType("jsonb");
        });

        // D-266 M7 — same nullable-enum trap as ProductClass above: the global enum→text loop tests
        // prop.ClrType.IsEnum, which is false for a nullable enum, so without this the column lands as
        // integer while every other status on the platform is text.
        b.Entity<Event>(e =>
        {
            e.Property(x => x.FinancialReviewStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FinancialReviewNotes).HasMaxLength(2000);

            // D-266 M4 — the review queue's "who holds this". Indexed because the reviewer console's own
            // queue ("items I am holding") filters on it. No FK to users: the hold is transient workflow
            // state, and a deleted account must not cascade into an event row.
            e.HasIndex(x => x.ReviewClaimedBy);
        });

        // D-188 — reuses the existing Capability catalog (see "V3 §11" block below); not DB-constrained to
        // any one CategoryLevel so a future capability-inheritance pass needs no migration (see the type
        // doc comment on CategoryCapabilityDefault).
        b.Entity<CategoryCapabilityDefault>(e =>
        {
            e.ToTable("category_capability_defaults");
            e.HasIndex(x => new { x.CategoryNodeId, x.CapabilitySlug }).IsUnique();
            e.HasOne<EventCategory>().WithMany().HasForeignKey(x => x.CategoryNodeId).OnDelete(DeleteBehavior.Cascade);
        });

        // V3 Kind registry (Event Architecture V3 §2, Phase 1). Reference-by-slug, no FK from Event —
        // the design references Kinds by slug and Phase 1 stays additive (Event.KindSlug is nullable).
        b.Entity<EventKind>(e =>
        {
            e.ToTable("event_kinds");
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.GroupSlug);
        });

        b.Entity<KindAlias>(e =>
        {
            e.ToTable("kind_aliases");
            // One alias per legacy Type slug; the slug is the resolution key for backfill/derivation.
            e.HasIndex(x => x.NormalizedAlias).IsUnique();
            e.HasIndex(x => x.KindSlug);
        });

        // V3 Capability registry (Event Architecture V3 §11, Phase 2). Slug-referenced like the Kind
        // registry (no FK from the reference tables); EventCapability cascades from its event.
        b.Entity<Capability>(e =>
        {
            e.ToTable("capabilities");
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.GroupSlug);
            e.Property(x => x.DependsOnJson).HasColumnType("jsonb");
            e.Property(x => x.AvailableModesJson).HasColumnType("jsonb");
        });

        b.Entity<KindCapabilityDefault>(e =>
        {
            e.ToTable("kind_capability_defaults");
            e.HasKey(x => new { x.KindSlug, x.CapabilitySlug });
            e.HasIndex(x => x.KindSlug);
        });

        b.Entity<EventCapability>(e =>
        {
            e.ToTable("event_capabilities");
            e.HasKey(x => new { x.EventId, x.CapabilitySlug });
            e.Property(x => x.ConfigJson).HasColumnType("jsonb");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── V3 §14.3 Approval chains (Phase 14) ──────────────────────────────────
        b.Entity<ApprovalChain>(e =>
        {
            e.ToTable("approval_chains");
            e.HasIndex(x => x.OrgUnitId);
            e.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ApprovalStep>(e =>
        {
            e.ToTable("approval_steps");
            e.HasIndex(x => new { x.ChainId, x.Sort });
            e.HasOne<ApprovalChain>().WithMany().HasForeignKey(x => x.ChainId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ApprovalRequest>(e =>
        {
            e.ToTable("approval_requests");
            e.HasIndex(x => new { x.EventId, x.ChainId }).IsUnique();   // one live request per (event, chain)
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApprovalChain>().WithMany().HasForeignKey(x => x.ChainId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ApprovalStepDecision>(e =>
        {
            e.ToTable("approval_step_decisions");
            e.HasIndex(x => new { x.RequestId, x.StepId }).IsUnique();   // one decision per step per request
            e.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApprovalStep>().WithMany().HasForeignKey(x => x.StepId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── V3 §13.2 EventSeries (Phase 12) ──────────────────────────────────────
        b.Entity<EventSeries>(e =>
        {
            e.ToTable("event_series");
            e.HasIndex(x => new { x.OrgId, x.Slug }).IsUnique();   // canonical series URL, unique per org
            e.Property(x => x.BrandAssetsJson).HasColumnType("jsonb");
            e.Property(x => x.ExceptionDatesJson).HasColumnType("jsonb");
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventSeriesFollower>(e =>
        {
            e.ToTable("event_series_followers");
            e.HasIndex(x => new { x.SeriesId, x.UserId }).IsUnique();   // one follow per user per series
            e.HasOne<EventSeries>().WithMany().HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── V3 §15 Discovery & Search index (Phase 16) ───────────────────────────
        // The FuzzyText jsonb→trgm GIN index and the generated `SearchVector` tsvector column + its GIN index are added
        // in the migration via raw SQL (they need Postgres-specific DDL EF can't model without a provider CLR type).
        b.Entity<EventSearchDocument>(e =>
        {
            e.ToTable("event_search_documents");
            e.HasKey(x => x.EventId);
            e.HasIndex(x => x.StartsAt);
            e.HasIndex(x => x.KindSlug);
            e.HasIndex(x => x.City);
            e.HasIndex(x => x.SeriesId);
            e.HasIndex(x => x.ParentEventId);
            e.HasIndex(x => x.IsSeriesPrimary);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Event>(e =>
        {
            e.ToTable("events", t => t.HasCheckConstraint("ck_events_status", StateVocabulary<EventStatus>("Status")));
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.ShortCode).IsUnique();
            // D-325: admin search is `Title ILIKE '%term%'`, which no btree can serve. Measured on 200k
            // events: sequential scan 138ms → bitmap index scan 1.8ms. The index is only reachable because
            // the predicate was split into independently-filterable branches in ListForAdminAsync — a
            // trigram index cannot be used while the OR spans two tables, which is why adding it alone was
            // refused first. Named explicitly so it cannot be matched to another HasIndex on Title.
            e.HasIndex(new[] { nameof(Event.Title) }, "ix_events_title_trgm")
                .HasMethod("gin").HasOperators("gin_trgm_ops");
            // D-273a: the property is RepresentingOrgId; the column, indexes and FK keep their original
            // physical names so the rename is code-only. Pinning them is what makes the change
            // schema-neutral — without it EF emits a RenameColumn/RenameIndex migration, and the app could
            // then no longer be rolled back independently of the database.
            e.Property(x => x.RepresentingOrgId).HasColumnName("OrgId");
            e.HasIndex(x => x.RepresentingOrgId).HasDatabaseName("IX_events_OrgId");
            e.HasIndex(x => x.ParentEventId);
            e.HasIndex(x => x.Status)
                .HasDatabaseName("ix_events_status_active")
                .HasFilter("\"DeletedAt\" IS NULL");
            e.HasIndex(x => x.Visibility)
                .HasDatabaseName("ix_events_visibility_active")
                .HasFilter("\"DeletedAt\" IS NULL");
            e.HasIndex(x => x.StartsAt);
            e.HasIndex(x => x.City);
            e.HasIndex(x => new { x.RepresentingOrgId, x.Status, x.EndsAt })
                .HasDatabaseName("IX_events_OrgId_Status_EndsAt");
            e.HasIndex(x => new { x.CreatedBy, x.Status });
            // Location index for state/country-level event discovery
            e.HasIndex(x => new { x.Country, x.State, x.City });
            // Partial: only live events need fast composite access
            e.HasIndex(x => new { x.Status, x.StartsAt })
                .HasDatabaseName("ix_events_status_starts_active")
                .HasFilter("\"DeletedAt\" IS NULL");
            e.Property(x => x.SocialLinksJson).HasColumnType("jsonb");
            e.HasIndex(x => x.OrgUnitId);               // V3 §4.1 (Phase 4): owning unit
            // D-265 — event creation fields.
            e.Property(x => x.FaqJson).HasColumnType("jsonb");
            // D-266 M1 — the behaviour axis. Indexed because capability resolution and discovery
            // both filter on it on every read.
            e.HasIndex(x => x.ArchetypeSlug);
            e.HasIndex(x => x.Product);
            // Every pre-D-266 event is a Public product — Private is a deliberate choice at creation,
            // never a backfill outcome. Also stops the migration emitting an empty-string default, which
            // is not a valid EventProduct and would throw on the first read of an existing row.
            e.Property(x => x.Product).HasDefaultValue(Kurx.Domain.Enums.EventProduct.Public);
            // Same reason as Product above: an empty-string default is not a valid enum member and would
            // throw on the first read of any pre-existing event. Open is the pre-D-266 behaviour.
            e.Property(x => x.RegistrationPolicy).HasDefaultValue(Kurx.Domain.Enums.EventRegistrationPolicy.Open);
            e.Property(x => x.PrizePoolJson).HasColumnType("jsonb");
            e.Property(x => x.Tagline).HasMaxLength(160);
            e.Property(x => x.ShortDescription).HasMaxLength(300);
            e.Property(x => x.PlatformFeePercent).HasPrecision(5, 2);
            e.Property(x => x.TaxPercent).HasPrecision(5, 2);
            // Registration-window scans on the discovery path; partial because a closed or deleted
            // event is never the answer.
            e.HasIndex(x => x.RegistrationClosesAt)
                .HasDatabaseName("ix_events_registration_closes_active")
                .HasFilter("\"DeletedAt\" IS NULL");
            // Foreign keys
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.RepresentingOrgId)
                .HasConstraintName("FK_events_organizations_OrgId");
            e.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.ParentEventId);
            e.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId);
            e.HasOne<EventTemplate>().WithMany().HasForeignKey(x => x.TemplateId);
            e.HasOne<EventCategory>().WithMany().HasForeignKey(x => x.CategoryId);
            e.HasOne<EventCategory>().WithMany().HasForeignKey(x => x.AudienceLevelId);
            e.HasOne<EventCategory>().WithMany().HasForeignKey(x => x.TypeId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy);
            e.HasOne<DesignTemplate>().WithMany().HasForeignKey(x => x.CertificateTemplateId);
            e.HasOne<DesignTemplate>().WithMany().HasForeignKey(x => x.InviteTemplateId);
            // V3 §13.2 (Phase 12): LINEAGE link + structural discovery.
            e.HasIndex(x => x.SeriesId);
            e.HasOne<EventSeries>().WithMany().HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── D-265: event creation ────────────────────────────────────────────
        b.Entity<ArchetypeCapabilityDefault>(e =>
        {
            e.ToTable("archetype_capability_defaults");
            e.HasKey(x => new { x.ArchetypeSlug, x.CapabilitySlug });
            e.Property(x => x.ArchetypeSlug).HasMaxLength(40);
            e.Property(x => x.CapabilitySlug).HasMaxLength(40);
        });

        b.Entity<EventArchetype>(e =>
        {
            e.ToTable("event_archetypes");
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(40).IsRequired();
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
        });

        // D-266 M5 — institutional authorization for ONE event. Unique on EventId: two rows would make
        // "is this event authorised?" a query with more than one answer.
        b.Entity<EventAuthorization>(e =>
        {
            e.ToTable("event_authorizations");
            e.HasIndex(x => x.EventId).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.HeadName).HasMaxLength(160).IsRequired();
            e.Property(x => x.HeadDesignation).HasMaxLength(160).IsRequired();
            e.Property(x => x.OfficialEmail).HasMaxLength(255).IsRequired();
            // D-266 M5 — required: a signatory who cannot be reached is not a verifiable one.
            e.Property(x => x.OfficialPhone).HasMaxLength(20).IsRequired();
            e.Property(x => x.RepresentativeRole).HasMaxLength(60).IsRequired();
            e.Property(x => x.RepresentativeRoleOther).HasMaxLength(80);
            // A LINK, not a grant — see the entity remarks. No cascade: deleting the account must not
            // delete the evidence that a letter was filed in that person's name.
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RepresentativeUserId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.LetterheadDocumentKey).HasMaxLength(400);
            e.Property(x => x.SignatureDocumentKey).HasMaxLength(400);
            e.Property(x => x.SupportingDocumentsJson).HasColumnType("jsonb");
            // Status is a non-nullable enum, so the global enum→text loop at the top of OnModelCreating
            // already stores it by member name; only the width is set here.
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.ReasonCode).HasMaxLength(40);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.SubmittedBy);
        });

        // D-266 M8 — wizard autosave. Opaque client state, never read by the publish path.
        b.Entity<EventDraftSnapshot>(e =>
        {
            e.ToTable("event_draft_snapshots");
            // Per (event, user): two managers mid-edit each keep their own in-progress form. Merging them
            // would produce something neither typed; last-write-wins would discard someone's work.
            e.HasIndex(x => new { x.EventId, x.UserId }).IsUnique();
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.Property(x => x.StepKey).HasMaxLength(40);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        // D-266 M7 — a reviewer's ticks. The ITEMS are a projection of PolicyResolver.ReviewerChecklist and
        // are never stored; only which of them this reviewer has ticked.
        b.Entity<EventReviewChecklistItem>(e =>
        {
            e.ToTable("event_review_checklist_items");
            // One tick per (event, reviewer, item). Two rows would make "is the checklist complete?"
            // ambiguous, which is the only question the table exists to answer.
            e.HasIndex(x => new { x.EventId, x.ReviewerId, x.ItemKey }).IsUnique();
            e.Property(x => x.ItemKey).HasMaxLength(60).IsRequired();
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerId);
        });

        b.Entity<RegistrationConsent>(e =>
        {
            e.ToTable("registration_consents");
            // One consent per registration — a second row would make "did they accept?" ambiguous.
            e.HasIndex(x => x.RegistrationId).IsUnique();
            e.HasIndex(x => x.EventId);
            e.Property(x => x.ConsentTextHash).HasMaxLength(64).IsRequired();
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
        });

        b.Entity<Coupon>(e =>
        {
            e.ToTable("coupons");
            // Codes are unique per EVENT, not globally: two organisers both wanting WELCOME10 is
            // normal, and a global unique index would stop the second one existing.
            e.HasIndex(x => new { x.EventId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.Percent).HasPrecision(5, 2);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
        });

        b.Entity<CouponRedemption>(e =>
        {
            e.ToTable("coupon_redemptions");
            // A retried checkout must not double-count against MaxRedemptions.
            e.HasIndex(x => new { x.CouponId, x.OrderId }).IsUnique();
            e.HasIndex(x => new { x.CouponId, x.UserId });   // enforces MaxPerUser
            e.HasOne<Coupon>().WithMany().HasForeignKey(x => x.CouponId);
        });

        b.Entity<Tag>(e =>
        {
            e.ToTable("tags");
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<EventTag>(e =>
        {
            e.ToTable("event_tags");
            e.HasKey(x => new { x.EventId, x.TagId });
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tag>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SavedEvent>(e =>
        {
            e.ToTable("saved_events");
            e.HasKey(x => new { x.UserId, x.EventId });
            e.HasIndex(x => x.EventId);                 // "how many saves for this event?"
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventReview>(e =>
        {
            e.ToTable("event_reviews", t => t.HasCheckConstraint("ck_event_reviews_rating", "\"Rating\" >= 1 AND \"Rating\" <= 5"));
            e.HasIndex(x => new { x.EventId, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.EventId, x.Rating })
                .HasDatabaseName("ix_event_reviews_event_rating_active")
                .HasFilter("\"DeletedAt\" IS NULL");
            e.HasIndex(x => x.UserId)
                .HasDatabaseName("ix_event_reviews_user_active")
                .HasFilter("\"DeletedAt\" IS NULL");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<EventAssignment>(e =>
        {
            e.ToTable("event_assignments");
            // A user can hold multiple roles at one event (e.g. Judge + Photographer)
            e.HasIndex(x => new { x.EventId, x.UserId, x.Role }).IsUnique();
            e.HasIndex(x => new { x.EventId, x.Status });
            // Partial index for profile page: only accepted/completed shown on profile
            e.HasIndex(x => x.UserId)
                .HasDatabaseName("ix_event_assignments_user_profile")
                .HasFilter("\"ShowOnProfile\" = true");
            e.HasIndex(x => x.OrgId);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.InvitedBy);
        });

        b.Entity<ParticipantRole>(e =>                  // V3 §5.3 (Phase 6)
        {
            e.ToTable("participant_roles");
            e.Property(x => x.Slug).HasMaxLength(60);
            e.Property(x => x.InventorySegment).HasMaxLength(40);
            e.Property(x => x.DefaultPermissionsJson).HasColumnType("jsonb");
            e.Property(x => x.DefaultAccessZonesJson).HasColumnType("jsonb");
            // Platform slugs are unique; org-extended slugs (OrgId set) are a later write path.
            e.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ix_participant_roles_platform_slug").HasFilter("\"OrgId\" IS NULL");
            e.HasIndex(x => x.OrgId);
        });

        b.Entity<EventParticipant>(e =>                 // V3 §5.1 (Phase 6)
        {
            e.ToTable("event_participants");
            e.Property(x => x.RoleSlug).HasMaxLength(60);
            e.Property(x => x.ScopeJson).HasColumnType("jsonb");
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => new { x.EventId, x.SubjectType, x.SubjectId });
            // One live row per (event, subject, role): idempotent assign, mirrors event_assignments.
            e.HasIndex(x => new { x.EventId, x.SubjectType, x.SubjectId, x.RoleSlug }).IsUnique()
                .HasDatabaseName("ix_event_participants_unique");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        // ════════════════════════════════════════════════════════════════════
        // EVENT CONTENT
        // ════════════════════════════════════════════════════════════════════

        b.Entity<Venue>(e =>
        {
            e.ToTable("venues");
            e.HasIndex(x => x.OrgId)
                .HasDatabaseName("ix_venues_org_active")
                .HasFilter("\"DeletedAt\" IS NULL");
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId);
        });

        b.Entity<VenueImage>(e =>
        {
            e.ToTable("venue_images");
            e.HasIndex(x => x.VenueId);
            e.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventTemplate>(e =>
        {
            e.ToTable("event_templates");
            // Versioned (Phase 15): one row per (family, version). Slug is stable across a family's versions, so it is
            // no longer unique per row (the service keeps it unique per scope on create).
            e.HasIndex(x => new { x.RootTemplateId, x.Version }).IsUnique();
            e.HasIndex(x => x.Slug);
            e.HasIndex(x => new { x.Scope, x.OrgId, x.OrgUnitId, x.OwnerUserId });   // scope resolution
            e.Property(x => x.DefaultSectionsJson).HasColumnType("jsonb");
            e.Property(x => x.ConfigJson).HasColumnType("jsonb");
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId);
        });

        b.Entity<Speaker>(e =>
        {
            e.ToTable("speakers");
            e.HasIndex(x => x.OrgId)
                .HasDatabaseName("ix_speakers_org_active")
                .HasFilter("\"DeletedAt\" IS NULL");
            e.Property(x => x.SocialLinksJson).HasColumnType("jsonb");
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId);
        });

        b.Entity<EventSpeaker>(e =>
        {
            e.ToTable("event_speakers");
            e.HasIndex(x => new { x.EventId, x.SpeakerId }).IsUnique();
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Speaker>().WithMany().HasForeignKey(x => x.SpeakerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Sponsor>(e =>
        {
            e.ToTable("sponsors");
            e.HasIndex(x => x.OrgId)
                .HasDatabaseName("ix_sponsors_org_active")
                .HasFilter("\"DeletedAt\" IS NULL");
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId);
        });

        b.Entity<EventSponsor>(e =>
        {
            e.ToTable("event_sponsors");
            e.HasIndex(x => new { x.EventId, x.SponsorId }).IsUnique();
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Sponsor>().WithMany().HasForeignKey(x => x.SponsorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventSession>(e =>
        {
            e.ToTable("event_sessions");
            e.HasIndex(x => new { x.EventId, x.StartsAt });
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            // V3 §3.4 (Phase 12): an AgendaItem may hold a pool for a seat limit (config-only; RESTRICT — never
            // cascade-delete a live pool from a schedule edit).
            e.HasOne<InventoryPool>().WithMany().HasForeignKey(x => x.InventoryPoolId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EventSessionSpeaker>(e =>
        {
            e.ToTable("event_session_speakers");
            e.HasKey(x => new { x.SessionId, x.SpeakerId });
            e.HasOne<EventSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Speaker>().WithMany().HasForeignKey(x => x.SpeakerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventMedia>(e =>
        {
            e.ToTable("event_media");
            e.HasIndex(x => new { x.EventId, x.Kind });
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        // ════════════════════════════════════════════════════════════════════
        // TICKETING
        // ════════════════════════════════════════════════════════════════════

        b.Entity<TicketType>(e =>
        {
            e.ToTable("ticket_types", t =>
            {
                t.HasCheckConstraint("ck_ticket_types_price_paise", "\"PricePaise\" >= 0");
                t.HasCheckConstraint("ck_ticket_types_quantity", "\"Quantity\" > 0");
                t.HasCheckConstraint("ck_ticket_types_per_user_limit", "\"PerUserLimit\" >= 1");
            });
            e.HasIndex(x => x.EventId)
                .HasDatabaseName("ix_ticket_types_event_active")
                .HasFilter("\"DeletedAt\" IS NULL");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
        });

        b.Entity<FormField>(e =>
        {
            e.ToTable("form_fields");
            e.HasIndex(x => new { x.TicketTypeId, x.Key }).IsUnique();
            e.Property(x => x.OptionsJson).HasColumnType("jsonb");
            e.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<FieldPreset>(e =>
        {
            e.ToTable("field_presets");
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.HasIndex(x => x.CategoryOrTypeSlug);
        });

        b.Entity<SeatHold>(e =>
        {
            e.ToTable("seat_holds", t => t.HasCheckConstraint("ck_seat_holds_qty", "\"Qty\" >= 1"));
            e.HasIndex(x => new { x.TicketTypeId, x.Status });
            e.HasIndex(x => new { x.Status, x.ExpiresAt });
            e.HasIndex(x => x.OrderId);
            e.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId);
            e.HasOne<InventoryPool>().WithMany().HasForeignKey(x => x.PoolId);   // V3 §17.1 (Phase 9) — the held pool
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId);
        });

        b.Entity<TicketTransfer>(e =>
        {
            e.ToTable("ticket_transfers");
            e.HasIndex(x => x.TicketId);
            e.HasIndex(x => x.TransferCode).IsUnique();
            e.HasIndex(x => new { x.ToPhone, x.Status });
            e.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId);
        });

        b.Entity<GateEntry>(e =>
        {
            e.ToTable("gate_entries");
            e.HasIndex(x => new { x.TicketId, x.EventId }).IsUnique();
            e.HasIndex(x => new { x.EventId, x.CreatedAt });
            e.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ScannedBy).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TicketWaitlist>(e =>
        {
            e.ToTable("ticket_waitlist");
            e.HasIndex(x => new { x.TicketTypeId, x.UserId }).IsUnique();
            // Fast queue advancement: lowest position where status = Waiting
            e.HasIndex(x => new { x.TicketTypeId, x.Position })
                .HasDatabaseName("ix_waitlist_type_pos_waiting")
                .HasFilter("\"Status\" = 'Waiting'");
            e.HasIndex(x => new { x.UserId, x.Status });
            // Expiry cleanup job: find notified offers that have passed their deadline
            e.HasIndex(x => x.OfferExpiresAt)
                .HasDatabaseName("ix_waitlist_expiry_notified")
                .HasFilter("\"Status\" = 'Notified'");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<InventoryPool>().WithMany().HasForeignKey(x => x.PoolId);   // V3 §8.5 (Phase 7)
        });

        b.Entity<InventoryPool>(e =>                    // V3 §8.1 (Phase 7), authoritative from Phase 9
        {
            // V3 §17.1 (Phase 9): non-negativity is a DB invariant so a release-below-zero bug fails closed. The
            // upper bound (no oversell beyond allowance) is enforced by the conditional decrement's WHERE clause.
            e.ToTable("inventory_pools", t => t.HasCheckConstraint("ck_inventory_pools_nonneg",
                "\"Held\" >= 0 AND \"Allocated\" >= 0 AND \"Consumed\" >= 0"));
            e.HasIndex(x => x.EventId);
            // One general pool per ticket type (the Phase-7 shadow). Partial-unique so future non-general
            // pools for the same ticket type coexist.
            e.HasIndex(x => new { x.TicketTypeId, x.Segment })
                .IsUnique()
                .HasDatabaseName("ix_inventory_pools_ticket_type_segment")
                .HasFilter("\"TicketTypeId\" IS NOT NULL");
            e.Property(x => x.ReleasePolicyJson).HasColumnType("jsonb");
            e.Property(x => x.WaitlistConfigJson).HasColumnType("jsonb");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventCheckinDevice>(e =>
        {
            e.ToTable("event_checkin_devices");
            e.HasIndex(x => x.DeviceTokenHash).IsUnique();
            e.HasIndex(x => x.EventId)
                .HasDatabaseName("ix_checkin_devices_event_active")
                .HasFilter("\"IsActive\" = true");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RegisteredBy);
        });

        // ════════════════════════════════════════════════════════════════════
        // ORDERS & PAYMENTS
        // ════════════════════════════════════════════════════════════════════

        b.Entity<Order>(e =>
        {
            e.ToTable("orders", t =>
            {
                t.HasCheckConstraint("ck_orders_amount_paise", "\"AmountPaise\" >= 0");
                t.HasCheckConstraint("ck_orders_status", StateVocabulary<OrderStatus>("Status"));
            });
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => x.RazorpayOrderId);
            e.HasIndex(x => new { x.EventId, x.Status });
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasIndex(x => x.GuestAccessToken)
                .IsUnique()
                .HasFilter("\"GuestAccessToken\" IS NOT NULL");
            e.HasIndex(x => x.GuestPhone);
            // V3 §17.1 (Phase 9): client idempotency, SCOPED TO THE CALLER so one caller's key can never collide
            // with another's (which would leak an order / a guest access token). Two partial-unique indexes: one
            // per authenticated (event, user, key), one per guest (event, phone, key).
            e.HasIndex(x => new { x.EventId, x.UserId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("ix_orders_user_idempotency")
                .HasFilter("\"UserId\" IS NOT NULL AND \"IdempotencyKey\" IS NOT NULL");
            e.HasIndex(x => new { x.EventId, x.GuestPhone, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("ix_orders_guest_idempotency")
                .HasFilter("\"UserId\" IS NULL AND \"IdempotencyKey\" IS NOT NULL");
            // V3 §7.6 (Phase 13, review H1): a NONE-identity walk-in has UserId AND GuestPhone both null, so the guest
            // index above (which includes GuestPhone) can't dedupe it — Postgres treats the NULL phone as distinct. This
            // partial-unique index closes the concurrent-replay gap on (event, key) for exactly that case; it touches no
            // other order (a normal guest order always has a GuestPhone, an account order a UserId).
            e.HasIndex(x => new { x.EventId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("ix_orders_walkin_idempotency")
                .HasFilter("\"UserId\" IS NULL AND \"GuestPhone\" IS NULL AND \"IdempotencyKey\" IS NOT NULL");
            e.Property(x => x.AnswersJson).HasColumnType("jsonb");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).IsRequired(false);
        });

        b.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items", t => t.HasCheckConstraint("ck_order_items_qty", "\"Qty\" >= 1"));
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.TicketTypeId);
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId);
            e.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId);
        });

        b.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.HasIndex(x => x.RazorpayPaymentId).IsUnique();
            e.HasIndex(x => x.OrderId);
            e.Property(x => x.WebhookPayloadJson).HasColumnType("jsonb");
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId);
        });

        b.Entity<Refund>(e =>
        {
            e.ToTable("refunds");
            e.HasIndex(x => x.RazorpayRefundId).IsUnique();
            // Non-unique (review P3): full-refund idempotency is guaranteed by the atomic Paid→Refunded status claim
            // in RefundService (a second/concurrent full refund sees Refunded and returns already_refunded), so a
            // UNIQUE(order_id) is unnecessary — and it would forbid V3 §9.6 partial refunds (per-VAR-line refunds ⇒
            // multiple refund rows per order). Partial refunds (a later wave) add their own per-line idempotency key.
            e.HasIndex(x => x.OrderId);
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId);
        });

        // ════════════════════════════════════════════════════════════════════
        // GROUPS & TICKETS
        // ════════════════════════════════════════════════════════════════════

        b.Entity<Group>(e =>
        {
            e.ToTable("groups");
            e.HasIndex(x => x.JoinCode).IsUnique();
            e.HasIndex(x => new { x.EventId, x.GroupNumber }).IsUnique();
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
            e.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<GroupMember>(e =>
        {
            e.ToTable("group_members");
            e.HasIndex(x => x.GroupId);
            e.HasIndex(x => x.UserId);                  // "which groups is this user in?"
            e.Property(x => x.AnswersJson).HasColumnType("jsonb");
            e.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<Ticket>(e =>
        {
            e.ToTable("tickets", t => t.HasCheckConstraint("ck_tickets_state", StateVocabulary<TicketState>("State")));
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.State });
            e.HasIndex(x => new { x.EventId, x.State });
            e.HasIndex(x => x.OrderItemId);
            e.Property(x => x.AnswersJson).HasColumnType("jsonb");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
            e.HasOne<OrderItem>().WithMany().HasForeignKey(x => x.OrderItemId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
            e.HasOne<GroupMember>().WithMany().HasForeignKey(x => x.GroupMemberId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CheckedInBy);
        });

        // ── V3 §7 Registration chain (Phase 8, dual-write shadow) ────────────────
        b.Entity<RegistrationPolicy>(e =>
        {
            e.ToTable("registration_policies");
            e.HasIndex(x => x.TicketTypeId).IsUnique();     // one policy per Pass (ticket type) this phase
            e.HasIndex(x => x.EventId);
            e.Property(x => x.GatesJson).HasColumnType("jsonb");
            e.Property(x => x.DocumentsRequiredJson).HasColumnType("jsonb");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Registration>(e =>
        {
            e.ToTable("registrations");
            e.HasIndex(x => x.OrderId).IsUnique();          // one registration shadows one order
            e.HasIndex(x => x.EventId);
            e.Property(x => x.AnswersJson).HasColumnType("jsonb");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Admission>(e =>
        {
            e.ToTable("admissions");
            e.HasIndex(x => x.TicketId).IsUnique();         // one admission shadows one ticket
            e.HasIndex(x => x.RegistrationId);
            e.HasIndex(x => new { x.EventId, x.PersonId });
            e.HasOne<Registration>().WithMany().HasForeignKey(x => x.RegistrationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
            e.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<InventoryPool>().WithMany().HasForeignKey(x => x.PoolId);
            e.HasOne<Credential>().WithMany().HasForeignKey(x => x.CredentialId);
        });

        // ── V3 §7.5 Delegated registration / SeatBlock (Phase 13) ────────────────
        b.Entity<SeatBlock>(e =>
        {
            e.ToTable("seat_blocks", t => t.HasCheckConstraint("ck_seat_blocks_qty", "\"Quantity\" >= 1 AND \"ReassignLimit\" >= 0"));
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => x.DelegateUserId);
            e.HasIndex(x => x.OrderId).IsUnique();          // one block per funding order
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.RegistrantOrgUnitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.DelegateUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SeatBlockSeat>(e =>
        {
            e.ToTable("seat_block_seats");
            e.HasIndex(x => x.SeatBlockId);
            e.HasIndex(x => x.AdmissionId).IsUnique();      // one seat governs one admission
            e.HasOne<SeatBlock>().WithMany().HasForeignKey(x => x.SeatBlockId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Admission>().WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Credential>(e =>
        {
            e.ToTable("credentials");
            // ONE credential per person per event tree (§7.2). Guests (PersonId null) get one per admission.
            e.HasIndex(x => new { x.EventId, x.PersonId })
                .IsUnique()
                .HasDatabaseName("ix_credentials_person_event")
                .HasFilter("\"PersonId\" IS NOT NULL");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── V3 §9.2/§9.5 Pass + AdmissionRight + VAR (Phase 9 authority cut-over) ─
        b.Entity<Pass>(e =>
        {
            e.ToTable("passes", t => t.HasCheckConstraint("ck_passes_price_paise", "\"PricePaise\" >= 0"));
            e.HasIndex(x => x.TicketTypeId).IsUnique();     // Phase 9 Option A: one Pass per ticket type (1:1)
            e.HasIndex(x => x.EventId);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AdmissionRight>(e =>
        {
            e.ToTable("admission_rights", t => t.HasCheckConstraint("ck_admission_rights_uses", "\"Uses\" >= 1"));
            e.HasIndex(x => x.PassId);
            e.HasIndex(x => x.EventId);
            e.HasOne<Pass>().WithMany().HasForeignKey(x => x.PassId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ValueAllocationRecord>(e =>
        {
            e.ToTable("var_lines", t => t.HasCheckConstraint("ck_var_lines_allocated_paise", "\"AllocatedPaise\" >= 0"));
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.OrderItemId);
            e.HasIndex(x => x.EventId);
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<OrderItem>().WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
        });

        // ── V3 §6 Team subsystem (Phase 10) ──────────────────────────────────────
        b.Entity<Team>(e =>
        {
            e.ToTable("teams");
            e.HasIndex(x => new { x.EventId, x.Slug }).IsUnique();   // one slug per event
            e.HasIndex(x => x.TicketTypeId);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.DeclaredOrgUnitId);
        });

        b.Entity<TeamMembership>(e =>
        {
            e.ToTable("team_memberships");
            e.HasIndex(x => x.TeamId);
            // A person holds at most one LIVE (non-terminal) membership per team — enforced in the service; the
            // partial-unique index backstops the active case. Replaced/Removed/Left rows are retained as history.
            e.HasIndex(x => new { x.TeamId, x.PersonId })
                .IsUnique()
                .HasDatabaseName("ix_team_memberships_active_person")
                .HasFilter("\"PersonId\" IS NOT NULL AND \"State\" IN ('Invited','Requested','Active')");
            e.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.PersonId);
        });

        b.Entity<TeamInvite>(e =>
        {
            e.ToTable("team_invites");
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.TeamId);
            e.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.InviteePersonId);
        });

        b.Entity<TeamJoinRequest>(e =>
        {
            e.ToTable("team_join_requests");
            e.HasIndex(x => x.TeamId);
            // At most one PENDING request per (team, person); decided rows are retained as history.
            e.HasIndex(x => new { x.TeamId, x.PersonId })
                .IsUnique()
                .HasDatabaseName("ix_team_join_requests_pending")
                .HasFilter("\"State\" = 'Pending'");
            e.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TeamPolicy>(e =>
        {
            e.ToTable("team_policies", t => t.HasCheckConstraint("ck_team_policies_size", "\"MinSize\" >= 1 AND \"MaxSize\" >= \"MinSize\""));
            e.HasIndex(x => x.TicketTypeId).IsUnique();   // one policy per competition ticket type
            e.HasIndex(x => x.EventId);
            e.Property(x => x.WaitlistConfigJson).HasColumnType("jsonb");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── V3 §10 Competition engine (Phase 11) ─────────────────────────────────
        b.Entity<Stage>(e =>
        {
            e.ToTable("stages");
            e.HasIndex(x => new { x.EventId, x.Sequence }).IsUnique();   // one sequence slot per event
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId);
            e.HasOne<ScoringPolicy>().WithMany().HasForeignKey(x => x.ScoringPolicyId);
            e.HasOne<InventoryPool>().WithMany().HasForeignKey(x => x.SpectatorPoolId);   // §10.4 config-only link
            e.HasOne<Stage>().WithMany().HasForeignKey(x => x.AdvancedFromStageId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StageParticipant>(e =>
        {
            e.ToTable("stage_participants");
            e.HasIndex(x => new { x.StageId, x.SubjectType, x.SubjectId }).IsUnique();   // a subject is on a stage once
            e.HasOne<Stage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Fixture>(e =>
        {
            e.ToTable("fixtures");
            e.HasIndex(x => new { x.StageId, x.RoundNo });
            e.Property(x => x.ResultJson).HasColumnType("jsonb");
            e.HasOne<Stage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId);
        });

        b.Entity<FixtureParticipant>(e =>
        {
            e.ToTable("fixture_participants");
            e.HasIndex(x => new { x.FixtureId, x.SubjectType, x.SubjectId }).IsUnique();   // a subject appears once per fixture
            e.HasOne<Fixture>().WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<FixtureOfficial>(e =>
        {
            e.ToTable("fixture_officials");
            e.HasIndex(x => new { x.FixtureId, x.ParticipantId }).IsUnique();
            e.HasOne<Fixture>().WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<EventParticipant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ScoringPolicy>(e =>
        {
            e.ToTable("scoring_policies", t => t.HasCheckConstraint("ck_scoring_weight_cap", "\"VoteWeightCapPercent\" >= 0 AND \"VoteWeightCapPercent\" <= 100"));
            e.HasIndex(x => x.EventId);
            e.Property(x => x.SourcesJson).HasColumnType("jsonb");
            e.Property(x => x.TieBreakJson).HasColumnType("jsonb");
            e.Property(x => x.ConflictRulesJson).HasColumnType("jsonb");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<JudgeScore>(e =>
        {
            e.ToTable("judge_scores");
            // One live score per (stage, judge, subject) — a re-submission updates it (duplicate prevention).
            e.HasIndex(x => new { x.StageId, x.JudgeParticipantId, x.SubjectType, x.SubjectId })
                .IsUnique().HasDatabaseName("ix_judge_scores_unique");
            e.Property(x => x.BreakdownJson).HasColumnType("jsonb");
            e.HasOne<Stage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Fixture>().WithMany().HasForeignKey(x => x.FixtureId);
            // Restrict (H2): a judge's scores are immutable evidence (§10.5) — removing the judge participant must not
            // erase them. Participants are soft-removed (ParticipantState.Removed), so this blocks only a hard delete.
            e.HasOne<EventParticipant>().WithMany().HasForeignKey(x => x.JudgeParticipantId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PublicVote>(e =>
        {
            e.ToTable("public_votes");
            // Exactly one vote per (stage, voter) — the enforceable one-vote-per-identity rule (§10.3). Immutable.
            e.HasIndex(x => new { x.StageId, x.VoterUserId }).IsUnique().HasDatabaseName("ix_public_votes_one_per_voter");
            e.HasIndex(x => new { x.VoterUserId, x.CreatedAt });   // rate-limit window scan
            e.HasOne<Stage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Cascade);
            // Restrict (H2): the public tally is an immutable audit (§10.3) — deleting a user must not silently drop
            // their vote. Stage deletion still cascades (StageId), so removing a whole stage clears its votes.
            e.HasOne<User>().WithMany().HasForeignKey(x => x.VoterUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StageResult>(e =>
        {
            e.ToTable("stage_results");
            e.HasIndex(x => new { x.StageId, x.SubjectType, x.SubjectId }).IsUnique();
            e.Property(x => x.ScoreBreakdownJson).HasColumnType("jsonb");
            e.HasOne<Stage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ResultCorrection>(e =>
        {
            e.ToTable("result_corrections");
            e.HasIndex(x => x.ResultId);
            e.Property(x => x.PreviousValueJson).HasColumnType("jsonb");
            e.HasOne<StageResult>().WithMany().HasForeignKey(x => x.ResultId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Transfer>(e =>
        {
            e.ToTable("transfers");
            e.HasIndex(x => x.RazorpayTransferId).IsUnique();
            e.HasIndex(x => x.OrgId);
            e.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
        });

        // ════════════════════════════════════════════════════════════════════
        // MONEY / LEDGER
        // ════════════════════════════════════════════════════════════════════

        b.Entity<LedgerEntry>(e =>
        {
            e.ToTable("ledger_entries", t => t.HasCheckConstraint("ck_ledger_entries_state", StateVocabulary<LedgerState>("State")));
            e.HasIndex(x => new { x.OrgId, x.State });
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => new { x.OrgId, x.State, x.CreatedAt });
            e.HasIndex(x => new { x.RefType, x.RefId });
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Withdrawal>(e =>
        {
            e.ToTable("withdrawals", t => t.HasCheckConstraint("ck_withdrawals_amount_paise", "\"AmountPaise\" > 0"));
            e.HasIndex(x => x.OrgId);
            e.HasIndex(x => new { x.OrgId, x.Status });
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
        });

        // ════════════════════════════════════════════════════════════════════
        // CERTIFICATES & DESIGN
        // ════════════════════════════════════════════════════════════════════

        b.Entity<DesignTemplate>(e =>
        {
            e.ToTable("design_templates");
            e.HasIndex(x => new { x.Kind, x.IsActive });
            e.Property(x => x.PlacementsJson).HasColumnType("jsonb");
            e.Property(x => x.SignatoryJson).HasColumnType("jsonb");
            // Optional FK to events for event-specific template overrides (D2 decision)
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId);
        });

        b.Entity<GeneratedCard>(e =>
        {
            e.ToTable("generated_cards");
            e.HasIndex(x => x.EventId);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<DesignTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── The certificate module (D-355) ──────────────────────────────────────────────────────
        //
        // Configured with NO navigation properties, matching the convention used throughout this file.
        // That is load-bearing here rather than stylistic: it is what lets these tables reference Event
        // and User by id without the certificate entities gaining a compile-time dependency on the
        // ticketing graph, and it is what would make the module extractable later.

        b.Entity<CertificateTemplate>(e =>
        {
            e.ToTable("certificate_templates",
                t => t.HasCheckConstraint("ck_certificate_templates_status", StateVocabulary<CertificateTemplateStatus>("Status")));
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.PageSize).HasMaxLength(20);
            e.HasIndex(x => x.OwnerUserId);
            // The reusable-library query: a creator's templates that belong to no event.
            e.HasIndex(x => new { x.OwnerUserId, x.EventId });
            e.HasIndex(x => x.EventId);
            // SetNull, not Cascade: a template outlives the event it was first used on, because that is
            // exactly what makes it reusable. Deleting the event returns it to the creator's library.
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CertificateTemplateField>(e =>
        {
            e.ToTable("certificate_template_fields");
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.FieldKey).HasMaxLength(100);
            e.Property(x => x.Label).HasMaxLength(200);
            e.Property(x => x.HorizontalAlignment).HasMaxLength(10);
            e.Property(x => x.VerticalAlignment).HasMaxLength(10);
            e.Property(x => x.FontFamily).HasMaxLength(100);
            e.Property(x => x.FontWeight).HasMaxLength(20);
            e.Property(x => x.Color).HasMaxLength(9);
            e.Property(x => x.BackgroundColor).HasMaxLength(9);
            // Paint order is read for every render, always scoped to one template.
            e.HasIndex(x => new { x.TemplateId, x.ZOrder });
            // Cascade: a field has no meaning apart from the template it sits on.
            e.HasOne<CertificateTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CertificateIdRule>(e =>
        {
            e.ToTable("certificate_id_rules",
                // NextSequence only ever moves forward, and only by the atomic allocator. A negative or
                // zero value would mean something wrote it directly.
                t => t.HasCheckConstraint("ck_certificate_id_rules_sequence", "\"NextSequence\" >= 1"));
            e.Property(x => x.Prefix).HasMaxLength(32).IsRequired();
            e.Property(x => x.Pattern).HasMaxLength(120).IsRequired();
            // Unique per event — this is what the allocator's INSERT … ON CONFLICT relies on, so it is a
            // correctness constraint rather than a lookup optimisation.
            e.HasIndex(x => x.EventId).IsUnique();
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CertificateBatch>(e =>
        {
            e.ToTable("certificate_batches",
                t => t.HasCheckConstraint("ck_certificate_batches_status", StateVocabulary<CertificateBatchStatus>("Status")));
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.SourceFileName).HasMaxLength(260);
            e.Property(x => x.ColumnMappingJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.EventId, x.Status });
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            // Restrict: a template that produced certificates someone holds must not be deletable out
            // from under the run that records them.
            e.HasOne<CertificateTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CertificateRecipient>(e =>
        {
            e.ToTable("certificate_recipients");
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.NormalizedEmail).HasMaxLength(320);
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => x.BatchId);
            // One recipient per row of an import. Two recipients claiming row 47 would make the batch
            // run's row → recipient lookup ambiguous, and a re-import must not silently double the list.
            e.HasIndex(x => new { x.BatchId, x.SourceRowNumber })
                .IsUnique()
                .HasFilter("\"BatchId\" IS NOT NULL AND \"SourceRowNumber\" IS NOT NULL");
            // The verified-email linking lookup. NOT unique: the same person legitimately appears on
            // several events, and two people can share a family address.
            e.HasIndex(x => x.NormalizedEmail);
            e.HasIndex(x => x.UserId);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<CertificateBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.SetNull);
            // SetNull, and deliberately so: if an account is deleted the certificate still exists and was
            // still issued to that person. Losing the link is correct; losing the recipient is not.
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<IssuedCertificate>(e =>
        {
            e.ToTable("issued_certificates",
                t => t.HasCheckConstraint("ck_issued_certificates_status", StateVocabulary<IssuedCertificateStatus>("Status")));
            e.Property(x => x.CertificateId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.FieldValuesJson).HasColumnType("jsonb");
            e.Property(x => x.DocumentSha256).HasMaxLength(64);
            e.Property(x => x.SignatureKeyId).HasMaxLength(64);

            // THE uniqueness guarantee. Platform-wide, not per-event: verification is by this value alone,
            // with no event context to disambiguate. The application allocator makes collisions rare; this
            // index is what makes them impossible.
            e.HasIndex(x => x.CertificateId).IsUnique();

            // Immutable after insert. EF throws on any attempt to change it rather than silently issuing
            // an UPDATE — a certificate id that can drift is not an identifier.
            e.Property(x => x.CertificateId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            e.HasIndex(x => new { x.EventId, x.Status });
            e.HasIndex(x => x.RecipientId);
            e.HasIndex(x => x.BatchId);

            // What makes a batch retry safe (D-355, Phase 7). The run skips rows it has already issued,
            // but a skip-list read at the top of a loop stops being true the moment two workers pick up
            // the same job — so one certificate per (batch, recipient) is enforced here, where concurrency
            // cannot get around it. Filtered, because a hand-issued certificate has no batch and several
            // may legitimately go to one recipient.
            e.HasIndex(x => new { x.BatchId, x.RecipientId })
                .IsUnique()
                .HasFilter("\"BatchId\" IS NOT NULL");

            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<CertificateTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CertificateBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<CertificateRecipient>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
            // The reissue chain. Restrict: the superseded original must survive its replacement, which is
            // the entire point of keeping lineage.
            e.HasOne<IssuedCertificate>().WithMany().HasForeignKey(x => x.SupersedesCertificateId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CertificateRevocation>(e =>
        {
            e.ToTable("certificate_revocations");
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.CertificateId);
            e.HasOne<IssuedCertificate>().WithMany().HasForeignKey(x => x.CertificateId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<IssuedCertificate>().WithMany().HasForeignKey(x => x.ReplacementCertificateId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CertificateAccessLink>(e =>
        {
            e.ToTable("certificate_access_links");
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();

            // THE lookup, and unique: two links resolving to one hash would mean a token that grants
            // access to more than one person's certificates.
            e.HasIndex(x => x.TokenHash).IsUnique();

            e.HasIndex(x => x.RecipientId);
            e.HasOne<CertificateRecipient>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.IsLive);
        });

        b.Entity<CertificateDelivery>(e =>
        {
            e.ToTable("certificate_deliveries",
                t => t.HasCheckConstraint("ck_certificate_deliveries_status", StateVocabulary<CertificateDeliveryStatus>("Status")));
            e.Property(x => x.Channel).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Destination).HasMaxLength(320);
            e.Property(x => x.ProviderMessageId).HasMaxLength(200);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasIndex(x => new { x.CertificateId, x.Channel });
            e.HasIndex(x => x.Status);
            e.HasOne<IssuedCertificate>().WithMany().HasForeignKey(x => x.CertificateId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CertificateEvent>(e =>
        {
            e.ToTable("certificate_events");
            e.Property(x => x.Type).HasMaxLength(20);
            e.Property(x => x.CorrelationId).HasMaxLength(64);
            // The dashboard counts group by type within a certificate.
            e.HasIndex(x => new { x.CertificateId, x.Type });
            e.HasOne<IssuedCertificate>().WithMany().HasForeignKey(x => x.CertificateId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CertificateSigningKey>(e =>
        {
            e.ToTable("certificate_signing_keys",
                t => t.HasCheckConstraint("ck_certificate_signing_keys_state", StateVocabulary<CertificateSigningKeyState>("State")));
            e.Property(x => x.KeyId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Algorithm).HasMaxLength(16).IsRequired();
            e.Property(x => x.State).HasMaxLength(20);
            e.Property(x => x.ProtectionScheme).HasMaxLength(32);
            e.Property(x => x.CompromisedReason).HasMaxLength(500);
            // Verification selects the exact key a certificate names, so this lookup must be unique and
            // fast — it runs on every public verification.
            e.HasIndex(x => x.KeyId).IsUnique();
            e.HasIndex(x => x.State);
            // No foreign keys at all: signing keys belong to the module, not to any event or user.
        });

        b.Entity<IdCard>(e =>
        {
            e.ToTable("id_cards");
            // Verification is a lookup by this code, so it is unique platform-wide and indexed for it.
            e.HasIndex(x => x.VerifyCode).IsUnique();
            // Card numbers are only promised unique within the issuing organization (D-331) — two
            // colleges may legitimately both number a card "2024/0001".
            e.HasIndex(x => new { x.OrgId, x.CardNumber }).IsUnique();
            // "My cards" and the admin roster are the two list reads; both filter on one of these.
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.OrgId, x.Status });
            e.HasIndex(x => x.EventId);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
            e.Property(x => x.LayoutJson).HasColumnType("jsonb");
        });

        b.Entity<EntitlementProduct>(e =>
        {
            e.ToTable("entitlement_products");
            // The organiser's list, and the participant's "what can I buy here" — both filter on event,
            // and the participant's also on published.
            e.HasIndex(x => new { x.EventId, x.IsPublished });
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.CustomLabel).HasMaxLength(60);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.RedemptionLocationsJson).HasColumnType("jsonb");
            e.Property(x => x.TagsJson).HasColumnType("jsonb");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
        });

        b.Entity<EntitlementGrant>(e =>
        {
            e.ToTable("entitlement_grants");
            // The QR resolves by this and nothing else, so it is unique platform-wide and indexed for a
            // single-row lookup at the counter.
            e.HasIndex(x => x.SecureToken).IsUnique();
            // Printed numbers are promised unique per event only — two events may both print "L-0001".
            e.HasIndex(x => new { x.EntitlementProductId, x.CouponNumber }).IsUnique();
            // "My coupons" is the participant read; the organiser's roster filters by product + status.
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.EntitlementProductId, x.Status });
            e.HasIndex(x => x.RegistrationId);
            e.Property(x => x.SecureToken).HasMaxLength(64).IsRequired();
            e.Property(x => x.CouponNumber).HasMaxLength(40);
            e.HasOne<EntitlementProduct>().WithMany().HasForeignKey(x => x.EntitlementProductId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<EntitlementRedemption>(e =>
        {
            e.ToTable("entitlement_redemptions");
            // The retry guard (D-334 §21). A scanner that re-sends on a dropped connection must record
            // one redemption, not two — this index is what makes that true rather than hoped for.
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => x.EntitlementGrantId);
            e.Property(x => x.IdempotencyKey).HasMaxLength(80).IsRequired();
            e.Property(x => x.LocationCode).HasMaxLength(40);
            e.HasOne<EntitlementGrant>().WithMany().HasForeignKey(x => x.EntitlementGrantId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RedeemedByUserId);
        });

        b.Entity<Certificate>(e =>
        {
            e.ToTable("certificates");
            e.HasIndex(x => x.VerifyCode).IsUnique();
            e.HasIndex(x => new { x.EventId, x.TicketId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.IsPublic });
            e.HasOne<DesignTemplate>().WithMany().HasForeignKey(x => x.TemplateId);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
            e.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        // ════════════════════════════════════════════════════════════════════
        // INVITATIONS, ANNOUNCEMENTS & CHAT
        // ════════════════════════════════════════════════════════════════════

        b.Entity<EventInvitation>(e =>
        {
            e.ToTable("event_invitations");
            e.HasIndex(x => x.InviteToken).IsUnique();
            e.HasIndex(x => new { x.EventId, x.SendStatus });
            e.HasIndex(x => new { x.EventId, x.RsvpStatus });
            // Partial unique: one invite per email per event
            e.HasIndex(x => new { x.EventId, x.Email })
                .IsUnique()
                .HasFilter("\"Email\" IS NOT NULL");
            // Partial unique: one invite per phone per event
            e.HasIndex(x => new { x.EventId, x.Phone })
                .IsUnique()
                .HasFilter("\"Phone\" IS NOT NULL");
            e.HasIndex(x => x.GroupId);
            // D-266 M6 — one invite per invited USER per event, the Method A analogue of the email/phone
            // partial uniques above. Without it an organiser could invite the same person twice and the
            // invitee would see two rows for one invitation.
            e.HasIndex(x => new { x.EventId, x.InvitedUserId })
                .IsUnique()
                .HasFilter("\"InvitedUserId\" IS NOT NULL");
            // The invitee's inbox query (GET /v1/me/invitations).
            e.HasIndex(x => new { x.InvitedUserId, x.RsvpStatus });
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.InvitedBy);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.InvitedUserId);
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId);
            e.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        });

        // D-266 M6 (D9 Method B) — the shareable link. Separate from EventInvitation because a link has
        // seats and an expiry that a per-invitee token cannot express.
        b.Entity<EventInviteLink>(e =>
        {
            e.ToTable("event_invite_links");
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => new { x.EventId, x.Status });
            e.Property(x => x.Token).HasMaxLength(64).IsRequired();
            e.Property(x => x.PasscodeHash).HasMaxLength(400);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy);
        });

        b.Entity<EventInviteLinkRedemption>(e =>
        {
            e.ToTable("event_invite_link_redemptions");
            // The uniqueness that makes redeeming twice idempotent (D9 rule 7) — enforced by the database,
            // not by a read-then-write in the service, which two concurrent redemptions would both pass.
            e.HasIndex(x => new { x.InviteLinkId, x.UserId }).IsUnique();
            e.HasOne<EventInviteLink>().WithMany().HasForeignKey(x => x.InviteLinkId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<EventAnnouncement>(e =>
        {
            e.ToTable("event_announcements");
            e.HasIndex(x => new { x.EventId, x.CreatedAt });
            e.HasIndex(x => new { x.Status, x.ScheduledAt });
            e.Property(x => x.Channels).HasColumnType("text[]");
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy);
        });

        b.Entity<ChatRoom>(e =>
        {
            e.ToTable("chat_rooms");
            // D-104: (EventId, Kind) rather than (EventId) — one General room per event today, and a
            // second kind later is an index change with no data migration.
            //
            // D-264 scopes it to non-null EventId. Direct rooms have no event, and without the filter
            // every DM would collide on (NULL, Direct) in Postgres... except it would NOT, because NULLs
            // are distinct in a unique index — which is worse: the index would silently stop meaning
            // anything for DMs while looking like it still applied.
            e.HasIndex(x => new { x.EventId, x.Kind }).IsUnique()
                .HasFilter("\"EventId\" IS NOT NULL");
            // One room per pair, database-enforced. Two people tapping Message at the same instant both
            // see no room and both insert; this picks the winner.
            e.HasIndex(x => new { x.DirectLowUserId, x.DirectHighUserId }).IsUnique()
                .HasDatabaseName("ix_chat_rooms_direct_pair")
                .HasFilter("\"DirectLowUserId\" IS NOT NULL");
            // SetNull, not Cascade: a DM has no event, and deleting an event must not take unrelated
            // rooms with it.
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.DirectLowUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.DirectHighUserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChatMember>(e =>
        {
            e.ToTable("chat_members");
            e.HasIndex(x => new { x.RoomId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.RoomId, x.Role });
            e.HasOne<ChatRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<ChatMessage>(e =>
        {
            e.ToTable("chat_messages");
            // D-104 keyset pagination: (CreatedAt, Id) is the sort key, so the index carries both.
            // Prefix-compatible with the (RoomId, CreatedAt) index it replaces.
            e.HasIndex(x => new { x.RoomId, x.CreatedAt, x.Id });
            e.HasIndex(x => new { x.RoomId, x.ClientMessageId }).IsUnique();
            // Partial index: pinned messages lookup — IsPinned uses properly quoted identifier
            e.HasIndex(x => x.RoomId)
                .HasDatabaseName("ix_chat_messages_pinned")
                .HasFilter("\"IsPinned\" = true");
            e.HasOne<ChatRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId);
            e.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.ReplyToMessageId);
        });

        b.Entity<ChatMessageReaction>(e =>              // D-295 reactions
        {
            e.ToTable("chat_message_reactions");
            // The triple IS the fact: one of each emoji per person per message. Also the idempotency
            // guard — two taps racing converge on one row rather than stacking or erroring.
            e.HasIndex(x => new { x.MessageId, x.UserId, x.Emoji }).IsUnique();
            // Every read is "all reactions on these messages", so the message side leads.
            e.HasIndex(x => x.MessageId);
            e.Property(x => x.Emoji).HasMaxLength(16);
            e.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChatMessageHide>(e =>                  // D-293 delete-for-me
        {
            e.ToTable("chat_message_hides");
            // The pair is the fact, and the unique index is what makes hiding idempotent: a retried
            // tap loses the insert and converges on the existing row rather than erroring.
            e.HasIndex(x => new { x.MessageId, x.UserId }).IsUnique();
            // Every read filters "messages in this room not hidden by ME", so the user side leads.
            e.HasIndex(x => x.UserId);
            // Cascade from the message: a hide is meaningless once the message is gone, and leaving
            // orphans would slowly poison the NOT EXISTS filter every message read now runs.
            e.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChatAttachment>(e =>
        {
            e.ToTable("chat_attachments");
            // One storage object is one attachment: a repeated confirm converges instead of
            // creating a second row pointing at the same bytes.
            e.HasIndex(x => x.StorageKey).IsUnique();
            e.HasIndex(x => x.MessageId);
            // Serves the orphan sweep, which scans unclaimed rows by age.
            e.HasIndex(x => new { x.RoomId, x.CreatedAt });
            e.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<ChatRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedBy);
        });

        // ════════════════════════════════════════════════════════════════════
        // MESSAGING & LOGS
        // ════════════════════════════════════════════════════════════════════

        b.Entity<EmailLog>(e =>
        {
            e.ToTable("email_logs");
            e.HasIndex(x => new { x.RelatedType, x.RelatedId });
            e.HasIndex(x => new { x.ToEmail, x.CreatedAt });  // delivery history per recipient
        });

        b.Entity<WhatsAppMessage>(e =>
        {
            e.ToTable("whatsapp_messages");
            // Partial unique index: Wamid is only set after successful send — uses quoted identifier
            e.HasIndex(x => x.Wamid)
                .IsUnique()
                .HasFilter("\"Wamid\" IS NOT NULL");
            e.HasIndex(x => x.Status);
            e.HasIndex(x => new { x.RelatedType, x.RelatedId });
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });

        // ════════════════════════════════════════════════════════════════════
        // ANALYTICS & GAMIFICATION (V2)
        // ════════════════════════════════════════════════════════════════════

        // Append-only view stream (D-130). Highest-write table on the platform, so it carries only the
        // index the nightly rollup needs — never a per-event read index, because nothing reads it live.
        b.Entity<EventView>(e =>
        {
            e.ToTable("event_views");
            e.HasIndex(x => new { x.EventId, x.ViewedAt });
            e.HasIndex(x => x.ViewedAt);                 // retention sweep
            e.Property(x => x.VisitorKey).HasMaxLength(64).IsRequired();
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<PointsLedger>(e =>
        {
            e.ToTable("points_ledger");
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<Badge>(e =>
        {
            e.ToTable("badges");
            e.HasIndex(x => x.Type).IsUnique();
        });

        b.Entity<UserBadge>(e =>
        {
            e.ToTable("user_badges");
            e.HasIndex(x => new { x.UserId, x.BadgeId }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Badge>().WithMany().HasForeignKey(x => x.BadgeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Leaderboard>(e =>
        {
            e.ToTable("leaderboards");
            e.HasIndex(x => new { x.Scope, x.ScopeId, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.Scope, x.ScopeId, x.Points });
        });

        b.Entity<ReferralReward>(e =>
        {
            e.ToTable("referral_rewards");
            e.HasIndex(x => x.ReferrerUserId);
            e.HasIndex(x => x.RefereeUserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReferrerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RefereeUserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ════════════════════════════════════════════════════════════════════
        // POSTS (D-262)
        // ════════════════════════════════════════════════════════════════════

        b.Entity<Post>(e =>
        {
            e.ToTable("posts");
            // Keyset pagination on the (CreatedAt, Id) pair — the same sort key chat uses (D-104), and
            // the reason the index carries both columns rather than CreatedAt alone.
            e.HasIndex(x => new { x.AuthorId, x.CreatedAt, x.Id });
            e.HasIndex(x => new { x.CreatedAt, x.Id });
            // Serves the per-event feed; partial because most posts carry no event.
            e.HasIndex(x => new { x.EventId, x.CreatedAt })
                .HasDatabaseName("ix_posts_event_active")
                .HasFilter("\"EventId\" IS NOT NULL");
            e.HasIndex(x => x.SharedPostId);
            e.Property(x => x.Body).HasMaxLength(3000);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.SetNull);
            // Restrict, not Cascade: a reshare must survive its original being removed, otherwise
            // deleting one post silently deletes other people's posts.
            e.HasOne<Post>().WithMany().HasForeignKey(x => x.SharedPostId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PostMedia>(e =>
        {
            e.ToTable("post_media");
            // One storage object is one media row: a repeated confirm converges instead of producing a
            // second row over the same bytes.
            e.HasIndex(x => x.StorageKey).IsUnique();
            e.HasIndex(x => new { x.PostId, x.Sort });
            // Serves the orphan sweep, which scans unclaimed rows by age.
            e.HasIndex(x => new { x.UploadedBy, x.CreatedAt });
            e.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostPoll>(e =>
        {
            e.ToTable("post_polls");
            e.HasIndex(x => x.PostId).IsUnique();
            e.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostPollOption>(e =>
        {
            e.ToTable("post_poll_options");
            e.HasIndex(x => new { x.PollId, x.Sort });
            e.HasOne<PostPoll>().WithMany().HasForeignKey(x => x.PollId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostPollBallot>(e =>
        {
            e.ToTable("post_poll_ballots");
            // The constraint that makes one-vote-per-person true under concurrency. A ballot is N vote
            // rows, and "at most one ballot" is not expressible over N rows — so it lives here.
            e.HasIndex(x => new { x.PollId, x.UserId }).IsUnique();
            e.HasOne<PostPoll>().WithMany().HasForeignKey(x => x.PollId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostPollVote>(e =>
        {
            e.ToTable("post_poll_votes");
            e.HasIndex(x => new { x.PollId, x.UserId, x.OptionId }).IsUnique();
            e.HasIndex(x => x.OptionId);
            e.HasOne<PostPoll>().WithMany().HasForeignKey(x => x.PollId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<PostPollOption>().WithMany().HasForeignKey(x => x.OptionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostLike>(e =>
        {
            e.ToTable("post_likes");
            // Idempotent liking is this index, not an application check: two concurrent likes both see
            // no row, and only one of them survives the insert.
            e.HasIndex(x => new { x.PostId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostComment>(e =>
        {
            e.ToTable("post_comments");
            e.HasIndex(x => new { x.PostId, x.CreatedAt, x.Id });
            e.HasIndex(x => x.ParentCommentId);
            e.Property(x => x.Body).HasMaxLength(1000);
            e.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<PostComment>().WithMany().HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PostCommentLike>(e =>
        {
            e.ToTable("post_comment_likes");
            e.HasIndex(x => new { x.CommentId, x.UserId }).IsUnique();
            e.HasOne<PostComment>().WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostSave>(e =>
        {
            e.ToTable("post_saves");
            e.HasIndex(x => new { x.PostId, x.UserId }).IsUnique();
            // The saved-posts listing pages by (CreatedAt, PostId) of the SAVE, not of the post.
            e.HasIndex(x => new { x.UserId, x.CreatedAt, x.PostId });
            e.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostHashtag>(e =>
        {
            e.ToTable("post_hashtags");
            e.HasIndex(x => new { x.PostId, x.Tag }).IsUnique();
            // Serves both the per-tag feed and the trending roll-up.
            e.HasIndex(x => x.Tag);
            e.Property(x => x.Tag).HasMaxLength(30);
            e.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostMention>(e =>
        {
            e.ToTable("post_mentions");
            e.HasIndex(x => new { x.PostId, x.MentionedUserId }).IsUnique();
            e.HasIndex(x => x.MentionedUserId);
            e.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.MentionedUserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ════════════════════════════════════════════════════════════════════
        // ACCOUNT SETTINGS (D-263)
        // ════════════════════════════════════════════════════════════════════

        b.Entity<NotificationPreference>(e =>
        {
            e.ToTable("notification_preferences");
            // One row per (user, category) — the lookup dispatch does on every notification.
            e.HasIndex(x => new { x.UserId, x.Category }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserBlock>(e =>
        {
            e.ToTable("user_blocks");
            // Idempotent blocking is this index, not an application check: two concurrent taps both see
            // no row and only one survives the insert.
            e.HasIndex(x => new { x.BlockerId, x.BlockedId }).IsUnique();
            // Serves the reverse lookup — enforcement is symmetric, so every read path checks both
            // directions and the second one needs an index too.
            e.HasIndex(x => x.BlockedId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.BlockerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.BlockedId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>`"Column" IN ('A','B',…)` built from the enum itself (DB-8).
    ///
    /// <para><b>Vocabulary, never transitions.</b> This says which values may exist, not which may follow
    /// which, and not who may make the change — those are <c>EventStatusWorkflow</c>'s and
    /// <c>IEventAuthority</c>'s, and moving either into the database would put a product rule somewhere it
    /// cannot be reviewed, tested or explained to a user.</para>
    ///
    /// <para><b>Derived from <c>Enum.GetNames</c> rather than written out.</b> These columns are stored by
    /// member name, so a hand-typed list is a second copy of the vocabulary that silently rots the first
    /// time someone adds a member — and the failure mode is a legitimate write rejected in production. The
    /// migration snapshots this string, so adding an enum member produces a real migration diff, which is
    /// exactly the review moment that ought to happen.</para></summary>
    private static string StateVocabulary<TEnum>(string column) where TEnum : struct, Enum
        => $"\"{column}\" IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"))})";
}
