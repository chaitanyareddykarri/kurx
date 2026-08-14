using Kurx.Infrastructure.Jobs;

namespace Kurx.Api.Endpoints;

/// <summary>Operational control for the staged E.164 phone migration (D-089, phase 2).
///
/// <para>The backfill is an operator action, not a startup side effect: running it automatically on boot
/// would mean every deploy silently rewrites identity columns with no one watching the failure count.
/// It is idempotent and resumable, so it is safe to run repeatedly and safe to interrupt.</para></summary>
public static class PhoneMigrationEndpoints
{
    public static void MapPhoneMigrationEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/admin/phone-backfill", async (PhoneE164BackfillJob job, CancellationToken ct) =>
        {
            var report = await job.BackfillAsync(ct);
            return Results.Ok(new
            {
                scanned = report.Scanned,
                converted = report.Converted,
                failed = report.Failed,      // unparseable — inspect the logs, these rows were left alone
                skipped = report.Skipped,    // canonicalized onto a number another row already holds
            });
        }).RequireAuthorization("KurxAdmin").WithTags("admin");
    }
}
