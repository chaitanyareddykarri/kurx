using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInviteLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InvitedUserId",
                table: "event_invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_invite_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MaxSeats = table.Column<int>(type: "integer", nullable: true),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    SingleUse = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasscodeHash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_invite_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_invite_links_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_invite_links_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_invite_link_redemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_invite_link_redemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_invite_link_redemptions_event_invite_links_InviteLink~",
                        column: x => x.InviteLinkId,
                        principalTable: "event_invite_links",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_invite_link_redemptions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_invitations_EventId_InvitedUserId",
                table: "event_invitations",
                columns: new[] { "EventId", "InvitedUserId" },
                unique: true,
                filter: "\"InvitedUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_event_invitations_InvitedUserId_RsvpStatus",
                table: "event_invitations",
                columns: new[] { "InvitedUserId", "RsvpStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_event_invite_link_redemptions_InviteLinkId_UserId",
                table: "event_invite_link_redemptions",
                columns: new[] { "InviteLinkId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_invite_link_redemptions_UserId",
                table: "event_invite_link_redemptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_event_invite_links_CreatedBy",
                table: "event_invite_links",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_event_invite_links_EventId_Status",
                table: "event_invite_links",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_event_invite_links_Token",
                table: "event_invite_links",
                column: "Token",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_event_invitations_users_InvitedUserId",
                table: "event_invitations",
                column: "InvitedUserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_invitations_users_InvitedUserId",
                table: "event_invitations");

            migrationBuilder.DropTable(
                name: "event_invite_link_redemptions");

            migrationBuilder.DropTable(
                name: "event_invite_links");

            migrationBuilder.DropIndex(
                name: "IX_event_invitations_EventId_InvitedUserId",
                table: "event_invitations");

            migrationBuilder.DropIndex(
                name: "IX_event_invitations_InvitedUserId_RsvpStatus",
                table: "event_invitations");

            migrationBuilder.DropColumn(
                name: "InvitedUserId",
                table: "event_invitations");
        }
    }
}
