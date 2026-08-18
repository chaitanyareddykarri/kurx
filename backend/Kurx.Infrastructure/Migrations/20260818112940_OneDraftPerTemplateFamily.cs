using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OneDraftPerTemplateFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_event_templates_RootTemplateId",
                table: "event_templates",
                column: "RootTemplateId",
                unique: true,
                filter: "\"State\" = 'Draft' AND \"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_event_templates_RootTemplateId",
                table: "event_templates");
        }
    }
}
