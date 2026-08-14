using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStructureAndSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EditionLabel",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EditionOrdinal",
                table: "events",
                type: "integer",
                nullable: true);

            // Backfill: every pre-existing event stays discoverable (preserve current behaviour — the §3.4 opt-in rule
            // applies to newly created sub-events, set false explicitly by EventService). New rows get their value from
            // the C# initializer (= true) / the service, so this store default is never used by EF going forward.
            migrationBuilder.AddColumn<bool>(
                name: "ListedStandalone",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryPoolId",
                table: "event_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    BannerKey = table.Column<string>(type: "text", nullable: true),
                    BrandAssetsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Rrule = table.Column<string>(type: "text", nullable: true),
                    ExceptionDatesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_series", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_series_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_series_followers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_series_followers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_series_followers_event_series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "event_series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_series_followers_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_SeriesId",
                table: "events",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_event_sessions_InventoryPoolId",
                table: "event_sessions",
                column: "InventoryPoolId");

            migrationBuilder.CreateIndex(
                name: "IX_event_series_OrgId_Slug",
                table: "event_series",
                columns: new[] { "OrgId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_series_followers_SeriesId_UserId",
                table: "event_series_followers",
                columns: new[] { "SeriesId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_series_followers_UserId",
                table: "event_series_followers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_event_sessions_inventory_pools_InventoryPoolId",
                table: "event_sessions",
                column: "InventoryPoolId",
                principalTable: "inventory_pools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_events_event_series_SeriesId",
                table: "events",
                column: "SeriesId",
                principalTable: "event_series",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_sessions_inventory_pools_InventoryPoolId",
                table: "event_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_events_event_series_SeriesId",
                table: "events");

            migrationBuilder.DropTable(
                name: "event_series_followers");

            migrationBuilder.DropTable(
                name: "event_series");

            migrationBuilder.DropIndex(
                name: "IX_events_SeriesId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_event_sessions_InventoryPoolId",
                table: "event_sessions");

            migrationBuilder.DropColumn(
                name: "EditionLabel",
                table: "events");

            migrationBuilder.DropColumn(
                name: "EditionOrdinal",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ListedStandalone",
                table: "events");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "InventoryPoolId",
                table: "event_sessions");
        }
    }
}
