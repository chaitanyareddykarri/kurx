using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalTablesAndProfileFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowOnProfile",
                table: "memberships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TransfersEnabled",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "gate_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScannedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceInfo = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gate_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gate_entries_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gate_entries_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seat_holds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seat_holds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seat_holds_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_seat_holds_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToPhone = table.Column<string>(type: "text", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferCode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_transfers_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToPhone = table.Column<string>(type: "text", nullable: false),
                    Template = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Wamid = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    RelatedType = table.Column<string>(type: "text", nullable: false),
                    RelatedId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tickets_UserId_State",
                table: "tickets",
                columns: new[] { "UserId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_memberships_UserId",
                table: "memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_events_CreatedBy_Status",
                table: "events",
                columns: new[] { "CreatedBy", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_events_OrgId_Status_EndsAt",
                table: "events",
                columns: new[] { "OrgId", "Status", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_certificates_UserId_IsPublic",
                table: "certificates",
                columns: new[] { "UserId", "IsPublic" });

            migrationBuilder.CreateIndex(
                name: "IX_gate_entries_EventId_CreatedAt",
                table: "gate_entries",
                columns: new[] { "EventId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_gate_entries_TicketId_EventId",
                table: "gate_entries",
                columns: new[] { "TicketId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_OrderId",
                table: "seat_holds",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_Status_ExpiresAt",
                table: "seat_holds",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_TicketTypeId_Status",
                table: "seat_holds",
                columns: new[] { "TicketTypeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_transfers_TicketId",
                table: "ticket_transfers",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_transfers_ToPhone_Status",
                table: "ticket_transfers",
                columns: new[] { "ToPhone", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_transfers_TransferCode",
                table: "ticket_transfers",
                column: "TransferCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_messages_RelatedType_RelatedId",
                table: "whatsapp_messages",
                columns: new[] { "RelatedType", "RelatedId" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_messages_Status",
                table: "whatsapp_messages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_messages_Wamid",
                table: "whatsapp_messages",
                column: "Wamid",
                unique: true,
                filter: "\"Wamid\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gate_entries");

            migrationBuilder.DropTable(
                name: "seat_holds");

            migrationBuilder.DropTable(
                name: "ticket_transfers");

            migrationBuilder.DropTable(
                name: "whatsapp_messages");

            migrationBuilder.DropIndex(
                name: "IX_tickets_UserId_State",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_memberships_UserId",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "IX_events_CreatedBy_Status",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_OrgId_Status_EndsAt",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_certificates_UserId_IsPublic",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "ShowOnProfile",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "TransfersEnabled",
                table: "events");
        }
    }
}
