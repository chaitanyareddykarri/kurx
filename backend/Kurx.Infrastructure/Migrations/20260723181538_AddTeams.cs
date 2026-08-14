using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "team_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinSize = table.Column<int>(type: "integer", nullable: false),
                    MaxSize = table.Column<int>(type: "integer", nullable: false),
                    FormationMode = table.Column<string>(type: "text", nullable: false),
                    JoinApproval = table.Column<string>(type: "text", nullable: false),
                    LockAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NameEditUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RosterEditUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MentorEditUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxTeamsPerPersonInEvent = table.Column<int>(type: "integer", nullable: false),
                    UnlimitedTeamsPerTree = table.Column<bool>(type: "boolean", nullable: false),
                    AllowSoloAsTeam = table.Column<bool>(type: "boolean", nullable: false),
                    AllowCrossOrgMembers = table.Column<bool>(type: "boolean", nullable: false),
                    SubstitutesAllowed = table.Column<int>(type: "integer", nullable: false),
                    SubstitutionDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IncompleteTeamPolicy = table.Column<string>(type: "text", nullable: false),
                    WaitlistConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_policies", x => x.Id);
                    table.CheckConstraint("ck_team_policies_size", "\"MinSize\" >= 1 AND \"MaxSize\" >= \"MinSize\"");
                    table.ForeignKey(
                        name: "FK_team_policies_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_policies_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    Tagline = table.Column<string>(type: "text", nullable: true),
                    DeclaredOrgUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: true),
                    MergedIntoTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teams_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_teams_org_units_DeclaredOrgUnitId",
                        column: x => x.DeclaredOrgUnitId,
                        principalTable: "org_units",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_teams_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_invites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteePersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    InviteeEmail = table.Column<string>(type: "text", nullable: true),
                    InviteePhone = table.Column<string>(type: "text", nullable: true),
                    Token = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_invites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_team_invites_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_invites_users_InviteePersonId",
                        column: x => x.InviteePersonId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "team_join_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_join_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_team_join_requests_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_join_requests_users_PersonId",
                        column: x => x.PersonId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    ReplacedByMembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_team_memberships_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_memberships_users_PersonId",
                        column: x => x.PersonId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_team_invites_InviteePersonId",
                table: "team_invites",
                column: "InviteePersonId");

            migrationBuilder.CreateIndex(
                name: "IX_team_invites_TeamId",
                table: "team_invites",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_team_invites_Token",
                table: "team_invites",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_team_join_requests_pending",
                table: "team_join_requests",
                columns: new[] { "TeamId", "PersonId" },
                unique: true,
                filter: "\"State\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_team_join_requests_PersonId",
                table: "team_join_requests",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_team_join_requests_TeamId",
                table: "team_join_requests",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "ix_team_memberships_active_person",
                table: "team_memberships",
                columns: new[] { "TeamId", "PersonId" },
                unique: true,
                filter: "\"PersonId\" IS NOT NULL AND \"State\" IN ('Invited','Requested','Active')");

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_PersonId",
                table: "team_memberships",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_TeamId",
                table: "team_memberships",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_team_policies_EventId",
                table: "team_policies",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_team_policies_TicketTypeId",
                table: "team_policies",
                column: "TicketTypeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teams_DeclaredOrgUnitId",
                table: "teams",
                column: "DeclaredOrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_teams_EventId_Slug",
                table: "teams",
                columns: new[] { "EventId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teams_TicketTypeId",
                table: "teams",
                column: "TicketTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_invites");

            migrationBuilder.DropTable(
                name: "team_join_requests");

            migrationBuilder.DropTable(
                name: "team_memberships");

            migrationBuilder.DropTable(
                name: "team_policies");

            migrationBuilder.DropTable(
                name: "teams");
        }
    }
}
