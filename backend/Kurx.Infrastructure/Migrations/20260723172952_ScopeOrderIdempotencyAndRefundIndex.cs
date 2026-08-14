using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScopeOrderIdempotencyAndRefundIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refunds_OrderId",
                table: "refunds");

            migrationBuilder.DropIndex(
                name: "ix_orders_event_idempotency",
                table: "orders");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_OrderId",
                table: "refunds",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "ix_orders_guest_idempotency",
                table: "orders",
                columns: new[] { "EventId", "GuestPhone", "IdempotencyKey" },
                unique: true,
                filter: "\"UserId\" IS NULL AND \"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_orders_user_idempotency",
                table: "orders",
                columns: new[] { "EventId", "UserId", "IdempotencyKey" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL AND \"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refunds_OrderId",
                table: "refunds");

            migrationBuilder.DropIndex(
                name: "ix_orders_guest_idempotency",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_user_idempotency",
                table: "orders");

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
        }
    }
}
