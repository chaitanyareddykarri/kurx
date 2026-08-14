using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAnalyticsRollupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_analytics_daily");

            migrationBuilder.DropTable(
                name: "organization_analytics");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_analytics_daily",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckIns = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "INR"),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreeRegistrations = table.Column<int>(type: "integer", nullable: false),
                    PaidRegistrations = table.Column<int>(type: "integer", nullable: false),
                    RefundCount = table.Column<int>(type: "integer", nullable: false),
                    Registrations = table.Column<int>(type: "integer", nullable: false),
                    RevenuePaise = table.Column<long>(type: "bigint", nullable: false),
                    UniqueVisitors = table.Column<int>(type: "integer", nullable: false),
                    Views = table.Column<int>(type: "integer", nullable: false),
                    WaitlistCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_analytics_daily", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "INR"),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Registrations = table.Column<int>(type: "integer", nullable: false),
                    RevenuePaise = table.Column<long>(type: "bigint", nullable: false),
                    UniqueVisitors = table.Column<int>(type: "integer", nullable: false),
                    Views = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_analytics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_analytics_daily_EventId_Date",
                table: "event_analytics_daily",
                columns: new[] { "EventId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_analytics_OrgId_Date",
                table: "organization_analytics",
                columns: new[] { "OrgId", "Date" },
                unique: true);
        }
    }
}
