using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketPriceTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_price_tiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinSize = table.Column<int>(type: "integer", nullable: false),
                    MaxSize = table.Column<int>(type: "integer", nullable: false),
                    PricePaise = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_price_tiers", x => x.Id);
                    table.CheckConstraint("ck_ticket_price_tiers_price", "\"PricePaise\" > 0");
                    table.CheckConstraint("ck_ticket_price_tiers_size", "\"MinSize\" >= 1 AND \"MaxSize\" >= \"MinSize\"");
                    table.ForeignKey(
                        name: "FK_ticket_price_tiers_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_price_tiers_ticket_type",
                table: "ticket_price_tiers",
                column: "TicketTypeId");

            /*
             * D-366 — two bands may never cover the same team size, and that is a database guarantee.
             *
             * `TicketTypeService` also checks it, and that check is what gives the organiser a sentence
             * to act on. But a check in application code is only as strong as the assumption that two
             * writers never run at once: two concurrent updates can each read a clean set, each pass, and
             * leave an overlap neither request created. At checkout that means a team size matched by two
             * prices — and the only honest response then is to refuse the sale.
             *
             * `btree_gist` is required because the constraint mixes equality on a uuid with overlap on a
             * range; the stock gist opclasses cover ranges only. It ships with Postgres as a standard
             * contrib module, so this needs no image change.
             */
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(
                """
                ALTER TABLE ticket_price_tiers
                  ADD CONSTRAINT ex_ticket_price_tiers_no_overlap
                  EXCLUDE USING gist (
                    "TicketTypeId" WITH =,
                    int4range("MinSize", "MaxSize", '[]') WITH &&
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_price_tiers");
        }
    }
}
