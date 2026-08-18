using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EventChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-388. Backfilled to 1, not 0, so an event that existed before this column and one created
            // after it start from the same number — a change request's BaseVersion is compared for
            // EQUALITY, and two origins for "never edited" would be an invisible difference in the one
            // value the staleness check turns on.
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "events",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "event_change_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseVersion = table.Column<int>(type: "integer", nullable: false),
                    ProposedJson = table.Column<string>(type: "jsonb", nullable: false),
                    PreviousJson = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewReasonCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_change_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_change_requests_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_change_requests_users_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_change_requests_EventId",
                table: "event_change_requests",
                column: "EventId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_event_change_requests_RequestedBy",
                table: "event_change_requests",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_event_change_requests_Status_CreatedAt",
                table: "event_change_requests",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_change_requests");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "events");
        }
    }
}
