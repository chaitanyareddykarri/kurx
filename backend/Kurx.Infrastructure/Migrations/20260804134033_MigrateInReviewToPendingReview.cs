using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateInReviewToPendingReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-266 M4 Stage 3. EventStatus is stored by member name, so splitting InReview into
            // PendingReview/UnderReview is invisible to the schema differ while every existing row still
            // holds the old string and would throw on its next read.
            //
            // InReview -> PendingReview is DETERMINISTIC, not a guess: the legacy model never recorded a
            // reviewer claim, so no row can be shown to have been UnderReview. Mapping any of them to
            // UnderReview would invent a claim that never happened and strand the item on a reviewer who
            // does not exist. Returning them to the queue is the only honest answer.
            //
            // Safe to run before Stage 4 because runtime never WRITES InReview — no transition in
            // EventStatusWorkflow targets it, so the backfill cannot be undone by a later submission.
            migrationBuilder.Sql(@"UPDATE ""events"" SET ""Status"" = 'PendingReview' WHERE ""Status"" = 'InReview';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible: PendingReview rows that came from InReview are indistinguishable from ones
            // submitted after M4, so this returns BOTH to InReview. That is lossless in behaviour terms
            // (InReview and PendingReview mean the same thing to the pre-M4 runtime) but it does discard
            // the post-M4 distinction, so a rollback should be followed by re-running Up.
            migrationBuilder.Sql(@"UPDATE ""events"" SET ""Status"" = 'InReview' WHERE ""Status"" = 'PendingReview';");
        }
    }
}
