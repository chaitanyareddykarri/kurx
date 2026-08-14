using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

public record BackfillReport(int Scanned, int Converted, int Failed, int Skipped);

/// <summary>Phase 2 of the staged E.164 migration (D-089): populates <c>PhoneE164</c>/<c>CountryCode</c>/
/// <c>PhoneNational</c> from the legacy <c>Phone</c> column.
///
/// <para><b>Safety properties, all deliberate.</b> It is <b>idempotent</b> — only rows with a null
/// PhoneE164 are considered, so re-running after a partial run resumes rather than redoing. It
/// <b>validates every conversion</b> through libphonenumber and <b>never writes a row it cannot convert
/// safely</b>: an unparseable or invalid legacy number is counted, logged with its user id, and left
/// exactly as it was for a human to look at. It <b>never touches the legacy column</b>, so the whole
/// operation is reversible by dropping the new columns.</para>
///
/// <para><b>The default region is an input, not an assumption.</b> Legacy rows predate country capture and
/// really are all Indian, so the job is run with <c>IN</c>; new registrations never get a default (see
/// <see cref="PhoneCanonicalizer"/>). Most legacy values already carry the 91 country code and parse as
/// international anyway — the region only rescues the minority stored as bare 10-digit numbers.</para></summary>
public class PhoneE164BackfillJob(KurxDbContext db, IConfiguration config, ILogger<PhoneE164BackfillJob> log)
{
    private const int BatchSize = 500;

    /// <summary>The region used to interpret legacy rows that lack a country code.</summary>
    private string LegacyRegion => config["PHONE_BACKFILL_LEGACY_REGION"] ?? "IN";

    public Task RunAsync(CancellationToken ct = default) => BackfillAsync(ct);

    public async Task<BackfillReport> BackfillAsync(CancellationToken ct = default)
    {
        int scanned = 0, converted = 0, failed = 0, skipped = 0;

        while (true)
        {
            var batch = await db.Users
                .Where(u => u.PhoneE164 == null && u.Phone != null)
                .OrderBy(u => u.Id)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batch.Count == 0) break;

            var progressed = false;
            foreach (var user in batch)
            {
                scanned++;

                // Legacy values are bare digits with the country code already present ("919876543210"),
                // so try international form first and only fall back to the legacy region.
                if (!PhoneCanonicalizer.TryParse("+" + user.Phone, null, out var parsed)
                    && !PhoneCanonicalizer.TryParse(user.Phone, LegacyRegion, out parsed))
                {
                    failed++;
                    log.LogWarning("Phone backfill could not convert user {UserId}; row left unchanged", user.Id);
                    continue;
                }

                // A collision means two legacy rows canonicalize to the same number — a real data problem
                // that must be resolved by a human, not silently by whichever row we happened to hit first.
                var taken = await db.Users.AnyAsync(u => u.PhoneE164 == parsed.E164 && u.Id != user.Id, ct);
                if (taken)
                {
                    skipped++;
                    log.LogError("Phone backfill collision for user {UserId}: {E164} already claimed; row left unchanged",
                        user.Id, parsed.E164);
                    continue;
                }

                user.PhoneE164 = parsed.E164;
                user.CountryCode = parsed.CountryCode;
                user.PhoneNational = parsed.National;
                converted++;
                progressed = true;
            }

            await db.SaveChangesAsync(ct);

            // Rows that failed or collided keep a null PhoneE164 and would be re-selected forever;
            // stop once a whole batch produced no writes.
            if (!progressed) break;
            if (batch.Count < BatchSize) break;
        }

        log.LogInformation("Phone E.164 backfill complete: scanned={Scanned} converted={Converted} failed={Failed} skipped={Skipped}",
            scanned, converted, failed, skipped);
        return new BackfillReport(scanned, converted, failed, skipped);
    }
}
