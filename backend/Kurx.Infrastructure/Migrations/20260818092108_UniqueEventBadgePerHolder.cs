using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueEventBadgePerHolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-added ahead of the generated index (D-386). Migrations run at startup, so a database
            // already holding a duplicate would abort the host rather than serve traffic — the index has
            // to be able to build. None exist in dev (checked: 0 duplicate pairs), and none can exist once
            // the index is in place; this covers any environment that raced a `generate` before it.
            //
            // Keeps the OLDEST row per (event, holder), matching IdCardService.OldestPerHolder — so the
            // card number someone is already holding stays authoritative, and the database and the code
            // cannot disagree about which of two rows was the real one.
            migrationBuilder.Sql(
                """
                DELETE FROM id_cards a
                USING id_cards b
                WHERE a."EventId" IS NOT NULL
                  AND a."EventId" = b."EventId"
                  AND a."UserId"  = b."UserId"
                  AND (a."CreatedAt", a."Id") > (b."CreatedAt", b."Id");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_id_cards_EventId_UserId",
                table: "id_cards",
                columns: new[] { "EventId", "UserId" },
                unique: true,
                filter: "\"EventId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_id_cards_EventId_UserId",
                table: "id_cards");
        }
    }
}
