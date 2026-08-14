using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameEventVisibilityPublicToListed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-266 M1. EventVisibility.Public was renamed to Listed. Enums are stored as their member
            // name (see the enum→text loop in KurxDbContext), so the rename is invisible to the schema
            // differ but every existing row still holds the old string — without this the first read of
            // any pre-existing event throws on the enum conversion.
            //
            // Scoped to "events" deliberately: ally_connections, event_participants, passes and posts all
            // have a Visibility column too, but each uses its own enum whose Public member is unchanged.
            migrationBuilder.Sql(@"UPDATE ""events"" SET ""Visibility"" = 'Listed' WHERE ""Visibility"" = 'Public';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Exactly reversible: Listed and Public are the same value under two names, and no other
            // EventVisibility member maps onto either, so nothing is merged or lost in either direction.
            migrationBuilder.Sql(@"UPDATE ""events"" SET ""Visibility"" = 'Public' WHERE ""Visibility"" = 'Listed';");
        }
    }
}
