using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail loudly with an actionable message instead of a bare Postgres unique-violation
            // error if any case-insensitive duplicate emails already exist (D-038).
            migrationBuilder.Sql(@"
DO $$
DECLARE dup_count int;
BEGIN
    SELECT count(*) INTO dup_count FROM (
        SELECT lower(""Email"") FROM users WHERE ""Email"" IS NOT NULL
        GROUP BY lower(""Email"") HAVING count(*) > 1
    ) d;
    IF dup_count > 0 THEN
        RAISE EXCEPTION 'Cannot add case-insensitive unique index on users.Email: % duplicate email(s) found. Resolve manually before migrating -- see docs/DECISIONS.md D-038.', dup_count;
    END IF;
END $$;
");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_users_Email_Lower\" ON users (lower(\"Email\")) WHERE \"Email\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_users_Email_Lower\";");
        }
    }
}
