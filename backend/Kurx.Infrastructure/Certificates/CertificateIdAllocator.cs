using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// Allocates certificate ids with a single atomic statement per call (D-355).
///
/// <para><b>The unsafe implementation this exists to prevent.</b> Read <c>NextSequence</c>, add one in C#,
/// write it back. Two generations running at once both read <c>7</c>, both write <c>8</c>, and both issue
/// <c>CERT-2026-00007</c>. The unique index then fails the second insert — after rendering, storing and
/// possibly emailing a document. That is not a race that shows up in testing; it shows up on the day an
/// organiser generates two batches at once.</para>
///
/// <para><b>What this does instead.</b> One <c>UPDATE … SET "NextSequence" = "NextSequence" + 1 …
/// RETURNING</c>. Postgres takes a row lock for the duration of the update, computes the new value
/// itself, and returns the value that was consumed. A concurrent caller blocks on that lock, then — under
/// READ COMMITTED — re-reads the committed row and increments from the new value. No two callers can be
/// handed the same number, and nothing is serialised beyond the microseconds one row update takes. This
/// is the same "mutate shared counters in SQL" rule the platform already applies elsewhere.</para>
///
/// <para><b>Creating the rule is also a race.</b> Two first-ever generations for one event would both find
/// no rule and both insert one. <c>INSERT … ON CONFLICT DO NOTHING</c> against the unique index on
/// <c>EventId</c> makes the loser a no-op rather than a violation, and the subsequent update then finds
/// exactly one row either way.</para>
/// </summary>
public class CertificateIdAllocator(KurxDbContext db) : ICertificateIdAllocator
{
    public async Task<CertificateIdAllocation> AllocateAsync(
        Guid eventId, DateTime issuedAt, CancellationToken ct = default)
    {
        await EnsureRuleAsync(eventId, ct);

        // Increment and return in one statement. `RETURNING "NextSequence" - 1` yields the value this
        // caller consumed rather than the one the next caller will get — returning the post-increment
        // value would make the first certificate of every event number 2.
        var sequences = await db.Database
            .SqlQueryRaw<long>(
                """
                UPDATE certificate_id_rules
                SET "NextSequence" = "NextSequence" + 1
                WHERE "EventId" = @eventId
                RETURNING "NextSequence" - 1 AS "Value"
                """,
                new NpgsqlParameter("eventId", eventId))
            .ToListAsync(ct);

        if (sequences.Count == 0)
            // The rule was inserted a moment ago and is gone: the event itself was deleted concurrently
            // (the FK cascades). Failing loudly is right — issuing a certificate for a deleted event
            // would produce a document that verifies against nothing.
            throw new InvalidOperationException(
                $"No certificate id rule for event {eventId}; the event may have been deleted.");

        var sequence = sequences[0];

        // Read back for formatting only. This is a plain read of an uncontended-for-formatting row: the
        // prefix, pattern and padding are configuration, and a concurrent change to them does not affect
        // the correctness of the number already allocated above.
        var rule = await db.CertificateIdRules.AsNoTracking()
            .FirstAsync(r => r.EventId == eventId, ct);

        return new CertificateIdAllocation(rule.Format(sequence, issuedAt), sequence);
    }

    /// <summary>Creates the event's id rule if it has none, tolerating a concurrent creator.
    ///
    /// <para>Written as raw SQL rather than "check then add" through the change tracker for the same
    /// reason as the allocation itself: the check-then-act version has a window between the two, and the
    /// unique index turns that window into an exception on a perfectly ordinary request.</para>
    ///
    /// <para><b>The default prefix is the event's short code, not a constant.</b> Sequences are per-event
    /// but <c>issued_certificates.CertificateId</c> is unique PLATFORM-WIDE — verification is by that
    /// value alone, with no event context to disambiguate. A shared default prefix would therefore make
    /// every event's FIRST certificate collide with every other event's first, which is not an edge case
    /// but the common path. <c>Event.ShortCode</c> is already unique per event, so using it as the
    /// default prefix makes ids distinct by construction.</para>
    ///
    /// <para>An organiser may still configure any prefix they like. If two events are given the same one,
    /// their sequences will eventually collide and the unique index refuses the insert — loudly, at issue
    /// time, rather than by silently handing two people the same identifier.</para></summary>
    private async Task EnsureRuleAsync(Guid eventId, CancellationToken ct)
    {
        var defaults = new CertificateIdRule();
        var prefix = await db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => e.ShortCode)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(prefix)) defaults.Prefix = prefix.Trim().ToUpperInvariant();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO certificate_id_rules ("Id", "EventId", "Prefix", "Pattern", "NextSequence", "Padding", "CreatedAt")
            VALUES (@id, @eventId, @prefix, @pattern, @nextSequence, @padding, @createdAt)
            ON CONFLICT ("EventId") DO NOTHING
            """,
            [
                new NpgsqlParameter("id", Guid.NewGuid()),
                new NpgsqlParameter("eventId", eventId),
                new NpgsqlParameter("prefix", defaults.Prefix),
                new NpgsqlParameter("pattern", defaults.Pattern),
                new NpgsqlParameter("nextSequence", defaults.NextSequence),
                new NpgsqlParameter("padding", defaults.Padding),
                new NpgsqlParameter("createdAt", DateTime.UtcNow),
            ],
            ct);
    }
}
