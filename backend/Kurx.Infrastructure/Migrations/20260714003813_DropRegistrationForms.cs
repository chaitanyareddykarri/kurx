using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropRegistrationForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registration_response_values");

            migrationBuilder.DropTable(
                name: "registration_fields");

            migrationBuilder.DropTable(
                name: "registration_responses");

            migrationBuilder.DropTable(
                name: "registration_forms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registration_forms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PresetTaxonomy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_forms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_registration_forms_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registration_fields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    FieldKey = table.Column<string>(type: "text", nullable: false),
                    FieldType = table.Column<string>(type: "text", nullable: false),
                    FormId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    OptionsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ValidationRulesJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_registration_fields_registration_forms_FormId",
                        column: x => x.FormId,
                        principalTable: "registration_forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registration_responses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_responses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_registration_responses_order_items_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "order_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_registration_responses_registration_forms_FormId",
                        column: x => x.FormId,
                        principalTable: "registration_forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_registration_responses_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "registration_response_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: true),
                    ValueText = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_response_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_registration_response_values_registration_fields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "registration_fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_registration_response_values_registration_responses_Respons~",
                        column: x => x.ResponseId,
                        principalTable: "registration_responses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_registration_fields_FormId_DisplayOrder",
                table: "registration_fields",
                columns: new[] { "FormId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_registration_fields_FormId_FieldKey",
                table: "registration_fields",
                columns: new[] { "FormId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registration_forms_EventId",
                table: "registration_forms",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_response_values_FieldId",
                table: "registration_response_values",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_response_values_ResponseId",
                table: "registration_response_values",
                column: "ResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_response_values_ResponseId_FieldId",
                table: "registration_response_values",
                columns: new[] { "ResponseId", "FieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registration_responses_FormId",
                table: "registration_responses",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_responses_OrderItemId",
                table: "registration_responses",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_responses_UserId",
                table: "registration_responses",
                column: "UserId");
        }
    }
}
