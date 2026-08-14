using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WalkInIdempotencyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_orders_walkin_idempotency",
                table: "orders",
                columns: new[] { "EventId", "IdempotencyKey" },
                unique: true,
                filter: "\"UserId\" IS NULL AND \"GuestPhone\" IS NULL AND \"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_walkin_idempotency",
                table: "orders");
        }
    }
}
