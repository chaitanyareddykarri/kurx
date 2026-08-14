using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entitlement_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    MealSlot = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CustomLabel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ImageKey = table.Column<string>(type: "text", nullable: true),
                    PricePaise = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "INR"),
                    Delivery = table.Column<string>(type: "text", nullable: false),
                    Inclusion = table.Column<string>(type: "text", nullable: false),
                    MaxPerParticipant = table.Column<int>(type: "integer", nullable: false),
                    AllowsPartialRedemption = table.Column<bool>(type: "boolean", nullable: false),
                    AvailableFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AvailableUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RedemptionLocationsJson = table.Column<string>(type: "jsonb", nullable: false),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entitlement_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entitlement_products_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entitlement_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntitlementProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RedeemedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SecureToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CouponNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupersededByGrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entitlement_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entitlement_grants_entitlement_products_EntitlementProductId",
                        column: x => x.EntitlementProductId,
                        principalTable: "entitlement_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_entitlement_grants_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entitlement_redemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntitlementGrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RedeemedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entitlement_redemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entitlement_redemptions_entitlement_grants_EntitlementGrant~",
                        column: x => x.EntitlementGrantId,
                        principalTable: "entitlement_grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_entitlement_redemptions_users_RedeemedByUserId",
                        column: x => x.RedeemedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_grants_EntitlementProductId_CouponNumber",
                table: "entitlement_grants",
                columns: new[] { "EntitlementProductId", "CouponNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_grants_EntitlementProductId_Status",
                table: "entitlement_grants",
                columns: new[] { "EntitlementProductId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_grants_RegistrationId",
                table: "entitlement_grants",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_grants_SecureToken",
                table: "entitlement_grants",
                column: "SecureToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_grants_UserId",
                table: "entitlement_grants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_products_EventId_IsPublished",
                table: "entitlement_products",
                columns: new[] { "EventId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_redemptions_EntitlementGrantId",
                table: "entitlement_redemptions",
                column: "EntitlementGrantId");

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_redemptions_IdempotencyKey",
                table: "entitlement_redemptions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_redemptions_RedeemedByUserId",
                table: "entitlement_redemptions",
                column: "RedeemedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entitlement_redemptions");

            migrationBuilder.DropTable(
                name: "entitlement_grants");

            migrationBuilder.DropTable(
                name: "entitlement_products");
        }
    }
}
