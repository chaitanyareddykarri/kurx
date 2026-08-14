using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAllyConnectionsAndProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowAllies",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ally_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserLowId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserHighId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddresseeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Visibility = table.Column<string>(type: "text", nullable: false),
                    ConnectedVia = table.Column<string>(type: "text", nullable: true),
                    FirstSharedEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastInteractionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ally_connections", x => x.Id);
                    table.CheckConstraint("ck_ally_connections_pair_order", "\"UserLowId\" < \"UserHighId\"");
                    table.ForeignKey(
                        name: "FK_ally_connections_events_FirstSharedEventId",
                        column: x => x.FirstSharedEventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ally_connections_users_UserHighId",
                        column: x => x.UserHighId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ally_connections_users_UserLowId",
                        column: x => x.UserLowId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ally_connections_AddresseeId",
                table: "ally_connections",
                column: "AddresseeId");

            migrationBuilder.CreateIndex(
                name: "IX_ally_connections_FirstSharedEventId",
                table: "ally_connections",
                column: "FirstSharedEventId");

            migrationBuilder.CreateIndex(
                name: "ix_ally_connections_pair",
                table: "ally_connections",
                columns: new[] { "UserLowId", "UserHighId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ally_connections_RequesterId",
                table: "ally_connections",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_ally_connections_UserHighId",
                table: "ally_connections",
                column: "UserHighId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ally_connections");

            migrationBuilder.DropColumn(
                name: "ShowAllies",
                table: "users");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "memberships");
        }
    }
}
