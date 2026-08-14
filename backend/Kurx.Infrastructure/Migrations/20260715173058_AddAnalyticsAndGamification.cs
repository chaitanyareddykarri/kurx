using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsAndGamification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppVersion",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "devices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DeviceName",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "badges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IconKey = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_badges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "event_analytics_daily",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Views = table.Column<int>(type: "integer", nullable: false),
                    UniqueVisitors = table.Column<int>(type: "integer", nullable: false),
                    Registrations = table.Column<int>(type: "integer", nullable: false),
                    PaidRegistrations = table.Column<int>(type: "integer", nullable: false),
                    FreeRegistrations = table.Column<int>(type: "integer", nullable: false),
                    RevenuePaise = table.Column<long>(type: "bigint", nullable: false),
                    CheckIns = table.Column<int>(type: "integer", nullable: false),
                    WaitlistCount = table.Column<int>(type: "integer", nullable: false),
                    RefundCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_analytics_daily", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "leaderboards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    ScopeId = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leaderboards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevenuePaise = table.Column<long>(type: "bigint", nullable: false),
                    Views = table.Column<int>(type: "integer", nullable: false),
                    UniqueVisitors = table.Column<int>(type: "integer", nullable: false),
                    Registrations = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_analytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "points_ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_points_ledger", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "referral_rewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferrerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefereeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AmountPaise = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_rewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_referral_rewards_users_RefereeUserId",
                        column: x => x.RefereeUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_referral_rewards_users_ReferrerUserId",
                        column: x => x.ReferrerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_badges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BadgeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_badges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_badges_badges_BadgeId",
                        column: x => x.BadgeId,
                        principalTable: "badges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_badges_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_badges_Type",
                table: "badges",
                column: "Type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_analytics_daily_EventId_Date",
                table: "event_analytics_daily",
                columns: new[] { "EventId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leaderboards_Scope_ScopeId_Points",
                table: "leaderboards",
                columns: new[] { "Scope", "ScopeId", "Points" });

            migrationBuilder.CreateIndex(
                name: "IX_leaderboards_Scope_ScopeId_UserId",
                table: "leaderboards",
                columns: new[] { "Scope", "ScopeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_analytics_OrgId_Date",
                table: "organization_analytics",
                columns: new[] { "OrgId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_points_ledger_CreatedAt",
                table: "points_ledger",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_points_ledger_UserId",
                table: "points_ledger",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_referral_rewards_RefereeUserId",
                table: "referral_rewards",
                column: "RefereeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_referral_rewards_ReferrerUserId",
                table: "referral_rewards",
                column: "ReferrerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_badges_BadgeId",
                table: "user_badges",
                column: "BadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_user_badges_UserId_BadgeId",
                table: "user_badges",
                columns: new[] { "UserId", "BadgeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_analytics_daily");

            migrationBuilder.DropTable(
                name: "leaderboards");

            migrationBuilder.DropTable(
                name: "organization_analytics");

            migrationBuilder.DropTable(
                name: "points_ledger");

            migrationBuilder.DropTable(
                name: "referral_rewards");

            migrationBuilder.DropTable(
                name: "user_badges");

            migrationBuilder.DropTable(
                name: "badges");

            migrationBuilder.DropColumn(
                name: "AppVersion",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "DeviceName",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "devices");
        }
    }
}
