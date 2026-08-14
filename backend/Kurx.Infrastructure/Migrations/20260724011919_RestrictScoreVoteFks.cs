using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestrictScoreVoteFks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_judge_scores_event_participants_JudgeParticipantId",
                table: "judge_scores");

            migrationBuilder.DropForeignKey(
                name: "FK_public_votes_users_VoterUserId",
                table: "public_votes");

            migrationBuilder.AddForeignKey(
                name: "FK_judge_scores_event_participants_JudgeParticipantId",
                table: "judge_scores",
                column: "JudgeParticipantId",
                principalTable: "event_participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_public_votes_users_VoterUserId",
                table: "public_votes",
                column: "VoterUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_judge_scores_event_participants_JudgeParticipantId",
                table: "judge_scores");

            migrationBuilder.DropForeignKey(
                name: "FK_public_votes_users_VoterUserId",
                table: "public_votes");

            migrationBuilder.AddForeignKey(
                name: "FK_judge_scores_event_participants_JudgeParticipantId",
                table: "judge_scores",
                column: "JudgeParticipantId",
                principalTable: "event_participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_public_votes_users_VoterUserId",
                table: "public_votes",
                column: "VoterUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
