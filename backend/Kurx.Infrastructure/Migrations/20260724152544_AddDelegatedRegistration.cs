using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDelegatedRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "seat_blocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrantOrgUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    DelegateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMode = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    AssignmentDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReassignLimit = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seat_blocks", x => x.Id);
                    table.CheckConstraint("ck_seat_blocks_qty", "\"Quantity\" >= 1 AND \"ReassignLimit\" >= 0");
                    table.ForeignKey(
                        name: "FK_seat_blocks_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_seat_blocks_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seat_blocks_org_units_RegistrantOrgUnitId",
                        column: x => x.RegistrantOrgUnitId,
                        principalTable: "org_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seat_blocks_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_seat_blocks_users_DelegateUserId",
                        column: x => x.DelegateUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seat_block_seats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatBlockId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReassignCount = table.Column<int>(type: "integer", nullable: false),
                    ReassignableUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seat_block_seats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seat_block_seats_admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_seat_block_seats_seat_blocks_SeatBlockId",
                        column: x => x.SeatBlockId,
                        principalTable: "seat_blocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seat_block_seats_AdmissionId",
                table: "seat_block_seats",
                column: "AdmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seat_block_seats_SeatBlockId",
                table: "seat_block_seats",
                column: "SeatBlockId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_blocks_DelegateUserId",
                table: "seat_blocks",
                column: "DelegateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_blocks_EventId",
                table: "seat_blocks",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_blocks_OrderId",
                table: "seat_blocks",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seat_blocks_RegistrantOrgUnitId",
                table: "seat_blocks",
                column: "RegistrantOrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_blocks_TicketTypeId",
                table: "seat_blocks",
                column: "TicketTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seat_block_seats");

            migrationBuilder.DropTable(
                name: "seat_blocks");
        }
    }
}
