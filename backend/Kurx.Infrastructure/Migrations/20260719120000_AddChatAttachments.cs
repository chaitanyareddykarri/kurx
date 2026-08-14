using System;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    // Chat attachments (D-110) — purely additive: one new table, no changes to existing ones.
    // Metadata only; binary content lives in IStorage under StorageKey.
    //
    // MessageId is nullable because an upload is confirmed before the message that references it
    // exists. Unclaimed rows are orphans, swept by ChatAttachmentCleanupJob.
    //
    // Hand-authored — the EF design-time host can't load locally (Application Control blocks
    // StackExchange.Redis, 0x800711C7); the [Migration] attribute makes MigrateAsync discover and
    // apply it on CI, exactly like AddAuthSubstrate/ChatContractFreeze.
    [DbContext(typeof(KurxDbContext))]
    [Migration("20260719120000_AddChatAttachments")]
    public partial class AddChatAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_attachments", x => x.Id);
                    // SetNull, not Cascade: a hard-deleted message must leave the attachment row
                    // behind so the sweep can still find and remove its storage object.
                    table.ForeignKey(
                        name: "FK_chat_attachments_chat_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "chat_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_chat_attachments_chat_rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "chat_rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_attachments_users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // One storage object is one attachment: a repeated confirm converges rather than
            // creating a second row over the same bytes.
            migrationBuilder.CreateIndex(
                name: "IX_chat_attachments_StorageKey",
                table: "chat_attachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_attachments_MessageId",
                table: "chat_attachments",
                column: "MessageId");

            // Serves the orphan sweep, which scans unclaimed rows by age.
            migrationBuilder.CreateIndex(
                name: "IX_chat_attachments_RoomId_CreatedAt",
                table: "chat_attachments",
                columns: new[] { "RoomId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_attachments_UploadedBy",
                table: "chat_attachments",
                column: "UploadedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "chat_attachments");
        }
    }
}
