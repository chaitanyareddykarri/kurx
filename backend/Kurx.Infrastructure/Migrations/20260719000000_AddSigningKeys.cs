using System;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    // Asymmetric JWT signing keys (AM10, D-099). Purely additive: one new table, no change to any
    // existing column, so HS256 issuance keeps working until the ES256 cut-over flips over.
    //
    // The partial unique index is the load-bearing part: it makes "at most one Active signing key"
    // a database invariant rather than a service-layer convention. Two concurrent rotations would
    // otherwise both promote a key, and instances whose JWKS cache had not yet refreshed would
    // reject perfectly valid tokens signed by the one they had not seen.
    //
    // Hand-authored + hand-synced snapshot: the EF design-time host cannot load here (AppControl
    // blocks StackExchange.Redis, 0x800711C7), same approach as AddAuthSubstrate / AddPhoneE164.
    [DbContext(typeof(KurxDbContext))]
    [Migration("20260719000000_AddSigningKeys")]
    public partial class AddSigningKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "signing_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<string>(type: "text", nullable: false),
                    Algorithm = table.Column<string>(type: "text", nullable: false),
                    PublicKeySpki = table.Column<string>(type: "text", nullable: false),
                    PrivateKeyPkcs8 = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetiringAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompromisedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompromiseReason = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_signing_keys", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_signing_keys_KeyId",
                table: "signing_keys",
                column: "KeyId",
                unique: true);

            // At most one Active key, enforced by Postgres.
            migrationBuilder.CreateIndex(
                name: "ix_signing_keys_single_active",
                table: "signing_keys",
                column: "State",
                unique: true,
                filter: "\"State\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible while HS256 issuance is still in place. Once tokens are signed with ES256,
            // dropping this table invalidates every live access token — refresh still works, so the
            // blast radius is one access-token lifetime, but do not run it casually in production.
            migrationBuilder.DropTable(name: "signing_keys");
        }
    }
}
