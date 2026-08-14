using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityComponentStatusAndPennyDrop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankNameMatch",
                table: "user_identity_verifications",
                type: "text",
                nullable: false,
                defaultValue: "NotChecked");

            migrationBuilder.AddColumn<string>(
                name: "BankStatus",
                table: "user_identity_verifications",
                type: "text",
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<DateTime>(
                name: "BankVerifiedAt",
                table: "user_identity_verifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GovtIdStatus",
                table: "user_identity_verifications",
                type: "text",
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<string>(
                name: "PanStatus",
                table: "user_identity_verifications",
                type: "text",
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<string>(
                name: "PennyDropStatus",
                table: "user_identity_verifications",
                type: "text",
                nullable: false,
                defaultValue: "NotStarted");
            // Backfill: derive each component from the evidence already on the row.
            //
            // A masked value is only ever written on provider APPROVAL — `RunAsync` applies
            // `onApprove` and nothing else sets these columns — so `GovtIdLast4 IS NOT NULL` means
            // that component genuinely passed. Leaving every existing row at NotStarted would tell
            // people who completed KYC months ago that they had never begun.
            //
            // Penny drop and name match follow `BankLast4` for the same reason and no further:
            // `SubmitBankAsync` has always called `IKycProvider.PennyDropAsync` with the holder name,
            // and only records the account on approval. A stored account number therefore already
            // implies a passed drop; this records what happened rather than asserting anything new.
            migrationBuilder.Sql(@"
                UPDATE user_identity_verifications SET
                    ""GovtIdStatus""    = CASE WHEN ""GovtIdLast4"" IS NOT NULL THEN 'Approved'  ELSE 'NotStarted' END,
                    ""PanStatus""       = CASE WHEN ""PanLast4""    IS NOT NULL THEN 'Approved'  ELSE 'NotStarted' END,
                    ""BankStatus""      = CASE WHEN ""BankLast4""   IS NOT NULL THEN 'Approved'  ELSE 'NotStarted' END,
                    ""PennyDropStatus"" = CASE WHEN ""BankLast4""   IS NOT NULL THEN 'Passed'    ELSE 'NotStarted' END,
                    ""BankNameMatch""   = CASE WHEN ""BankLast4""   IS NOT NULL THEN 'Match'     ELSE 'NotChecked' END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankNameMatch",
                table: "user_identity_verifications");

            migrationBuilder.DropColumn(
                name: "BankStatus",
                table: "user_identity_verifications");

            migrationBuilder.DropColumn(
                name: "BankVerifiedAt",
                table: "user_identity_verifications");

            migrationBuilder.DropColumn(
                name: "GovtIdStatus",
                table: "user_identity_verifications");

            migrationBuilder.DropColumn(
                name: "PanStatus",
                table: "user_identity_verifications");

            migrationBuilder.DropColumn(
                name: "PennyDropStatus",
                table: "user_identity_verifications");
        }
    }
}
