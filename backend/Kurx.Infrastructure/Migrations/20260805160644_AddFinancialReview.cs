using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FinancialReviewNotes",
                table: "events",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinancialReviewStatus",
                table: "events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinancialReviewedAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialReviewedBy",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresFinancialReview",
                table: "event_archetypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinancialReviewNotes",
                table: "events");

            migrationBuilder.DropColumn(
                name: "FinancialReviewStatus",
                table: "events");

            migrationBuilder.DropColumn(
                name: "FinancialReviewedAt",
                table: "events");

            migrationBuilder.DropColumn(
                name: "FinancialReviewedBy",
                table: "events");

            migrationBuilder.DropColumn(
                name: "RequiresFinancialReview",
                table: "event_archetypes");
        }
    }
}
