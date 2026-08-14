using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_event_templates_Slug",
                table: "event_templates");

            migrationBuilder.AddColumn<int>(
                name: "CreatedFromTemplateVersion",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfigJson",
                table: "event_templates",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");   // '' is not valid jsonb — Postgres rejects the column default

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "event_templates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KindSlug",
                table: "event_templates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrgUnitId",
                table: "event_templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "event_templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RootTemplateId",
                table: "event_templates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "event_templates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "event_templates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "event_templates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "event_templates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill the activation columns for any pre-existing (inert) template rows BEFORE the unique
            // (RootTemplateId, Version) index — every legacy row becomes its own family root, v1, Published
            // (they were already usable), Platform for system templates and Org for custom ones.
            migrationBuilder.Sql(@"
                UPDATE event_templates SET
                    ""RootTemplateId"" = ""Id"",
                    ""Version"" = 1,
                    ""State"" = 'Published',
                    ""Scope"" = CASE WHEN ""IsSystem"" THEN 'Platform' ELSE 'Org' END,
                    ""UpdatedAt"" = ""CreatedAt""
                WHERE ""RootTemplateId"" = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.CreateIndex(
                name: "IX_event_templates_RootTemplateId_Version",
                table: "event_templates",
                columns: new[] { "RootTemplateId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_templates_Scope_OrgId_OrgUnitId_OwnerUserId",
                table: "event_templates",
                columns: new[] { "Scope", "OrgId", "OrgUnitId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_event_templates_Slug",
                table: "event_templates",
                column: "Slug");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_event_templates_RootTemplateId_Version",
                table: "event_templates");

            migrationBuilder.DropIndex(
                name: "IX_event_templates_Scope_OrgId_OrgUnitId_OwnerUserId",
                table: "event_templates");

            migrationBuilder.DropIndex(
                name: "IX_event_templates_Slug",
                table: "event_templates");

            migrationBuilder.DropColumn(
                name: "CreatedFromTemplateVersion",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ConfigJson",
                table: "event_templates");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "event_templates");

            migrationBuilder.DropColumn(
                name: "KindSlug",
                table: "event_templates");

            migrationBuilder.DropColumn(
                name: "OrgUnitId",
                table: "event_templates");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "event_templates");

            migrationBuilder.DropColumn(
                name: "RootTemplateId",
                table: "event_templates");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "event_templates");

            migrationBuilder.DropColumn(
                name: "State",
                table: "event_templates");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "event_templates");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "event_templates");

            migrationBuilder.CreateIndex(
                name: "IX_event_templates_Slug",
                table: "event_templates",
                column: "Slug",
                unique: true);
        }
    }
}
