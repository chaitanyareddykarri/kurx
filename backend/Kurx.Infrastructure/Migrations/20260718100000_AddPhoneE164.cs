using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    // Canonical phone identity (D-089, ADR-A6) — Phase 1 of the staged E.164 migration.
    //
    // STRICTLY ADDITIVE AND REVERSIBLE. Three nullable columns; the legacy `Phone` column and its unique
    // index are untouched, so every existing query, index and client keeps working exactly as before and
    // this migration can be rolled back by dropping the new columns (Down does precisely that, and loses
    // nothing that is not derivable from `Phone`).
    //
    // The unique index on PhoneE164 is FILTERED to non-null rows: during the migration most rows are null
    // (not yet backfilled), and a plain unique index would treat those as... fine in Postgres actually,
    // but the filter also keeps the index small while the backfill is in flight and documents the intent.
    //
    // Hand-authored: the EF design-time host cannot load locally (AppControl blocks StackExchange.Redis,
    // 0x800711C7), so the [Migration] attribute + a hand-synced ModelSnapshot are how migrations land here
    // (same approach as AddAuthSubstrate / AddUserModeration).
    [DbContext(typeof(KurxDbContext))]
    [Migration("20260718100000_AddPhoneE164")]
    public partial class AddPhoneE164 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneE164",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNational",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_phone_e164",
                table: "users",
                column: "PhoneE164",
                unique: true,
                filter: "\"PhoneE164\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback is lossless: everything in these columns is re-derivable from `Phone` by re-running
            // the backfill job. No data that exists only here is destroyed.
            migrationBuilder.DropIndex(name: "ix_users_phone_e164", table: "users");
            migrationBuilder.DropColumn(name: "PhoneNational", table: "users");
            migrationBuilder.DropColumn(name: "CountryCode", table: "users");
            migrationBuilder.DropColumn(name: "PhoneE164", table: "users");
        }
    }
}
