using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "capabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    GroupSlug = table.Column<string>(type: "text", nullable: false),
                    IsUniversal = table.Column<bool>(type: "boolean", nullable: false),
                    WorkspaceTab = table.Column<string>(type: "text", nullable: true),
                    DependsOnJson = table.Column<string>(type: "jsonb", nullable: false),
                    AvailableModesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capabilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "event_capabilities",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilitySlug = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    LockedReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_capabilities", x => new { x.EventId, x.CapabilitySlug });
                    table.ForeignKey(
                        name: "FK_event_capabilities_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kind_capability_defaults",
                columns: table => new
                {
                    KindSlug = table.Column<string>(type: "text", nullable: false),
                    CapabilitySlug = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kind_capability_defaults", x => new { x.KindSlug, x.CapabilitySlug });
                });

            migrationBuilder.CreateIndex(
                name: "IX_capabilities_GroupSlug",
                table: "capabilities",
                column: "GroupSlug");

            migrationBuilder.CreateIndex(
                name: "IX_capabilities_Slug",
                table: "capabilities",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kind_capability_defaults_KindSlug",
                table: "kind_capability_defaults",
                column: "KindSlug");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capabilities");

            migrationBuilder.DropTable(
                name: "event_capabilities");

            migrationBuilder.DropTable(
                name: "kind_capability_defaults");
        }
    }
}
