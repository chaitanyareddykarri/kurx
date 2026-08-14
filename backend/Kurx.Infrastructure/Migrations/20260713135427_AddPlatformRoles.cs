using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_roles_users_GrantedBy",
                        column: x => x.GrantedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_roles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_roles_GrantedBy",
                table: "platform_roles",
                column: "GrantedBy");

            migrationBuilder.CreateIndex(
                name: "IX_platform_roles_UserId",
                table: "platform_roles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_roles_UserId_Role",
                table: "platform_roles",
                columns: new[] { "UserId", "Role" },
                unique: true);

            // Backfill: every existing platform super-admin (users.IsKurxAdmin) becomes a SuperAdmin
            // platform-role grant, so authority moves to platform_roles with no loss (M2, D-040).
            // GrantedBy is NULL = system/migration. IsKurxAdmin is dropped later in M1 (D-041).
            migrationBuilder.Sql(@"
INSERT INTO platform_roles (""Id"", ""UserId"", ""Role"", ""GrantedAt"")
SELECT gen_random_uuid(), ""Id"", 'SuperAdmin', now()
FROM users WHERE ""IsKurxAdmin"" = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_roles");
        }
    }
}
