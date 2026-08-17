using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EventSoftDeleteFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_events_ShortCode",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_Slug",
                table: "events");

            migrationBuilder.CreateIndex(
                name: "IX_events_ShortCode",
                table: "events",
                column: "ShortCode",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_events_Slug",
                table: "events",
                column: "Slug",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_events_ShortCode",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_Slug",
                table: "events");

            migrationBuilder.CreateIndex(
                name: "IX_events_ShortCode",
                table: "events",
                column: "ShortCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_Slug",
                table: "events",
                column: "Slug",
                unique: true);
        }
    }
}
