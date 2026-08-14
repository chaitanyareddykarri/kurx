using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudienceRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttributesJson",
                table: "memberships",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "memberships",
                type: "text",
                nullable: false,
                // Enum-as-text: existing memberships backfill to the valid default member, never "" (which would
                // fail to materialise as MembershipSource). New rows carry the app-side default (SelfDeclared).
                defaultValue: "SelfDeclared");

            migrationBuilder.CreateTable(
                name: "audience_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitSubtreeInJson = table.Column<string>(type: "jsonb", nullable: true),
                    RoleInJson = table.Column<string>(type: "jsonb", nullable: true),
                    CohortYearInJson = table.Column<string>(type: "jsonb", nullable: true),
                    AttributeMatchesJson = table.Column<string>(type: "jsonb", nullable: true),
                    RequireVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalOrgsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    AppliesTo = table.Column<string>(type: "text", nullable: false),
                    GuestsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    GuestPerRegistrantCap = table.Column<int>(type: "integer", nullable: false),
                    GuestsRequireApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audience_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audience_rules_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audience_rules_EventId",
                table: "audience_rules",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audience_rules");

            migrationBuilder.DropColumn(
                name: "AttributesJson",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "memberships");
        }
    }
}
