using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetireLegacyPrivateVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-266 M3 Step 5. EventVisibility.Private is retired; enums are stored by member name, so the
            // enum edit is invisible to the schema differ while every existing row still holds 'Private'
            // and would throw on its next read.
            //
            // IRREVERSIBLE, and deliberately so. After this runs, a row that was Private is indistinguishable
            // from one that was already InviteOnly; Down() cannot separate them. That is acceptable only
            // because Step 4 first moved every public-exposure decision onto Product+Visibility
            // (EventExposure), at which point Private and InviteOnly already behaved identically everywhere.
            // The merge therefore loses historical labelling, not behaviour. Running this BEFORE Step 4
            // would have destroyed a live distinction — the ordering is the safety property.
            //
            // ROLLBACK: reverting this migration leaves the merged rows as InviteOnly, which is their
            // correct behaviour under both models. To recover the original labels, restore from a backup
            // taken before this migration; there is no in-database path back.
            migrationBuilder.Sql(@"UPDATE ""events"" SET ""Visibility"" = 'InviteOnly' WHERE ""Visibility"" = 'Private';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. The merged rows are already correct as InviteOnly; inventing a rule to
            // split them back would fabricate data. See the note on Up().
        }
    }
}
