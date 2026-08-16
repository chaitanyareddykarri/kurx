using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateTextStyles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FontStyle",
                table: "certificate_template_fields",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LetterSpacing",
                table: "certificate_template_fields",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LineHeight",
                table: "certificate_template_fields",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Underline",
                table: "certificate_template_fields",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FontStyle",
                table: "certificate_template_fields");

            migrationBuilder.DropColumn(
                name: "LetterSpacing",
                table: "certificate_template_fields");

            migrationBuilder.DropColumn(
                name: "LineHeight",
                table: "certificate_template_fields");

            migrationBuilder.DropColumn(
                name: "Underline",
                table: "certificate_template_fields");
        }
    }
}
