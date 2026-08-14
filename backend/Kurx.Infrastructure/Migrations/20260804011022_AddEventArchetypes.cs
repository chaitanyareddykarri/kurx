using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventArchetypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchetypeSlug",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Product",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "Public");

            migrationBuilder.AddColumn<string>(
                name: "AllowedRegistrationPoliciesJson",
                table: "event_categories",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchetypeSlug",
                table: "event_categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductClass",
                table: "event_categories",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_archetypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Product = table.Column<string>(type: "text", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_archetypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_ArchetypeSlug",
                table: "events",
                column: "ArchetypeSlug");

            migrationBuilder.CreateIndex(
                name: "IX_events_Product",
                table: "events",
                column: "Product");

            migrationBuilder.CreateIndex(
                name: "IX_event_archetypes_Slug",
                table: "event_archetypes",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_archetypes");

            migrationBuilder.DropIndex(
                name: "IX_events_ArchetypeSlug",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_Product",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ArchetypeSlug",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Product",
                table: "events");

            migrationBuilder.DropColumn(
                name: "AllowedRegistrationPoliciesJson",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "ArchetypeSlug",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "ProductClass",
                table: "event_categories");
        }
    }
}
