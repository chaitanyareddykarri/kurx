using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chat_rooms_EventId_Kind",
                table: "chat_rooms");

            migrationBuilder.AlterColumn<Guid>(
                name: "EventId",
                table: "chat_rooms",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "DirectHighUserId",
                table: "chat_rooms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DirectLowUserId",
                table: "chat_rooms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DmInitiatedBy",
                table: "chat_rooms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DmRequestState",
                table: "chat_rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "chat_members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_rooms_direct_pair",
                table: "chat_rooms",
                columns: new[] { "DirectLowUserId", "DirectHighUserId" },
                unique: true,
                filter: "\"DirectLowUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_chat_rooms_DirectHighUserId",
                table: "chat_rooms",
                column: "DirectHighUserId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_rooms_EventId_Kind",
                table: "chat_rooms",
                columns: new[] { "EventId", "Kind" },
                unique: true,
                filter: "\"EventId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_chat_rooms_users_DirectHighUserId",
                table: "chat_rooms",
                column: "DirectHighUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_chat_rooms_users_DirectLowUserId",
                table: "chat_rooms",
                column: "DirectLowUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chat_rooms_users_DirectHighUserId",
                table: "chat_rooms");

            migrationBuilder.DropForeignKey(
                name: "FK_chat_rooms_users_DirectLowUserId",
                table: "chat_rooms");

            migrationBuilder.DropIndex(
                name: "ix_chat_rooms_direct_pair",
                table: "chat_rooms");

            migrationBuilder.DropIndex(
                name: "IX_chat_rooms_DirectHighUserId",
                table: "chat_rooms");

            migrationBuilder.DropIndex(
                name: "IX_chat_rooms_EventId_Kind",
                table: "chat_rooms");

            migrationBuilder.DropColumn(
                name: "DirectHighUserId",
                table: "chat_rooms");

            migrationBuilder.DropColumn(
                name: "DirectLowUserId",
                table: "chat_rooms");

            migrationBuilder.DropColumn(
                name: "DmInitiatedBy",
                table: "chat_rooms");

            migrationBuilder.DropColumn(
                name: "DmRequestState",
                table: "chat_rooms");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "chat_members");

            migrationBuilder.AlterColumn<Guid>(
                name: "EventId",
                table: "chat_rooms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_rooms_EventId_Kind",
                table: "chat_rooms",
                columns: new[] { "EventId", "Kind" },
                unique: true);
        }
    }
}
