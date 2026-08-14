using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRepresentativeDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OfficialPhone",
                table: "event_authorizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeRole",
                table: "event_authorizations",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeRoleOther",
                table: "event_authorizations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RepresentativeUserId",
                table: "event_authorizations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_authorizations_RepresentativeUserId",
                table: "event_authorizations",
                column: "RepresentativeUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_event_authorizations_users_RepresentativeUserId",
                table: "event_authorizations",
                column: "RepresentativeUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_authorizations_users_RepresentativeUserId",
                table: "event_authorizations");

            migrationBuilder.DropIndex(
                name: "IX_event_authorizations_RepresentativeUserId",
                table: "event_authorizations");

            migrationBuilder.DropColumn(
                name: "RepresentativeRole",
                table: "event_authorizations");

            migrationBuilder.DropColumn(
                name: "RepresentativeRoleOther",
                table: "event_authorizations");

            migrationBuilder.DropColumn(
                name: "RepresentativeUserId",
                table: "event_authorizations");

            migrationBuilder.AlterColumn<string>(
                name: "OfficialPhone",
                table: "event_authorizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }
    }
}
