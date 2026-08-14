using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalChainsAndMaterialChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RefundWindowEndsAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "approval_chains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_chains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_chains_org_units_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "org_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChainId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_requests_approval_chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "approval_chains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_requests_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChainId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    ApproverRole = table.Column<int>(type: "integer", nullable: true),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Condition = table.Column<string>(type: "text", nullable: false),
                    ConditionParamPaise = table.Column<long>(type: "bigint", nullable: true),
                    SlaHours = table.Column<int>(type: "integer", nullable: true),
                    EscalationAfterHours = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_steps_approval_chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "approval_chains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_step_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_step_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_step_decisions_approval_requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "approval_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_approval_step_decisions_approval_steps_StepId",
                        column: x => x.StepId,
                        principalTable: "approval_steps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_chains_OrgUnitId",
                table: "approval_chains",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_ChainId",
                table: "approval_requests",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_EventId_ChainId",
                table: "approval_requests",
                columns: new[] { "EventId", "ChainId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_step_decisions_RequestId_StepId",
                table: "approval_step_decisions",
                columns: new[] { "RequestId", "StepId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_step_decisions_StepId",
                table: "approval_step_decisions",
                column: "StepId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_steps_ChainId_Sort",
                table: "approval_steps",
                columns: new[] { "ChainId", "Sort" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_step_decisions");

            migrationBuilder.DropTable(
                name: "approval_requests");

            migrationBuilder.DropTable(
                name: "approval_steps");

            migrationBuilder.DropTable(
                name: "approval_chains");

            migrationBuilder.DropColumn(
                name: "RefundWindowEndsAt",
                table: "events");
        }
    }
}
