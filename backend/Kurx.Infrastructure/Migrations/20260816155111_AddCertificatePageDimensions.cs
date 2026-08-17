using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificatePageDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PageSize",
                table: "certificate_templates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<double>(
                name: "PageHeightMm",
                table: "certificate_templates",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PageWidthMm",
                table: "certificate_templates",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            // D-361 — backfill, so no existing template is left with a zero-sized page.
            //
            // Every row today is A4Landscape or A4Portrait (those were the only two members), but the
            // whole map is written out rather than the two that exist: a migration is read years later
            // by someone establishing what a stored name meant, and "the other case" is exactly what
            // they will need. The renderer falls back to the name when the dimensions are absent, so
            // this is belt and braces — but a fallback that is never exercised is a fallback nobody
            // knows is broken.
            //
            // 1 in = 25.4 mm exactly; Letter and Legal are therefore not whole millimetres.
            migrationBuilder.Sql(@"
                UPDATE certificate_templates SET
                  ""PageWidthMm"" = CASE ""PageSize""
                    WHEN 'A4Portrait'          THEN 210
                    WHEN 'A4Landscape'         THEN 297
                    WHEN 'A5Portrait'          THEN 148
                    WHEN 'A5Landscape'         THEN 210
                    WHEN 'LetterPortrait'      THEN 215.9
                    WHEN 'LetterLandscape'     THEN 279.4
                    WHEN 'LegalPortrait'       THEN 215.9
                    WHEN 'LegalLandscape'      THEN 355.6
                    WHEN 'Photo8x10Portrait'   THEN 203.2
                    WHEN 'Photo8x10Landscape'  THEN 254
                    WHEN 'Photo11x14Portrait'  THEN 279.4
                    WHEN 'Photo11x14Landscape' THEN 355.6
                    WHEN 'Photo12x16Portrait'  THEN 304.8
                    WHEN 'Photo12x16Landscape' THEN 406.4
                    ELSE 297 END,
                  ""PageHeightMm"" = CASE ""PageSize""
                    WHEN 'A4Portrait'          THEN 297
                    WHEN 'A4Landscape'         THEN 210
                    WHEN 'A5Portrait'          THEN 210
                    WHEN 'A5Landscape'         THEN 148
                    WHEN 'LetterPortrait'      THEN 279.4
                    WHEN 'LetterLandscape'     THEN 215.9
                    WHEN 'LegalPortrait'       THEN 355.6
                    WHEN 'LegalLandscape'      THEN 215.9
                    WHEN 'Photo8x10Portrait'   THEN 254
                    WHEN 'Photo8x10Landscape'  THEN 203.2
                    WHEN 'Photo11x14Portrait'  THEN 355.6
                    WHEN 'Photo11x14Landscape' THEN 279.4
                    WHEN 'Photo12x16Portrait'  THEN 406.4
                    WHEN 'Photo12x16Landscape' THEN 304.8
                    ELSE 210 END
                WHERE ""PageWidthMm"" = 0 OR ""PageHeightMm"" = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PageHeightMm",
                table: "certificate_templates");

            migrationBuilder.DropColumn(
                name: "PageWidthMm",
                table: "certificate_templates");

            migrationBuilder.AlterColumn<string>(
                name: "PageSize",
                table: "certificate_templates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);
        }
    }
}
