using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArchetypeCapabilityDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archetype_capability_defaults",
                columns: table => new
                {
                    ArchetypeSlug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CapabilitySlug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Rule = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archetype_capability_defaults", x => new { x.ArchetypeSlug, x.CapabilitySlug });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archetype_capability_defaults");
        }
    }
}
