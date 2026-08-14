using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleSlug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CustomLabel = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    ScopeJson = table.Column<string>(type: "jsonb", nullable: true),
                    Visibility = table.Column<string>(type: "text", nullable: false),
                    InvitedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_participants_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participant_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Class = table.Column<string>(type: "text", nullable: false),
                    DefaultPermissionsJson = table.Column<string>(type: "jsonb", nullable: true),
                    DefaultAccessZonesJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CountsTowardCapacity = table.Column<bool>(type: "boolean", nullable: false),
                    InventorySegment = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_roles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_EventId",
                table: "event_participants",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_EventId_SubjectType_SubjectId",
                table: "event_participants",
                columns: new[] { "EventId", "SubjectType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "ix_event_participants_unique",
                table: "event_participants",
                columns: new[] { "EventId", "SubjectType", "SubjectId", "RoleSlug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participant_roles_OrgId",
                table: "participant_roles",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "ix_participant_roles_platform_slug",
                table: "participant_roles",
                column: "Slug",
                unique: true,
                filter: "\"OrgId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_participants");

            migrationBuilder.DropTable(
                name: "participant_roles");
        }
    }
}
