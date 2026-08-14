using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDedupKeyAndCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DedupKey",
                table: "notifications",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Backfill BEFORE the unique index is created — creating it first would fail on any existing
            // duplicate, and backfilling after would leave already-delivered notifications unrecognised, so
            // the next announcement send or reminder tick would deliver every one of them a second time.
            //
            // Deterministic: the key is derived from DataJson, the same fact the old `DataJson.Contains`
            // dedup matched on, so a backfilled row means exactly what a newly-written one means.
            //
            // Collision-safe WITHOUT deleting anything: the old code deduplicated with a pre-check, not a
            // constraint, so genuine duplicates can exist (a concurrent double-send — SendAsync claims the
            // announcement with a read-then-write, not an atomic claim). row_number() gives the key to the
            // OLDEST row of each (UserId, key) group and leaves the rest NULL. Those extras keep working
            // exactly as they do today: visible in the bell, readable, deletable. They are simply not the
            // row that future dedup checks match on. Deleting them would destroy user-visible history to
            // satisfy an index, which is not a trade this migration is entitled to make.
            //
            // `->>` rather than the jsonb `?` operator: `?` collides with parameter placeholders in some
            // ADO paths, and `->> … IS NOT NULL` expresses the same key-existence test with no ambiguity.
            migrationBuilder.Sql("""
                WITH keyed AS (
                    SELECT "Id", "UserId",
                           CASE
                               WHEN "Kind" = 'event_announcement' AND "DataJson" ->> 'announcement_id' IS NOT NULL
                                   THEN 'event_announcement:' || ("DataJson" ->> 'announcement_id')
                               WHEN "Kind" = 'EventReminder' AND "DataJson" ->> 'eventId' IS NOT NULL
                                   THEN 'EventReminder:' || ("DataJson" ->> 'eventId')
                           END AS dedup_key
                    FROM notifications
                    WHERE "Kind" IN ('event_announcement', 'EventReminder')
                      AND "DataJson" IS NOT NULL
                ),
                ranked AS (
                    SELECT "Id", dedup_key,
                           row_number() OVER (PARTITION BY "UserId", dedup_key ORDER BY "Id") AS rn
                    FROM keyed
                    WHERE dedup_key IS NOT NULL
                )
                UPDATE notifications n
                SET "DedupKey" = r.dedup_key
                FROM ranked r
                WHERE n."Id" = r."Id" AND r.rn = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_created_at",
                table: "notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_dedup",
                table: "notifications",
                columns: new[] { "DedupKey", "UserId" },
                unique: true,
                filter: "\"DedupKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_created_at",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_dedup",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "DedupKey",
                table: "notifications");
        }
    }
}
