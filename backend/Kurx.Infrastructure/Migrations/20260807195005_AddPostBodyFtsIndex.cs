using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostBodyFtsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-297: PostService.SearchAsync runs websearch_to_tsquery against to_tsvector('english', "Body").
            // Without a GIN index on the SAME expression, Postgres recomputes the vector for every row in
            // posts on every search — a sequential scan over every post body per query. Expression indexes
            // cannot be declared through the EF model, hence raw SQL and an otherwise-empty migration.
            // Mirrors ix_chat_messages_body_fts (D-295), which is the same index for the same reason.
            migrationBuilder.Sql(
                "CREATE INDEX ix_posts_body_fts ON posts USING GIN (to_tsvector('english', \"Body\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_posts_body_fts;");
        }
    }
}
