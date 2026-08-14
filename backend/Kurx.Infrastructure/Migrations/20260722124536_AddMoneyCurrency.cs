using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "withdrawals",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "transfers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ticket_types",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "refunds",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "payout_schedules",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "SettlementCurrency",
                table: "organizations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "organization_wallet",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "organization_analytics",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "orders",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "order_items",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ledger_entries",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "SettlementCurrency",
                table: "events",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "event_analytics_daily",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "withdrawals");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ticket_types");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "payout_schedules");

            migrationBuilder.DropColumn(
                name: "SettlementCurrency",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "organization_wallet");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "organization_analytics");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "SettlementCurrency",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "event_analytics_daily");
        }
    }
}
