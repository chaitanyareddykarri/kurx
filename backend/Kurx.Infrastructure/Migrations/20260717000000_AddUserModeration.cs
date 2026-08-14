using System;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    // Additive account-moderation columns (D-060): SuspendedAt / BannedAt / ModerationReason on users,
    // all nullable — existing rows are unaffected (active). Hand-authored because the EF design-time host
    // can't load on this dev machine (Application Control blocks StackExchange.Redis, 0x800711C7); the
    // [Migration] attribute makes it discoverable by MigrateAsync exactly like a scaffolded one.
    [DbContext(typeof(KurxDbContext))]
    [Migration("20260717000000_AddUserModeration")]
    public partial class AddUserModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BannedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationReason",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SuspendedAt", table: "users");
            migrationBuilder.DropColumn(name: "BannedAt", table: "users");
            migrationBuilder.DropColumn(name: "ModerationReason", table: "users");
        }
    }
}
