using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatReactionsMediaAndFiling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ForwardedFromMessageId",
                table: "chat_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkDescription",
                table: "chat_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkImageUrl",
                table: "chat_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkTitle",
                table: "chat_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkUrl",
                table: "chat_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastDeliveredMessageId",
                table: "chat_members",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NotificationsMutedUntil",
                table: "chat_members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "chat_members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chat_message_reactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Emoji = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_message_reactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chat_message_reactions_chat_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "chat_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_message_reactions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_message_reactions_MessageId",
                table: "chat_message_reactions",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_message_reactions_MessageId_UserId_Emoji",
                table: "chat_message_reactions",
                columns: new[] { "MessageId", "UserId", "Emoji" },
                unique: true);

            // D-295: SearchMessagesAsync runs websearch_to_tsquery against to_tsvector('english', "Body").
            // Without a GIN index on the SAME expression, Postgres recomputes the vector for every row
            // in chat_messages on every search — a sequential scan over all chat history per query.
            // Expression indexes cannot be declared through the EF model, hence raw SQL.
            migrationBuilder.Sql(
                "CREATE INDEX ix_chat_messages_body_fts ON chat_messages USING GIN (to_tsvector('english', \"Body\"));");

            migrationBuilder.CreateIndex(
                name: "IX_chat_message_reactions_UserId",
                table: "chat_message_reactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_chat_messages_body_fts;");

            migrationBuilder.DropTable(
                name: "chat_message_reactions");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ForwardedFromMessageId",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "LinkDescription",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "LinkImageUrl",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "LinkTitle",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "LinkUrl",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "LastDeliveredMessageId",
                table: "chat_members");

            migrationBuilder.DropColumn(
                name: "NotificationsMutedUntil",
                table: "chat_members");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "chat_members");
        }
    }
}
