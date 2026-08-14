using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStateVocabularyCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_tickets_state",
                table: "tickets",
                sql: "\"State\" IN ('Issued', 'CheckedIn', 'Void')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_status",
                table: "orders",
                sql: "\"Status\" IN ('Pending', 'Paid', 'Failed', 'Refunded', 'PartiallyRefunded')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ledger_entries_state",
                table: "ledger_entries",
                sql: "\"State\" IN ('Collected', 'Available', 'Advanced', 'Reserved', 'Settled', 'Refunded', 'Disputed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_events_status",
                table: "events",
                sql: "\"Status\" IN ('Draft', 'Published', 'Closed', 'Archived', 'Cancelled', 'Scheduled', 'Live', 'Completed', 'PendingReview', 'UnderReview', 'ChangesRequested', 'Approved', 'Rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tickets_state",
                table: "tickets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_status",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ledger_entries_state",
                table: "ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_events_status",
                table: "events");
        }
    }
}
