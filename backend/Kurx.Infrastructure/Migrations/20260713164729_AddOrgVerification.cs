using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerificationNotes",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationReviewedAt",
                table: "organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerificationReviewedBy",
                table: "organizations",
                type: "uuid",
                nullable: true);

            // Existing orgs (and the column default) start Unverified, not the EF-scaffolded "".
            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "organizations",
                type: "text",
                nullable: false,
                defaultValue: "Unverified");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationNotes",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "VerificationReviewedAt",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "VerificationReviewedBy",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "organizations");
        }
    }
}
