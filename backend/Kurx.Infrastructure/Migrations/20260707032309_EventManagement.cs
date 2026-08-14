using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EventManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "AudienceLevelId",
                table: "events",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialLinksJson",
                table: "events",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subtitle",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Timezone",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "VenueId",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsVisible",
                table: "event_categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Sort",
                table: "event_categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "event_media",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Caption = table.Column<string>(type: "text", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_media", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_media_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_sessions_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DefaultSectionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_templates_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "speakers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Bio = table.Column<string>(type: "text", nullable: false),
                    PhotoKey = table.Column<string>(type: "text", nullable: true),
                    Company = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    SocialLinksJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_speakers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_speakers_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sponsors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LogoKey = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "text", nullable: false),
                    Tier = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sponsors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sponsors_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "venues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Lat = table.Column<double>(type: "double precision", nullable: true),
                    Lng = table.Column<double>(type: "double precision", nullable: true),
                    GoogleMapsUrl = table.Column<string>(type: "text", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    HasParking = table.Column<bool>(type: "boolean", nullable: false),
                    IsAccessible = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_venues_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_session_speakers",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeakerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_session_speakers", x => new { x.SessionId, x.SpeakerId });
                    table.ForeignKey(
                        name: "FK_event_session_speakers_event_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "event_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_session_speakers_speakers_SpeakerId",
                        column: x => x.SpeakerId,
                        principalTable: "speakers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_speakers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeakerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_speakers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_speakers_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_speakers_speakers_SpeakerId",
                        column: x => x.SpeakerId,
                        principalTable: "speakers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_sponsors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SponsorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_sponsors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_sponsors_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_sponsors_sponsors_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "sponsors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_tags",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_tags", x => new { x.EventId, x.TagId });
                    table.ForeignKey(
                        name: "FK_event_tags_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_tags_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venue_images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_venue_images_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_City",
                table: "events",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_events_StartsAt",
                table: "events",
                column: "StartsAt");

            migrationBuilder.CreateIndex(
                name: "IX_events_Status",
                table: "events",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_events_TemplateId",
                table: "events",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_events_VenueId",
                table: "events",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_events_Visibility",
                table: "events",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_event_media_EventId_Kind",
                table: "event_media",
                columns: new[] { "EventId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_event_session_speakers_SpeakerId",
                table: "event_session_speakers",
                column: "SpeakerId");

            migrationBuilder.CreateIndex(
                name: "IX_event_sessions_EventId_StartsAt",
                table: "event_sessions",
                columns: new[] { "EventId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_event_speakers_EventId_SpeakerId",
                table: "event_speakers",
                columns: new[] { "EventId", "SpeakerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_speakers_SpeakerId",
                table: "event_speakers",
                column: "SpeakerId");

            migrationBuilder.CreateIndex(
                name: "IX_event_sponsors_EventId_SponsorId",
                table: "event_sponsors",
                columns: new[] { "EventId", "SponsorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_sponsors_SponsorId",
                table: "event_sponsors",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_event_tags_TagId",
                table: "event_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_event_templates_OrgId",
                table: "event_templates",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_event_templates_Slug",
                table: "event_templates",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_speakers_OrgId",
                table: "speakers",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_sponsors_OrgId",
                table: "sponsors",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_tags_Name",
                table: "tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tags_Slug",
                table: "tags",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_venue_images_VenueId",
                table: "venue_images",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_venues_OrgId",
                table: "venues",
                column: "OrgId");

            migrationBuilder.AddForeignKey(
                name: "FK_events_event_templates_TemplateId",
                table: "events",
                column: "TemplateId",
                principalTable: "event_templates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_events_venues_VenueId",
                table: "events",
                column: "VenueId",
                principalTable: "venues",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_events_event_templates_TemplateId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_venues_VenueId",
                table: "events");

            migrationBuilder.DropTable(
                name: "event_media");

            migrationBuilder.DropTable(
                name: "event_session_speakers");

            migrationBuilder.DropTable(
                name: "event_speakers");

            migrationBuilder.DropTable(
                name: "event_sponsors");

            migrationBuilder.DropTable(
                name: "event_tags");

            migrationBuilder.DropTable(
                name: "event_templates");

            migrationBuilder.DropTable(
                name: "venue_images");

            migrationBuilder.DropTable(
                name: "event_sessions");

            migrationBuilder.DropTable(
                name: "speakers");

            migrationBuilder.DropTable(
                name: "sponsors");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "venues");

            migrationBuilder.DropIndex(
                name: "IX_events_City",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_StartsAt",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_Status",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_TemplateId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_VenueId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_Visibility",
                table: "events");

            migrationBuilder.DropColumn(
                name: "City",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "events");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "events");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "events");

            migrationBuilder.DropColumn(
                name: "SocialLinksJson",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Subtitle",
                table: "events");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Timezone",
                table: "events");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "events");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "events");

            migrationBuilder.DropColumn(
                name: "IsVisible",
                table: "event_categories");

            migrationBuilder.DropColumn(
                name: "Sort",
                table: "event_categories");

            migrationBuilder.AlterColumn<Guid>(
                name: "AudienceLevelId",
                table: "events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
