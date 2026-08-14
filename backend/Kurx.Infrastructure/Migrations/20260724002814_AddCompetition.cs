using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scoring_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SourcesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Aggregation = table.Column<string>(type: "text", nullable: false),
                    Normalisation = table.Column<string>(type: "text", nullable: false),
                    TieBreakJson = table.Column<string>(type: "jsonb", nullable: true),
                    ConflictRulesJson = table.Column<string>(type: "jsonb", nullable: true),
                    VoteIdentityBinding = table.Column<string>(type: "text", nullable: false),
                    VoteRateLimitPerHour = table.Column<int>(type: "integer", nullable: false),
                    VoteWeightCapPercent = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_policies", x => x.Id);
                    table.CheckConstraint("ck_scoring_weight_cap", "\"VoteWeightCapPercent\" >= 0 AND \"VoteWeightCapPercent\" <= 100");
                    table.ForeignKey(
                        name: "FK_scoring_policies_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Format = table.Column<string>(type: "text", nullable: false),
                    ParticipantSource = table.Column<string>(type: "text", nullable: false),
                    AdvancedFromStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdvancementRule = table.Column<string>(type: "text", nullable: false),
                    AdvancementThreshold = table.Column<int>(type: "integer", nullable: true),
                    ScoringPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    ResultsVisibility = table.Column<string>(type: "text", nullable: false),
                    SpectatorPoolId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stages_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stages_inventory_pools_SpectatorPoolId",
                        column: x => x.SpectatorPoolId,
                        principalTable: "inventory_pools",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_stages_scoring_policies_ScoringPolicyId",
                        column: x => x.ScoringPolicyId,
                        principalTable: "scoring_policies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_stages_stages_AdvancedFromStageId",
                        column: x => x.AdvancedFromStageId,
                        principalTable: "stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stages_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "fixtures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNo = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: true),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    SlotStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SlotEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixtures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fixtures_stages_StageId",
                        column: x => x.StageId,
                        principalTable: "stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fixtures_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "public_votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_public_votes_stages_StageId",
                        column: x => x.StageId,
                        principalTable: "stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_public_votes_users_VoterUserId",
                        column: x => x.VoterUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stage_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: true),
                    Advanced = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stage_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stage_participants_stages_StageId",
                        column: x => x.StageId,
                        principalTable: "stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stage_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    FinalScore = table.Column<decimal>(type: "numeric", nullable: true),
                    ScoreBreakdownJson = table.Column<string>(type: "jsonb", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stage_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stage_results_stages_StageId",
                        column: x => x.StageId,
                        principalTable: "stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fixture_officials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FixtureId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixture_officials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fixture_officials_event_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "event_participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fixture_officials_fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fixture_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FixtureId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixture_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fixture_participants_fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "judge_scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    FixtureId = table.Column<Guid>(type: "uuid", nullable: true),
                    JudgeParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    BreakdownJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_judge_scores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_judge_scores_event_participants_JudgeParticipantId",
                        column: x => x.JudgeParticipantId,
                        principalTable: "event_participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_judge_scores_fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "fixtures",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_judge_scores_stages_StageId",
                        column: x => x.StageId,
                        principalTable: "stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "result_corrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrectedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    PreviousValueJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_result_corrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_result_corrections_stage_results_ResultId",
                        column: x => x.ResultId,
                        principalTable: "stage_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fixture_officials_FixtureId_ParticipantId",
                table: "fixture_officials",
                columns: new[] { "FixtureId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fixture_officials_ParticipantId",
                table: "fixture_officials",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_fixture_participants_FixtureId_SubjectType_SubjectId",
                table: "fixture_participants",
                columns: new[] { "FixtureId", "SubjectType", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_StageId_RoundNo",
                table: "fixtures",
                columns: new[] { "StageId", "RoundNo" });

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_VenueId",
                table: "fixtures",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_judge_scores_FixtureId",
                table: "judge_scores",
                column: "FixtureId");

            migrationBuilder.CreateIndex(
                name: "IX_judge_scores_JudgeParticipantId",
                table: "judge_scores",
                column: "JudgeParticipantId");

            migrationBuilder.CreateIndex(
                name: "ix_judge_scores_unique",
                table: "judge_scores",
                columns: new[] { "StageId", "JudgeParticipantId", "SubjectType", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_public_votes_one_per_voter",
                table: "public_votes",
                columns: new[] { "StageId", "VoterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_votes_VoterUserId_CreatedAt",
                table: "public_votes",
                columns: new[] { "VoterUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_result_corrections_ResultId",
                table: "result_corrections",
                column: "ResultId");

            migrationBuilder.CreateIndex(
                name: "IX_scoring_policies_EventId",
                table: "scoring_policies",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_stage_participants_StageId_SubjectType_SubjectId",
                table: "stage_participants",
                columns: new[] { "StageId", "SubjectType", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stage_results_StageId_SubjectType_SubjectId",
                table: "stage_results",
                columns: new[] { "StageId", "SubjectType", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stages_AdvancedFromStageId",
                table: "stages",
                column: "AdvancedFromStageId");

            migrationBuilder.CreateIndex(
                name: "IX_stages_EventId_Sequence",
                table: "stages",
                columns: new[] { "EventId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stages_ScoringPolicyId",
                table: "stages",
                column: "ScoringPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_stages_SpectatorPoolId",
                table: "stages",
                column: "SpectatorPoolId");

            migrationBuilder.CreateIndex(
                name: "IX_stages_VenueId",
                table: "stages",
                column: "VenueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fixture_officials");

            migrationBuilder.DropTable(
                name: "fixture_participants");

            migrationBuilder.DropTable(
                name: "judge_scores");

            migrationBuilder.DropTable(
                name: "public_votes");

            migrationBuilder.DropTable(
                name: "result_corrections");

            migrationBuilder.DropTable(
                name: "stage_participants");

            migrationBuilder.DropTable(
                name: "fixtures");

            migrationBuilder.DropTable(
                name: "stage_results");

            migrationBuilder.DropTable(
                name: "stages");

            migrationBuilder.DropTable(
                name: "scoring_policies");
        }
    }
}
