using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameKycToOrgBankVerification : Migration
    {
        // M9 (D-048): "KYC" now means person identity (M3). The org's bank/PAN verification table is
        // renamed kyc_records -> org_bank_verifications. EF scaffolded a DROP+CREATE (which would lose
        // any rows); this is a DATA-PRESERVING rename of the table, its indexes, and its PK/FK constraints.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "kyc_records", newName: "org_bank_verifications");
            migrationBuilder.RenameIndex(
                name: "IX_kyc_records_OrgId", newName: "IX_org_bank_verifications_OrgId",
                table: "org_bank_verifications");
            migrationBuilder.RenameIndex(
                name: "IX_kyc_records_OrgId_Kind_Status", newName: "IX_org_bank_verifications_OrgId_Kind_Status",
                table: "org_bank_verifications");
            migrationBuilder.Sql(
                "ALTER TABLE org_bank_verifications RENAME CONSTRAINT \"PK_kyc_records\" TO \"PK_org_bank_verifications\";");
            migrationBuilder.Sql(
                "ALTER TABLE org_bank_verifications RENAME CONSTRAINT \"FK_kyc_records_organizations_OrgId\" TO \"FK_org_bank_verifications_organizations_OrgId\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE org_bank_verifications RENAME CONSTRAINT \"FK_org_bank_verifications_organizations_OrgId\" TO \"FK_kyc_records_organizations_OrgId\";");
            migrationBuilder.Sql(
                "ALTER TABLE org_bank_verifications RENAME CONSTRAINT \"PK_org_bank_verifications\" TO \"PK_kyc_records\";");
            migrationBuilder.RenameIndex(
                name: "IX_org_bank_verifications_OrgId_Kind_Status", newName: "IX_kyc_records_OrgId_Kind_Status",
                table: "kyc_records");
            migrationBuilder.RenameIndex(
                name: "IX_org_bank_verifications_OrgId", newName: "IX_kyc_records_OrgId",
                table: "kyc_records");
            migrationBuilder.RenameTable(name: "org_bank_verifications", newName: "kyc_records");
        }
    }
}
