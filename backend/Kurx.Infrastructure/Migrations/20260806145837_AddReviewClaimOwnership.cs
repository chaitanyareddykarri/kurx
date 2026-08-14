using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewClaimOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewClaimedAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewClaimedBy",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_ReviewClaimedBy",
                table: "events",
                column: "ReviewClaimedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_events_ReviewClaimedBy",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ReviewClaimedAt",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ReviewClaimedBy",
                table: "events");
        }
    }
}
