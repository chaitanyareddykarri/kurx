using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresRepresentation",
                table: "event_archetypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "event_authorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    HeadName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    HeadDesignation = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OfficialEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OfficialPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LetterheadDocumentKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    SignatureDocumentKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    SupportingDocumentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_authorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_authorizations_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_authorizations_users_SubmittedBy",
                        column: x => x.SubmittedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_authorizations_EventId",
                table: "event_authorizations",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_authorizations_Status",
                table: "event_authorizations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_event_authorizations_SubmittedBy",
                table: "event_authorizations",
                column: "SubmittedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_authorizations");

            migrationBuilder.DropColumn(
                name: "RequiresRepresentation",
                table: "event_archetypes");
        }
    }
}
