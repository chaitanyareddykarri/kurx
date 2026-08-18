using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffGateEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_gate_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScannedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceInfo = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_gate_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_gate_entries_event_assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "event_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_gate_entries_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_gate_entries_users_ScannedBy",
                        column: x => x.ScannedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_gate_entries_AssignmentId_EventId",
                table: "staff_gate_entries",
                columns: new[] { "AssignmentId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_gate_entries_EventId_CreatedAt",
                table: "staff_gate_entries",
                columns: new[] { "EventId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_gate_entries_ScannedBy",
                table: "staff_gate_entries",
                column: "ScannedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_gate_entries");
        }
    }
}
