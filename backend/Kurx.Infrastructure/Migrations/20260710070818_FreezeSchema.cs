using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FreezeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_events_Status",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_Visibility",
                table: "events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_withdrawals_amount_paise",
                table: "withdrawals",
                sql: "\"AmountPaise\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_username_history_UserId",
                table: "username_history",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_EventId_State",
                table: "tickets",
                columns: new[] { "EventId", "State" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_types_per_user_limit",
                table: "ticket_types",
                sql: "\"PerUserLimit\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_types_price_paise",
                table: "ticket_types",
                sql: "\"PricePaise\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_types_quantity",
                table: "ticket_types",
                sql: "\"Quantity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_seat_holds_qty",
                table: "seat_holds",
                sql: "\"Qty\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_risk_flags_EventId",
                table: "risk_flags",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_active",
                table: "refresh_tokens",
                column: "UserId",
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_otp_requests_phone_unexpired",
                table: "otp_requests",
                columns: new[] { "Phone", "ExpiresAt" },
                filter: "\"Consumed\" = false");

            migrationBuilder.AddCheckConstraint(
                name: "ck_org_wallet_advanced",
                table: "organization_wallet",
                sql: "\"AdvancedPaise\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_org_wallet_available",
                table: "organization_wallet",
                sql: "\"AvailablePaise\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_org_wallet_collected",
                table: "organization_wallet",
                sql: "\"CollectedPaise\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_org_wallet_lifetime_earned",
                table: "organization_wallet",
                sql: "\"LifetimeEarnedPaise\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_org_wallet_lifetime_withdrawn",
                table: "organization_wallet",
                sql: "\"LifetimeWithdrawnPaise\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_org_wallet_reserved",
                table: "organization_wallet",
                sql: "\"ReservedPaise\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_org_wallet_settled",
                table: "organization_wallet",
                sql: "\"SettledPaise\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_amount_paise",
                table: "orders",
                sql: "\"AmountPaise\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_items_qty",
                table: "order_items",
                sql: "\"Qty\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_RefType_RefId",
                table: "ledger_entries",
                columns: new[] { "RefType", "RefId" });

            migrationBuilder.CreateIndex(
                name: "IX_groups_OrderId",
                table: "groups",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_groups_TicketTypeId",
                table: "groups",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_generated_cards_TemplateId",
                table: "generated_cards",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_gate_entries_ScannedBy",
                table: "gate_entries",
                column: "ScannedBy");

            migrationBuilder.CreateIndex(
                name: "ix_events_status_active",
                table: "events",
                column: "Status",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_events_visibility_active",
                table: "events",
                column: "Visibility",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_event_reviews_rating",
                table: "event_reviews",
                sql: "\"Rating\" >= 1 AND \"Rating\" <= 5");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_ActorId",
                table: "audit_log",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_CreatedAt",
                table: "audit_log",
                column: "CreatedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_devices_users_UserId",
                table: "devices",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_gate_entries_users_ScannedBy",
                table: "gate_entries",
                column: "ScannedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_generated_cards_design_templates_TemplateId",
                table: "generated_cards",
                column: "TemplateId",
                principalTable: "design_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_generated_cards_events_EventId",
                table: "generated_cards",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_groups_orders_OrderId",
                table: "groups",
                column: "OrderId",
                principalTable: "orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_groups_ticket_types_TicketTypeId",
                table: "groups",
                column: "TicketTypeId",
                principalTable: "ticket_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_kyc_records_organizations_OrgId",
                table: "kyc_records",
                column: "OrgId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_events_EventId",
                table: "ledger_entries",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_organizations_OrgId",
                table: "ledger_entries",
                column: "OrgId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_users_UserId",
                table: "notifications",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payout_schedules_organizations_OrgId",
                table: "payout_schedules",
                column: "OrgId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_users_UserId",
                table: "refresh_tokens",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_risk_flags_events_EventId",
                table: "risk_flags",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_risk_flags_organizations_OrgId",
                table: "risk_flags",
                column: "OrgId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transfers_organizations_OrgId",
                table: "transfers",
                column: "OrgId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_username_history_users_UserId",
                table: "username_history",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_withdrawals_organizations_OrgId",
                table: "withdrawals",
                column: "OrgId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_devices_users_UserId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_gate_entries_users_ScannedBy",
                table: "gate_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_generated_cards_design_templates_TemplateId",
                table: "generated_cards");

            migrationBuilder.DropForeignKey(
                name: "FK_generated_cards_events_EventId",
                table: "generated_cards");

            migrationBuilder.DropForeignKey(
                name: "FK_groups_orders_OrderId",
                table: "groups");

            migrationBuilder.DropForeignKey(
                name: "FK_groups_ticket_types_TicketTypeId",
                table: "groups");

            migrationBuilder.DropForeignKey(
                name: "FK_kyc_records_organizations_OrgId",
                table: "kyc_records");

            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_events_EventId",
                table: "ledger_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_organizations_OrgId",
                table: "ledger_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_users_UserId",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_payout_schedules_organizations_OrgId",
                table: "payout_schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_refresh_tokens_users_UserId",
                table: "refresh_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_risk_flags_events_EventId",
                table: "risk_flags");

            migrationBuilder.DropForeignKey(
                name: "FK_risk_flags_organizations_OrgId",
                table: "risk_flags");

            migrationBuilder.DropForeignKey(
                name: "FK_transfers_organizations_OrgId",
                table: "transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_username_history_users_UserId",
                table: "username_history");

            migrationBuilder.DropForeignKey(
                name: "FK_withdrawals_organizations_OrgId",
                table: "withdrawals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_withdrawals_amount_paise",
                table: "withdrawals");

            migrationBuilder.DropIndex(
                name: "IX_username_history_UserId",
                table: "username_history");

            migrationBuilder.DropIndex(
                name: "IX_tickets_EventId_State",
                table: "tickets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_types_per_user_limit",
                table: "ticket_types");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_types_price_paise",
                table: "ticket_types");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_types_quantity",
                table: "ticket_types");

            migrationBuilder.DropCheckConstraint(
                name: "ck_seat_holds_qty",
                table: "seat_holds");

            migrationBuilder.DropIndex(
                name: "IX_risk_flags_EventId",
                table: "risk_flags");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_user_active",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_otp_requests_phone_unexpired",
                table: "otp_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_org_wallet_advanced",
                table: "organization_wallet");

            migrationBuilder.DropCheckConstraint(
                name: "ck_org_wallet_available",
                table: "organization_wallet");

            migrationBuilder.DropCheckConstraint(
                name: "ck_org_wallet_collected",
                table: "organization_wallet");

            migrationBuilder.DropCheckConstraint(
                name: "ck_org_wallet_lifetime_earned",
                table: "organization_wallet");

            migrationBuilder.DropCheckConstraint(
                name: "ck_org_wallet_lifetime_withdrawn",
                table: "organization_wallet");

            migrationBuilder.DropCheckConstraint(
                name: "ck_org_wallet_reserved",
                table: "organization_wallet");

            migrationBuilder.DropCheckConstraint(
                name: "ck_org_wallet_settled",
                table: "organization_wallet");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_amount_paise",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_items_qty",
                table: "order_items");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_RefType_RefId",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_groups_OrderId",
                table: "groups");

            migrationBuilder.DropIndex(
                name: "IX_groups_TicketTypeId",
                table: "groups");

            migrationBuilder.DropIndex(
                name: "IX_generated_cards_TemplateId",
                table: "generated_cards");

            migrationBuilder.DropIndex(
                name: "IX_gate_entries_ScannedBy",
                table: "gate_entries");

            migrationBuilder.DropIndex(
                name: "ix_events_status_active",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_visibility_active",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_event_reviews_rating",
                table: "event_reviews");

            migrationBuilder.DropIndex(
                name: "IX_audit_log_ActorId",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "IX_audit_log_CreatedAt",
                table: "audit_log");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_events_Status",
                table: "events",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_events_Visibility",
                table: "events",
                column: "Visibility");
        }
    }
}
