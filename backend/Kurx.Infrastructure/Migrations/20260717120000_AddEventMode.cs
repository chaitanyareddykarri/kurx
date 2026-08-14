using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    // Additive online/hybrid event mode (D-064 A7): events.EventMode (text, NOT NULL, default 'Offline'
    // so existing rows stay Offline) + events.OnlineUrl (text, nullable). Hand-authored — the EF design-time
    // host can't load locally (Application Control blocks StackExchange.Redis, 0x800711C7); the [Migration]
    // attribute makes MigrateAsync discover + apply it. Enums are stored as text (KurxDbContext convention).
    [DbContext(typeof(KurxDbContext))]
    [Migration("20260717120000_AddEventMode")]
    public partial class AddEventMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventMode",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "Offline");

            migrationBuilder.AddColumn<string>(
                name: "OnlineUrl",
                table: "events",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EventMode", table: "events");
            migrationBuilder.DropColumn(name: "OnlineUrl", table: "events");
        }
    }
}
