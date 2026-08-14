using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventCreationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ticket_types",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "MinAmountPaise",
                table: "ticket_types",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundPolicy",
                table: "ticket_types",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedAmountsJson",
                table: "ticket_types",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Booth",
                table: "sponsors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoClose",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Building",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationPolicy",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CertificateReleaseAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckinClosesAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckinOpensAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodeOfConduct",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentText",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaqJson",
                table: "events",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Floor",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenderRestriction",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GoogleMapsUrl",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoKey",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAge",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxTeams",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingPassword",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingPlatform",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinAge",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PlatformFeeFlatPaise",
                table: "events",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeePercent",
                table: "events",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrizePoolJson",
                table: "events",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoVideoKey",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundPolicy",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationClosesAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationOpensAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresConsent",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResultDate",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Room",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rules",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "events",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                table: "events",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TaxInclusive",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxPercent",
                table: "events",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsText",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsUrl",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailKey",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "coupons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ValuePaise = table.Column<long>(type: "bigint", nullable: true),
                    MaxRedemptions = table.Column<int>(type: "integer", nullable: true),
                    RedeemedCount = table.Column<int>(type: "integer", nullable: false),
                    MaxPerUser = table.Column<int>(type: "integer", nullable: false),
                    MinOrderPaise = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_coupons_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registration_consents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsentTextHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Ip = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_consents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_registration_consents_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "coupon_redemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CouponId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiscountPaise = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupon_redemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_coupon_redemptions_coupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "coupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_events_registration_closes_active",
                table: "events",
                column: "RegistrationClosesAt",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_coupon_redemptions_CouponId_OrderId",
                table: "coupon_redemptions",
                columns: new[] { "CouponId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coupon_redemptions_CouponId_UserId",
                table: "coupon_redemptions",
                columns: new[] { "CouponId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_coupons_EventId_Code",
                table: "coupons",
                columns: new[] { "EventId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registration_consents_EventId",
                table: "registration_consents",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_consents_RegistrationId",
                table: "registration_consents",
                column: "RegistrationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coupon_redemptions");

            migrationBuilder.DropTable(
                name: "registration_consents");

            migrationBuilder.DropTable(
                name: "coupons");

            migrationBuilder.DropIndex(
                name: "ix_events_registration_closes_active",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ticket_types");

            migrationBuilder.DropColumn(
                name: "MinAmountPaise",
                table: "ticket_types");

            migrationBuilder.DropColumn(
                name: "RefundPolicy",
                table: "ticket_types");

            migrationBuilder.DropColumn(
                name: "SuggestedAmountsJson",
                table: "ticket_types");

            migrationBuilder.DropColumn(
                name: "Booth",
                table: "sponsors");

            migrationBuilder.DropColumn(
                name: "AutoClose",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Building",
                table: "events");

            migrationBuilder.DropColumn(
                name: "CancellationPolicy",
                table: "events");

            migrationBuilder.DropColumn(
                name: "CertificateReleaseAt",
                table: "events");

            migrationBuilder.DropColumn(
                name: "CheckinClosesAt",
                table: "events");

            migrationBuilder.DropColumn(
                name: "CheckinOpensAt",
                table: "events");

            migrationBuilder.DropColumn(
                name: "CodeOfConduct",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ConsentText",
                table: "events");

            migrationBuilder.DropColumn(
                name: "FaqJson",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "events");

            migrationBuilder.DropColumn(
                name: "GenderRestriction",
                table: "events");

            migrationBuilder.DropColumn(
                name: "GoogleMapsUrl",
                table: "events");

            migrationBuilder.DropColumn(
                name: "LogoKey",
                table: "events");

            migrationBuilder.DropColumn(
                name: "MaxAge",
                table: "events");

            migrationBuilder.DropColumn(
                name: "MaxTeams",
                table: "events");

            migrationBuilder.DropColumn(
                name: "MeetingPassword",
                table: "events");

            migrationBuilder.DropColumn(
                name: "MeetingPlatform",
                table: "events");

            migrationBuilder.DropColumn(
                name: "MinAge",
                table: "events");

            migrationBuilder.DropColumn(
                name: "PlatformFeeFlatPaise",
                table: "events");

            migrationBuilder.DropColumn(
                name: "PlatformFeePercent",
                table: "events");

            migrationBuilder.DropColumn(
                name: "PrizePoolJson",
                table: "events");

            migrationBuilder.DropColumn(
                name: "PromoVideoKey",
                table: "events");

            migrationBuilder.DropColumn(
                name: "RefundPolicy",
                table: "events");

            migrationBuilder.DropColumn(
                name: "RegistrationClosesAt",
                table: "events");

            migrationBuilder.DropColumn(
                name: "RegistrationOpensAt",
                table: "events");

            migrationBuilder.DropColumn(
                name: "RequiresConsent",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ResultDate",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Room",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Rules",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Tagline",
                table: "events");

            migrationBuilder.DropColumn(
                name: "TaxInclusive",
                table: "events");

            migrationBuilder.DropColumn(
                name: "TaxPercent",
                table: "events");

            migrationBuilder.DropColumn(
                name: "TermsText",
                table: "events");

            migrationBuilder.DropColumn(
                name: "TermsUrl",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ThumbnailKey",
                table: "events");
        }
    }
}
