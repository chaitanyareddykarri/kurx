using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateAccessLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "certificate_access_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_access_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certificate_access_links_certificate_recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "certificate_recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_certificate_access_links_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_certificate_access_links_users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_certificate_access_links_CreatedByUserId",
                table: "certificate_access_links",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_access_links_RecipientId",
                table: "certificate_access_links",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_access_links_RevokedByUserId",
                table: "certificate_access_links",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_access_links_TokenHash",
                table: "certificate_access_links",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "certificate_access_links");
        }
    }
}
