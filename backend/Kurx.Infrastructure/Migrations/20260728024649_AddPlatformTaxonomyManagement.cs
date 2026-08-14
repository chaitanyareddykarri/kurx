using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformTaxonomyManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Badge",
                table: "event_categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "event_categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "event_categories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "event_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "event_categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconKey",
                table: "event_categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "event_categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchKeywords",
                table: "event_categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "event_categories",
                type: "text",
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "event_categories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "event_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "event_categories",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "category_capability_defaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilitySlug = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_capability_defaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_category_capability_defaults_event_categories_CategoryNodeId",
                        column: x => x.CategoryNodeId,
                        principalTable: "event_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_category_capability_defaults_CategoryNodeId_CapabilitySlug",
                table: "category_capability_defaults",
                columns: new[] { "CategoryNodeId", "CapabilitySlug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_capability_defaults");

            migrationBuilder.DropColumn(
                name: "Badge",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "IconKey",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "SearchKeywords",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "event_categories");
        }
    }
}
