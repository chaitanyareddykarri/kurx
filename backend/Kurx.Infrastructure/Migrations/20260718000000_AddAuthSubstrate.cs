using System;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    // Trusted Device Authentication substrate (AM0) — purely additive: 8 new tables + 2 nullable
    // columns on refresh_tokens. Zero backfill, zero-downtime. (These tables were inert when this
    // migration shipped; the rails that use them landed in AM2-AM7 and are always on — D-115.)
    // Ids are UUID v7 (generated app-side, ADR-AM13) so the column type is plain uuid. Enums persist
    // as text (KurxDbContext convention). Hand-authored — the EF design-time host can't load locally
    // (Application Control blocks StackExchange.Redis, 0x800711C7); the [Migration] attribute makes
    // MigrateAsync discover + apply it on CI, exactly like AddUserModeration/AddEventMode.
    // Design: docs/auth/AUTHENTICATION_DATABASE.md §2.
    [DbContext(typeof(KurxDbContext))]
    [Migration("20260718000000_AddAuthSubstrate")]
    public partial class AddAuthSubstrate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trusted_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    LifecycleState = table.Column<string>(type: "text", nullable: false),
                    AttestationJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StateChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trusted_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trusted_devices_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrustedDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialType = table.Column<string>(type: "text", nullable: false),
                    PublicKeySpki = table.Column<string>(type: "text", nullable: false),
                    Alg = table.Column<string>(type: "text", nullable: false),
                    WebAuthnCredentialId = table.Column<string>(type: "text", nullable: true),
                    SignatureCounter = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_credentials_trusted_devices_TrustedDeviceId",
                        column: x => x.TrustedDeviceId,
                        principalTable: "trusted_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "auth_challenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Nonce = table.Column<string>(type: "text", nullable: false),
                    ContextJson = table.Column<string>(type: "jsonb", nullable: true),
                    MatchNumber = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ApprovedByDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_challenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_auth_challenges_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_auth_challenges_trusted_devices_ApprovedByDeviceId",
                        column: x => x.ApprovedByDeviceId,
                        principalTable: "trusted_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "auth_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrustedDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastRotatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokeReason = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_auth_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_auth_sessions_trusted_devices_TrustedDeviceId",
                        column: x => x.TrustedDeviceId,
                        principalTable: "trusted_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "security_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    ContextJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_security_events_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "recovery_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recovery_codes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "otp_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Destination = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    PepperVersion = table.Column<int>(type: "integer", nullable: false),
                    RequestIp = table.Column<string>(type: "text", nullable: true),
                    VerifyAttempts = table.Column<int>(type: "integer", nullable: false),
                    Consumed = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoPKeyThumbprint",
                table: "refresh_tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_auth_sessions_SessionId",
                table: "refresh_tokens",
                column: "SessionId",
                principalTable: "auth_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ── Indexes ──────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_trusted_devices_UserId_LifecycleState",
                table: "trusted_devices",
                columns: new[] { "UserId", "LifecycleState" });

            migrationBuilder.CreateIndex(
                name: "IX_device_credentials_TrustedDeviceId",
                table: "device_credentials",
                column: "TrustedDeviceId");

            migrationBuilder.CreateIndex(
                name: "ix_device_credentials_webauthn",
                table: "device_credentials",
                column: "WebAuthnCredentialId",
                unique: true,
                filter: "\"WebAuthnCredentialId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_auth_challenges_Nonce",
                table: "auth_challenges",
                column: "Nonce",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_auth_challenges_UserId_Status_ExpiresAt",
                table: "auth_challenges",
                columns: new[] { "UserId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_auth_challenges_ApprovedByDeviceId",
                table: "auth_challenges",
                column: "ApprovedByDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_UserId_FamilyId",
                table: "auth_sessions",
                columns: new[] { "UserId", "FamilyId" });

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_TrustedDeviceId",
                table: "auth_sessions",
                column: "TrustedDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_security_events_UserId_CreatedAt",
                table: "security_events",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_security_events_Type_CreatedAt",
                table: "security_events",
                columns: new[] { "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_recovery_codes_UserId",
                table: "recovery_codes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_otp_codes_dest_unexpired",
                table: "otp_codes",
                columns: new[] { "Destination", "Purpose", "ExpiresAt" },
                filter: "\"Consumed\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_IdempotencyKey",
                table: "outbox_messages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                table: "outbox_messages",
                columns: new[] { "Status", "CreatedAt" },
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_session",
                table: "refresh_tokens",
                column: "SessionId",
                filter: "\"SessionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_refresh_tokens_auth_sessions_SessionId",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_session",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(name: "SessionId", table: "refresh_tokens");
            migrationBuilder.DropColumn(name: "PoPKeyThumbprint", table: "refresh_tokens");

            migrationBuilder.DropTable(name: "device_credentials");
            migrationBuilder.DropTable(name: "auth_challenges");
            migrationBuilder.DropTable(name: "security_events");
            migrationBuilder.DropTable(name: "recovery_codes");
            migrationBuilder.DropTable(name: "otp_codes");
            migrationBuilder.DropTable(name: "outbox_messages");
            // auth_sessions after refresh_tokens FK is dropped; trusted_devices last (children gone).
            migrationBuilder.DropTable(name: "auth_sessions");
            migrationBuilder.DropTable(name: "trusted_devices");
        }
    }
}
