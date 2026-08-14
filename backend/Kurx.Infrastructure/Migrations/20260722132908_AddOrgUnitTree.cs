using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgUnitTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrgUnitId",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "org_units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_org_units_org_units_ParentId",
                        column: x => x.ParentId,
                        principalTable: "org_units",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_org_units_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_OrgUnitId",
                table: "events",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "ix_org_units_one_root_per_org",
                table: "org_units",
                column: "OrgId",
                unique: true,
                filter: "\"ParentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_org_units_ParentId",
                table: "org_units",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_events_org_units_OrgUnitId",
                table: "events",
                column: "OrgUnitId",
                principalTable: "org_units",
                principalColumn: "Id");

            // ── Backfill (V3 §4.1) ────────────────────────────────────────────────────────────────────────
            // One root unit per existing organisation. Orgs ≪ events, so this stays small and runs inside the
            // migration transaction. gen_random_uuid() is core in Postgres 13+ (this project runs 17). The
            // NOT EXISTS / IS NULL guards make the whole backfill idempotent, so an interrupted run resumes.
            migrationBuilder.Sql("""
                INSERT INTO org_units ("Id", "OrgId", "ParentId", "Kind", "Name", "Path", "State", "CreatedAt")
                SELECT gen_random_uuid(), o."Id", NULL, 'organization', o."Name", '', 'Active', now()
                FROM organizations o
                WHERE NOT EXISTS (
                    SELECT 1 FROM org_units u WHERE u."OrgId" = o."Id" AND u."ParentId" IS NULL);

                UPDATE org_units SET "Path" = '/' || "Id" || '/'
                WHERE "ParentId" IS NULL AND "Path" = '';
                """);

            // Repoint existing events to their org's root IN BATCHES with a COMMIT per batch, so a large
            // events table is never rewritten under one long-held lock (the ADD COLUMN above was metadata-only;
            // this is the only O(rows) step). The COMMIT requires running outside the migration transaction —
            // hence suppressTransaction — and a COMMIT-capable procedure rather than a DO block. Idempotent via
            // "OrgUnitId" IS NULL: a re-run resumes, and on an empty/fresh DB the loop makes one 0-row pass.
            migrationBuilder.Sql("""
                CREATE PROCEDURE pg_temp.kurx_p4_backfill_event_units() LANGUAGE plpgsql AS $$
                DECLARE moved integer;
                BEGIN
                  LOOP
                    UPDATE events e SET "OrgUnitId" = u."Id"
                    FROM org_units u
                    WHERE u."OrgId" = e."OrgId" AND u."ParentId" IS NULL
                      AND e."Id" IN (SELECT "Id" FROM events WHERE "OrgUnitId" IS NULL LIMIT 5000);
                    GET DIAGNOSTICS moved = ROW_COUNT;
                    COMMIT;
                    EXIT WHEN moved = 0;
                  END LOOP;
                END $$;

                CALL pg_temp.kurx_p4_backfill_event_units();
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_events_org_units_OrgUnitId",
                table: "events");

            migrationBuilder.DropTable(
                name: "org_units");

            migrationBuilder.DropIndex(
                name: "IX_events_OrgUnitId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "OrgUnitId",
                table: "events");
        }
    }
}
