using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateBatchIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_issued_certificates_BatchId_RecipientId",
                table: "issued_certificates",
                columns: new[] { "BatchId", "RecipientId" },
                unique: true,
                filter: "\"BatchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_recipients_BatchId_SourceRowNumber",
                table: "certificate_recipients",
                columns: new[] { "BatchId", "SourceRowNumber" },
                unique: true,
                filter: "\"BatchId\" IS NOT NULL AND \"SourceRowNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_issued_certificates_BatchId_RecipientId",
                table: "issued_certificates");

            migrationBuilder.DropIndex(
                name: "IX_certificate_recipients_BatchId_SourceRowNumber",
                table: "certificate_recipients");
        }
    }
}
