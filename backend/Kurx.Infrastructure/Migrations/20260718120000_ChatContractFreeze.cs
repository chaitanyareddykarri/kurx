using System;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    // Event Chat contract freeze (D-104) — purely additive: 3 nullable columns + index reshaping.
    // Zero backfill (chat_rooms.Kind lands with a DEFAULT), zero data movement, zero downtime.
    //
    // Existing chat_messages rows keep their Guid v4 ids; only new rows are UUID v7. That is exactly
    // why the sort key is the (CreatedAt, Id) pair rather than Id alone — a mixed table still has a
    // stable total order. The new (RoomId, CreatedAt, Id) index is a prefix-superset of the
    // (RoomId, CreatedAt) index it replaces, so nothing that used the old one regresses.
    //
    // Hand-authored — the EF design-time host can't load locally (Application Control blocks
    // StackExchange.Redis, 0x800711C7); the [Migration] attribute makes MigrateAsync discover and
    // apply it on CI, exactly like AddAuthSubstrate/AddUserModeration/AddEventMode.
    [DbContext(typeof(KurxDbContext))]
    [Migration("20260718120000_ChatContractFreeze")]
    public partial class ChatContractFreeze : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── chat_rooms.Kind ────────────────────────────────────────────────────────────────
            // Existing rows are all the event's General room, so the default backfills them.
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "chat_rooms",
                type: "text",
                nullable: false,
                defaultValue: "General");

            migrationBuilder.DropIndex(
                name: "IX_chat_rooms_EventId",
                table: "chat_rooms");

            migrationBuilder.CreateIndex(
                name: "IX_chat_rooms_EventId_Kind",
                table: "chat_rooms",
                columns: new[] { "EventId", "Kind" },
                unique: true);

            // ── chat_members.LastReadMessageId ─────────────────────────────────────────────────
            // No FK by design: a dangling pointer is harmless (unread falls back to counting all),
            // and an FK here would introduce a second cascade path from chat_rooms.
            // Null on every existing row = "never read", which is the correct starting state.
            migrationBuilder.AddColumn<Guid>(
                name: "LastReadMessageId",
                table: "chat_members",
                type: "uuid",
                nullable: true);

            // ── chat_messages.ClientMessageId ──────────────────────────────────────────────────
            // Nullable: system messages have no client origin. PostgreSQL treats NULLs as distinct
            // in a unique index, so any number of system messages coexist without conflict.
            migrationBuilder.AddColumn<Guid>(
                name: "ClientMessageId",
                table: "chat_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_RoomId_ClientMessageId",
                table: "chat_messages",
                columns: new[] { "RoomId", "ClientMessageId" },
                unique: true);

            // ── keyset pagination index ────────────────────────────────────────────────────────
            migrationBuilder.DropIndex(
                name: "IX_chat_messages_RoomId_CreatedAt",
                table: "chat_messages");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_RoomId_CreatedAt_Id",
                table: "chat_messages",
                columns: new[] { "RoomId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chat_messages_RoomId_CreatedAt_Id",
                table: "chat_messages");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_RoomId_CreatedAt",
                table: "chat_messages",
                columns: new[] { "RoomId", "CreatedAt" });

            migrationBuilder.DropIndex(
                name: "IX_chat_messages_RoomId_ClientMessageId",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "ClientMessageId",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "LastReadMessageId",
                table: "chat_members");

            migrationBuilder.DropIndex(
                name: "IX_chat_rooms_EventId_Kind",
                table: "chat_rooms");

            migrationBuilder.CreateIndex(
                name: "IX_chat_rooms_EventId",
                table: "chat_rooms",
                column: "EventId",
                unique: true);

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "chat_rooms");
        }
    }
}
