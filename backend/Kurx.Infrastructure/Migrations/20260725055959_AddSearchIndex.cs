using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_search_documents",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TitleText = table.Column<string>(type: "text", nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    FuzzyText = table.Column<string>(type: "text", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    KindSlug = table.Column<string>(type: "text", nullable: true),
                    EventMode = table.Column<string>(type: "text", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Lat = table.Column<double>(type: "double precision", nullable: true),
                    Lng = table.Column<double>(type: "double precision", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ListedStandalone = table.Column<bool>(type: "boolean", nullable: false),
                    ParentEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeriesMode = table.Column<string>(type: "text", nullable: true),
                    IsSeriesPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    HasAudienceRule = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    RecentViewCount = table.Column<int>(type: "integer", nullable: false),
                    ConversionCount = table.Column<int>(type: "integer", nullable: false),
                    EventCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_search_documents", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_event_search_documents_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_search_documents_City",
                table: "event_search_documents",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_event_search_documents_IsSeriesPrimary",
                table: "event_search_documents",
                column: "IsSeriesPrimary");

            migrationBuilder.CreateIndex(
                name: "IX_event_search_documents_KindSlug",
                table: "event_search_documents",
                column: "KindSlug");

            migrationBuilder.CreateIndex(
                name: "IX_event_search_documents_ParentEventId",
                table: "event_search_documents",
                column: "ParentEventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_search_documents_SeriesId",
                table: "event_search_documents",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_event_search_documents_StartsAt",
                table: "event_search_documents",
                column: "StartsAt");

            // V3 §15 (Phase 16): the weighted FTS document is a Postgres GENERATED tsvector (title → weight A, body →
            // weight B) so the projector only writes plain text and the DB derives + indexes the vector. A GIN index
            // serves full-text; a gin_trgm index over FuzzyText serves typo-tolerant / partial matching (pg_trgm is
            // already enabled). EF can't model a generated tsvector without a provider CLR type, so this is raw DDL.
            migrationBuilder.Sql(@"
                ALTER TABLE event_search_documents ADD COLUMN search_vector tsvector
                GENERATED ALWAYS AS (
                    setweight(to_tsvector('english', coalesce(""TitleText"", '')), 'A') ||
                    setweight(to_tsvector('english', coalesce(""BodyText"", '')), 'B')
                ) STORED;");
            migrationBuilder.Sql(@"CREATE INDEX ""IX_event_search_documents_vector""
                ON event_search_documents USING gin (search_vector);");
            migrationBuilder.Sql(@"CREATE INDEX ""IX_event_search_documents_fuzzy_trgm""
                ON event_search_documents USING gin (""FuzzyText"" gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_search_documents");
        }
    }
}
