using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKindRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KindSlug",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_kinds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    GroupSlug = table.Column<string>(type: "text", nullable: false),
                    GroupName = table.Column<string>(type: "text", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_kinds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "kind_aliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "text", nullable: false),
                    NormalizedAlias = table.Column<string>(type: "text", nullable: false),
                    KindSlug = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kind_aliases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_kinds_GroupSlug",
                table: "event_kinds",
                column: "GroupSlug");

            migrationBuilder.CreateIndex(
                name: "IX_event_kinds_Slug",
                table: "event_kinds",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kind_aliases_KindSlug",
                table: "kind_aliases",
                column: "KindSlug");

            migrationBuilder.CreateIndex(
                name: "IX_kind_aliases_NormalizedAlias",
                table: "kind_aliases",
                column: "NormalizedAlias",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_kinds");

            migrationBuilder.DropTable(
                name: "kind_aliases");

            migrationBuilder.DropColumn(
                name: "KindSlug",
                table: "events");
        }
    }
}
