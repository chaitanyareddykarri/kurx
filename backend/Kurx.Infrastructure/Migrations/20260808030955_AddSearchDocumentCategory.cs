using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchDocumentCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "event_search_documents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TypeId",
                table: "event_search_documents",
                type: "uuid",
                nullable: true);

            // Backfill from the authoritative rows. Without this every existing document keeps the all-zero
            // default and `?categoryId=` returns NOTHING — the opposite of the bug being fixed, and just as
            // silent. Done in SQL here rather than left to the reindex job so the filter is correct the
            // moment the migration lands (D-299).
            migrationBuilder.Sql(@"
                UPDATE event_search_documents d
                SET ""CategoryId"" = e.""CategoryId"", ""TypeId"" = e.""TypeId""
                FROM events e WHERE e.""Id"" = d.""EventId"";");

            // Two single-column indexes rather than one composite: the predicate is
            // (CategoryId = @cat OR TypeId = @cat), and Postgres BitmapOrs two indexes for that. A composite
            // on (CategoryId, TypeId) could not serve the TypeId arm at all.
            migrationBuilder.CreateIndex(
                name: "IX_event_search_documents_CategoryId",
                table: "event_search_documents",
                column: "CategoryId");
            migrationBuilder.CreateIndex(
                name: "IX_event_search_documents_TypeId",
                table: "event_search_documents",
                column: "TypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "event_search_documents");

            migrationBuilder.DropColumn(
                name: "TypeId",
                table: "event_search_documents");
        }
    }
}
