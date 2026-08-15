using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateSigningKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "certificate_signing_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Algorithm = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PublicKeySpki = table.Column<string>(type: "text", nullable: false),
                    ProtectedPrivateKey = table.Column<string>(type: "text", nullable: true),
                    ProtectionScheme = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompromisedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompromisedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_signing_keys", x => x.Id);
                    table.CheckConstraint("ck_certificate_signing_keys_state", "\"State\" IN ('Active', 'Retired', 'Compromised')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_certificate_signing_keys_KeyId",
                table: "certificate_signing_keys",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificate_signing_keys_State",
                table: "certificate_signing_keys",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "certificate_signing_keys");
        }
    }
}
