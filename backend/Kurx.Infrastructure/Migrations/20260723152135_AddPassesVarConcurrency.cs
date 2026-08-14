using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPassesVarConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refunds_OrderId",
                table: "refunds");

            migrationBuilder.AddColumn<Guid>(
                name: "PoolId",
                table: "seat_holds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "passes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PricePaise = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "INR"),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SaleStarts = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SaleEnds = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PerSubjectLimit = table.Column<int>(type: "integer", nullable: false),
                    Visibility = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_passes", x => x.Id);
                    table.CheckConstraint("ck_passes_price_paise", "\"PricePaise\" >= 0");
                    table.ForeignKey(
                        name: "FK_passes_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_passes_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "var_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocatedPaise = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "INR"),
                    Basis = table.Column<string>(type: "text", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_var_lines", x => x.Id);
                    table.CheckConstraint("ck_var_lines_allocated_paise", "\"AllocatedPaise\" >= 0");
                    table.ForeignKey(
                        name: "FK_var_lines_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_var_lines_order_items_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_var_lines_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admission_rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Uses = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admission_rights", x => x.Id);
                    table.CheckConstraint("ck_admission_rights_uses", "\"Uses\" >= 1");
                    table.ForeignKey(
                        name: "FK_admission_rights_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_admission_rights_passes_PassId",
                        column: x => x.PassId,
                        principalTable: "passes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_PoolId",
                table: "seat_holds",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_OrderId",
                table: "refunds",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_event_idempotency",
                table: "orders",
                columns: new[] { "EventId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_pools_nonneg",
                table: "inventory_pools",
                sql: "\"Held\" >= 0 AND \"Allocated\" >= 0 AND \"Consumed\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_admission_rights_EventId",
                table: "admission_rights",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_admission_rights_PassId",
                table: "admission_rights",
                column: "PassId");

            migrationBuilder.CreateIndex(
                name: "IX_passes_EventId",
                table: "passes",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_passes_TicketTypeId",
                table: "passes",
                column: "TicketTypeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_var_lines_EventId",
                table: "var_lines",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_var_lines_OrderId",
                table: "var_lines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_var_lines_OrderItemId",
                table: "var_lines",
                column: "OrderItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_seat_holds_inventory_pools_PoolId",
                table: "seat_holds",
                column: "PoolId",
                principalTable: "inventory_pools",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_seat_holds_inventory_pools_PoolId",
                table: "seat_holds");

            migrationBuilder.DropTable(
                name: "admission_rights");

            migrationBuilder.DropTable(
                name: "var_lines");

            migrationBuilder.DropTable(
                name: "passes");

            migrationBuilder.DropIndex(
                name: "IX_seat_holds_PoolId",
                table: "seat_holds");

            migrationBuilder.DropIndex(
                name: "IX_refunds_OrderId",
                table: "refunds");

            migrationBuilder.DropIndex(
                name: "ix_orders_event_idempotency",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_pools_nonneg",
                table: "inventory_pools");

            migrationBuilder.DropColumn(
                name: "PoolId",
                table: "seat_holds");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "orders");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_OrderId",
                table: "refunds",
                column: "OrderId");
        }
    }
}
