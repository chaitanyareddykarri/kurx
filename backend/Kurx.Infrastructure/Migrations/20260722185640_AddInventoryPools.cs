using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryPools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PoolId",
                table: "ticket_waitlist",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "inventory_pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    Segment = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false),
                    Held = table.Column<int>(type: "integer", nullable: false),
                    Allocated = table.Column<int>(type: "integer", nullable: false),
                    Consumed = table.Column<int>(type: "integer", nullable: false),
                    OversellAllowance = table.Column<int>(type: "integer", nullable: false),
                    ReleasePolicyJson = table.Column<string>(type: "jsonb", nullable: true),
                    NoShowPolicy = table.Column<string>(type: "text", nullable: false),
                    NoShowReleaseMinutes = table.Column<int>(type: "integer", nullable: false),
                    WaitlistConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_pools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_pools_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_pools_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_waitlist_PoolId",
                table: "ticket_waitlist",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_pools_EventId",
                table: "inventory_pools",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_pools_ticket_type_segment",
                table: "inventory_pools",
                columns: new[] { "TicketTypeId", "Segment" },
                unique: true,
                filter: "\"TicketTypeId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_waitlist_inventory_pools_PoolId",
                table: "ticket_waitlist",
                column: "PoolId",
                principalTable: "inventory_pools",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ticket_waitlist_inventory_pools_PoolId",
                table: "ticket_waitlist");

            migrationBuilder.DropTable(
                name: "inventory_pools");

            migrationBuilder.DropIndex(
                name: "IX_ticket_waitlist_PoolId",
                table: "ticket_waitlist");

            migrationBuilder.DropColumn(
                name: "PoolId",
                table: "ticket_waitlist");
        }
    }
}
