using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddV2Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_venues_OrgId",
                table: "venues");

            migrationBuilder.DropIndex(
                name: "IX_ticket_types_EventId",
                table: "ticket_types");

            migrationBuilder.DropIndex(
                name: "IX_sponsors_OrgId",
                table: "sponsors");

            migrationBuilder.DropIndex(
                name: "IX_speakers_OrgId",
                table: "speakers");

            migrationBuilder.DropIndex(
                name: "IX_organizations_Slug",
                table: "organizations");

            migrationBuilder.RenameIndex(
                name: "IX_chat_messages_RoomId",
                table: "chat_messages",
                newName: "ix_chat_messages_pinned");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "venues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ticket_types",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "sponsors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "speakers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "design_templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false),
                    CustomRole = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ShowOnProfile = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_assignments_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_assignments_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_assignments_users_InvitedBy",
                        column: x => x.InvitedBy,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_event_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_checkin_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "text", nullable: false),
                    DeviceTokenHash = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_checkin_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_checkin_devices_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_checkin_devices_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_checkin_devices_users_RegisteredBy",
                        column: x => x.RegisteredBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: true),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_reviews_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_reviews_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_event_reviews_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "org_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvitedPhone = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_org_invitations_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_org_invitations_users_InvitedBy",
                        column: x => x.InvitedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_org_invitations_users_InvitedUserId",
                        column: x => x.InvitedUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "organization_followers",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_followers", x => new { x.UserId, x.OrgId });
                    table.ForeignKey(
                        name: "FK_organization_followers_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_followers_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_wallet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectedPaise = table.Column<long>(type: "bigint", nullable: false),
                    AvailablePaise = table.Column<long>(type: "bigint", nullable: false),
                    AdvancedPaise = table.Column<long>(type: "bigint", nullable: false),
                    ReservedPaise = table.Column<long>(type: "bigint", nullable: false),
                    SettledPaise = table.Column<long>(type: "bigint", nullable: false),
                    LifetimeEarnedPaise = table.Column<long>(type: "bigint", nullable: false),
                    LifetimeWithdrawnPaise = table.Column<long>(type: "bigint", nullable: false),
                    LastLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_wallet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_wallet_ledger_entries_LastLedgerEntryId",
                        column: x => x.LastLedgerEntryId,
                        principalTable: "ledger_entries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_organization_wallet_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registration_forms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    PresetTaxonomy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reports_users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_events",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_events", x => new { x.UserId, x.EventId });
                    table.ForeignKey(
                        name: "FK_saved_events_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_saved_events_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_waitlist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfferExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_waitlist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_waitlist_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ticket_waitlist_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ticket_waitlist_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "username_change_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldUsername = table.Column<string>(type: "text", nullable: true),
                    NewUsername = table.Column<string>(type: "text", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_username_change_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_username_change_log_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registration_fields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    FieldType = table.Column<string>(type: "text", nullable: false),
                    OptionsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    ResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValueText = table.Column<string>(type: "text", nullable: true),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: true)
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
                name: "IX_withdrawals_OrgId_Status",
                table: "withdrawals",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_venues_org_active",
                table: "venues",
                column: "OrgId",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_CheckedInBy",
                table: "tickets",
                column: "CheckedInBy");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_GroupMemberId",
                table: "tickets",
                column: "GroupMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_OrderItemId",
                table: "tickets",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_types_event_active",
                table: "ticket_types",
                column: "EventId",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sponsors_org_active",
                table: "sponsors",
                column: "OrgId",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_speakers_org_active",
                table: "speakers",
                column: "OrgId",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_slug_active",
                table: "organizations",
                column: "Slug",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_orders_EventId_Status",
                table: "orders",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_UserId_Status",
                table: "orders",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_TicketTypeId",
                table: "order_items",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_unread",
                table: "notifications",
                column: "UserId",
                filter: "\"ReadAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_OrgId_State_CreatedAt",
                table: "ledger_entries",
                columns: new[] { "OrgId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_kyc_records_OrgId_Kind_Status",
                table: "kyc_records",
                columns: new[] { "OrgId", "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_group_members_UserId",
                table: "group_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_events_AudienceLevelId",
                table: "events",
                column: "AudienceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_events_CategoryId",
                table: "events",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_events_CertificateTemplateId",
                table: "events",
                column: "CertificateTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_events_Country_State_City",
                table: "events",
                columns: new[] { "Country", "State", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_events_InviteTemplateId",
                table: "events",
                column: "InviteTemplateId");

            migrationBuilder.CreateIndex(
                name: "ix_events_status_starts_active",
                table: "events",
                columns: new[] { "Status", "StartsAt" },
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_events_TypeId",
                table: "events",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_email_logs_ToEmail_CreatedAt",
                table: "email_logs",
                columns: new[] { "ToEmail", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_design_templates_EventId",
                table: "design_templates",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_design_templates_OrgId",
                table: "design_templates",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_TemplateId",
                table: "certificates",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_TicketId",
                table: "certificates",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_event_assignments_EventId_Status",
                table: "event_assignments",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_event_assignments_EventId_UserId_Role",
                table: "event_assignments",
                columns: new[] { "EventId", "UserId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_assignments_InvitedBy",
                table: "event_assignments",
                column: "InvitedBy");

            migrationBuilder.CreateIndex(
                name: "IX_event_assignments_OrgId",
                table: "event_assignments",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "ix_event_assignments_user_profile",
                table: "event_assignments",
                column: "UserId",
                filter: "\"ShowOnProfile\" = true");

            migrationBuilder.CreateIndex(
                name: "ix_checkin_devices_event_active",
                table: "event_checkin_devices",
                column: "EventId",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_event_checkin_devices_DeviceTokenHash",
                table: "event_checkin_devices",
                column: "DeviceTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_checkin_devices_OrgId",
                table: "event_checkin_devices",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_event_checkin_devices_RegisteredBy",
                table: "event_checkin_devices",
                column: "RegisteredBy");

            migrationBuilder.CreateIndex(
                name: "ix_event_reviews_event_rating_active",
                table: "event_reviews",
                columns: new[] { "EventId", "Rating" },
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_event_reviews_EventId_UserId",
                table: "event_reviews",
                columns: new[] { "EventId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_reviews_TicketId",
                table: "event_reviews",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "ix_event_reviews_user_active",
                table: "event_reviews",
                column: "UserId",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_org_invitations_InvitedBy",
                table: "org_invitations",
                column: "InvitedBy");

            migrationBuilder.CreateIndex(
                name: "IX_org_invitations_InvitedUserId_Status",
                table: "org_invitations",
                columns: new[] { "InvitedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_org_invitations_OrgId_Status",
                table: "org_invitations",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_org_invitations_Token",
                table: "org_invitations",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_followers_OrgId",
                table: "organization_followers",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_organization_wallet_LastLedgerEntryId",
                table: "organization_wallet",
                column: "LastLedgerEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_organization_wallet_OrgId",
                table: "organization_wallet",
                column: "OrgId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_reports_EntityType_EntityId",
                table: "reports",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_open",
                table: "reports",
                column: "Status",
                filter: "\"Status\" = 'open'");

            migrationBuilder.CreateIndex(
                name: "IX_reports_ReporterId",
                table: "reports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_saved_events_EventId",
                table: "saved_events",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_waitlist_EventId",
                table: "ticket_waitlist",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_waitlist_TicketTypeId_UserId",
                table: "ticket_waitlist",
                columns: new[] { "TicketTypeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_waitlist_UserId_Status",
                table: "ticket_waitlist",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_expiry_notified",
                table: "ticket_waitlist",
                column: "OfferExpiresAt",
                filter: "\"Status\" = 'Notified'");

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_type_pos_waiting",
                table: "ticket_waitlist",
                columns: new[] { "TicketTypeId", "Position" },
                filter: "\"Status\" = 'Waiting'");

            migrationBuilder.CreateIndex(
                name: "IX_username_change_log_UserId",
                table: "username_change_log",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_username_change_log_UserId_ChangedAt",
                table: "username_change_log",
                columns: new[] { "UserId", "ChangedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_certificates_design_templates_TemplateId",
                table: "certificates",
                column: "TemplateId",
                principalTable: "design_templates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_certificates_events_EventId",
                table: "certificates",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_certificates_tickets_TicketId",
                table: "certificates",
                column: "TicketId",
                principalTable: "tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_certificates_users_UserId",
                table: "certificates",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_design_templates_events_EventId",
                table: "design_templates",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_design_templates_organizations_OrgId",
                table: "design_templates",
                column: "OrgId",
                principalTable: "organizations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_events_design_templates_CertificateTemplateId",
                table: "events",
                column: "CertificateTemplateId",
                principalTable: "design_templates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_events_design_templates_InviteTemplateId",
                table: "events",
                column: "InviteTemplateId",
                principalTable: "design_templates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_events_event_categories_AudienceLevelId",
                table: "events",
                column: "AudienceLevelId",
                principalTable: "event_categories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_events_event_categories_CategoryId",
                table: "events",
                column: "CategoryId",
                principalTable: "event_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_events_event_categories_TypeId",
                table: "events",
                column: "TypeId",
                principalTable: "event_categories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_events_users_CreatedBy",
                table: "events",
                column: "CreatedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_group_members_users_UserId",
                table: "group_members",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_ticket_types_TicketTypeId",
                table: "order_items",
                column: "TicketTypeId",
                principalTable: "ticket_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_users_UserId",
                table: "orders",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_refunds_orders_OrderId",
                table: "refunds",
                column: "OrderId",
                principalTable: "orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_group_members_GroupMemberId",
                table: "tickets",
                column: "GroupMemberId",
                principalTable: "group_members",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_order_items_OrderItemId",
                table: "tickets",
                column: "OrderItemId",
                principalTable: "order_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_users_CheckedInBy",
                table: "tickets",
                column: "CheckedInBy",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_users_UserId",
                table: "tickets",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_certificates_design_templates_TemplateId",
                table: "certificates");

            migrationBuilder.DropForeignKey(
                name: "FK_certificates_events_EventId",
                table: "certificates");

            migrationBuilder.DropForeignKey(
                name: "FK_certificates_tickets_TicketId",
                table: "certificates");

            migrationBuilder.DropForeignKey(
                name: "FK_certificates_users_UserId",
                table: "certificates");

            migrationBuilder.DropForeignKey(
                name: "FK_design_templates_events_EventId",
                table: "design_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_design_templates_organizations_OrgId",
                table: "design_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_events_design_templates_CertificateTemplateId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_design_templates_InviteTemplateId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_event_categories_AudienceLevelId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_event_categories_CategoryId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_event_categories_TypeId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_users_CreatedBy",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_group_members_users_UserId",
                table: "group_members");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_ticket_types_TicketTypeId",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_users_UserId",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_refunds_orders_OrderId",
                table: "refunds");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_group_members_GroupMemberId",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_order_items_OrderItemId",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_users_CheckedInBy",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_users_UserId",
                table: "tickets");

            migrationBuilder.DropTable(
                name: "event_assignments");

            migrationBuilder.DropTable(
                name: "event_checkin_devices");

            migrationBuilder.DropTable(
                name: "event_reviews");

            migrationBuilder.DropTable(
                name: "org_invitations");

            migrationBuilder.DropTable(
                name: "organization_followers");

            migrationBuilder.DropTable(
                name: "organization_wallet");

            migrationBuilder.DropTable(
                name: "registration_response_values");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "saved_events");

            migrationBuilder.DropTable(
                name: "ticket_waitlist");

            migrationBuilder.DropTable(
                name: "username_change_log");

            migrationBuilder.DropTable(
                name: "registration_fields");

            migrationBuilder.DropTable(
                name: "registration_responses");

            migrationBuilder.DropTable(
                name: "registration_forms");

            migrationBuilder.DropIndex(
                name: "IX_withdrawals_OrgId_Status",
                table: "withdrawals");

            migrationBuilder.DropIndex(
                name: "ix_venues_org_active",
                table: "venues");

            migrationBuilder.DropIndex(
                name: "IX_tickets_CheckedInBy",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_tickets_GroupMemberId",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_tickets_OrderItemId",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_ticket_types_event_active",
                table: "ticket_types");

            migrationBuilder.DropIndex(
                name: "ix_sponsors_org_active",
                table: "sponsors");

            migrationBuilder.DropIndex(
                name: "ix_speakers_org_active",
                table: "speakers");

            migrationBuilder.DropIndex(
                name: "ix_organizations_slug_active",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_orders_EventId_Status",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_UserId_Status",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_order_items_TicketTypeId",
                table: "order_items");

            migrationBuilder.DropIndex(
                name: "ix_notifications_user_unread",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_OrgId_State_CreatedAt",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_kyc_records_OrgId_Kind_Status",
                table: "kyc_records");

            migrationBuilder.DropIndex(
                name: "IX_group_members_UserId",
                table: "group_members");

            migrationBuilder.DropIndex(
                name: "IX_events_AudienceLevelId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_CategoryId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_CertificateTemplateId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_Country_State_City",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_InviteTemplateId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_status_starts_active",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_TypeId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_email_logs_ToEmail_CreatedAt",
                table: "email_logs");

            migrationBuilder.DropIndex(
                name: "IX_design_templates_EventId",
                table: "design_templates");

            migrationBuilder.DropIndex(
                name: "IX_design_templates_OrgId",
                table: "design_templates");

            migrationBuilder.DropIndex(
                name: "IX_certificates_TemplateId",
                table: "certificates");

            migrationBuilder.DropIndex(
                name: "IX_certificates_TicketId",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ticket_types");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "sponsors");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "speakers");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "events");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "events");

            migrationBuilder.DropColumn(
                name: "District",
                table: "events");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "events");

            migrationBuilder.DropColumn(
                name: "State",
                table: "events");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "design_templates");

            migrationBuilder.RenameIndex(
                name: "ix_chat_messages_pinned",
                table: "chat_messages",
                newName: "IX_chat_messages_RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_venues_OrgId",
                table: "venues",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_types_EventId",
                table: "ticket_types",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_sponsors_OrgId",
                table: "sponsors",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_speakers_OrgId",
                table: "speakers",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Slug",
                table: "organizations",
                column: "Slug",
                unique: true);
        }
    }
}
