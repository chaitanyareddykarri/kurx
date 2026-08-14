using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerPartyAllyVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HiddenByHigh",
                table: "ally_connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HiddenByLow",
                table: "ally_connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: an already-hidden pair predates the per-party flags, so we cannot know WHICH
            // party hid it. Setting both preserves the hidden state — with only the defaults, the next
            // visibility toggle by either party would recompute the derived column to Public and reveal
            // a connection someone had deliberately hidden, which is the exact defect this migration
            // exists to fix. The cost is that each party must un-hide once to republish; that is the
            // conservative direction for a privacy flag.
            // Status/Visibility are mapped as `text` on this table (see the creating migration), so the
            // predicate matches the enum NAME — an integer comparison would silently match nothing.
            migrationBuilder.Sql(
                """
                UPDATE ally_connections
                SET "HiddenByLow" = TRUE, "HiddenByHigh" = TRUE
                WHERE "Visibility" = 'Hidden';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HiddenByHigh",
                table: "ally_connections");

            migrationBuilder.DropColumn(
                name: "HiddenByLow",
                table: "ally_connections");
        }
    }
}
