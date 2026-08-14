using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestCompetitionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_users_UserId",
                table: "orders");

            migrationBuilder.AddColumn<bool>(
                name: "IsCompetition",
                table: "ticket_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "GuestAccessToken",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPhone",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortCode",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "event_invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRevoked",
                table: "certificates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "certificates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "certificates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevokedReason",
                table: "certificates",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_GuestAccessToken",
                table: "orders",
                column: "GuestAccessToken",
                unique: true,
                filter: "\"GuestAccessToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_orders_GuestPhone",
                table: "orders",
                column: "GuestPhone");

            migrationBuilder.CreateIndex(
                name: "IX_events_ShortCode",
                table: "events",
                column: "ShortCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_invitations_GroupId",
                table: "event_invitations",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_event_invitations_groups_GroupId",
                table: "event_invitations",
                column: "GroupId",
                principalTable: "groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_users_UserId",
                table: "orders",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_invitations_groups_GroupId",
                table: "event_invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_users_UserId",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_GuestAccessToken",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_GuestPhone",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_events_ShortCode",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_event_invitations_GroupId",
                table: "event_invitations");

            migrationBuilder.DropColumn(
                name: "IsCompetition",
                table: "ticket_types");

            migrationBuilder.DropColumn(
                name: "GuestAccessToken",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "GuestName",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "GuestPhone",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShortCode",
                table: "events");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "event_invitations");

            migrationBuilder.DropColumn(
                name: "IsRevoked",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "RevokedReason",
                table: "certificates");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_users_UserId",
                table: "orders",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
