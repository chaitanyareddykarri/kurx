using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalOrgId",
                table: "organizations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "organizations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryDomain",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "organizations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "organization_aliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "text", nullable: false),
                    NormalizedAlias = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_aliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_aliases_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organizations_CanonicalOrgId",
                table: "organizations",
                column: "CanonicalOrgId");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_domain_active",
                table: "organizations",
                column: "PrimaryDomain",
                unique: true,
                filter: "\"PrimaryDomain\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_NormalizedName",
                table: "organizations",
                column: "NormalizedName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_aliases_NormalizedAlias",
                table: "organization_aliases",
                column: "NormalizedAlias")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_aliases_OrgId",
                table: "organization_aliases",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_organization_aliases_OrgId_NormalizedAlias",
                table: "organization_aliases",
                columns: new[] { "OrgId", "NormalizedAlias" },
                unique: true);

            // Backfill existing orgs (M4): every org is its own canonical, and NormalizedName is derived
            // from Name (lowercase, non-alphanumeric -> single space, trimmed) to match the app-side
            // OrganizationRegistryService.Normalize used by the fuzzy-dedup search.
            migrationBuilder.Sql(@"
UPDATE organizations SET ""CanonicalOrgId"" = ""Id"" WHERE ""CanonicalOrgId"" = '00000000-0000-0000-0000-000000000000';
UPDATE organizations SET ""NormalizedName"" = btrim(lower(regexp_replace(""Name"", '[^a-zA-Z0-9]+', ' ', 'g')));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_aliases");

            migrationBuilder.DropIndex(
                name: "IX_organizations_CanonicalOrgId",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "ix_organizations_domain_active",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_organizations_NormalizedName",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "CanonicalOrgId",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PrimaryDomain",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "organizations");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
