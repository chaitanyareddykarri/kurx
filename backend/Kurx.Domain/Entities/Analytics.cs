using System;

namespace Kurx.Domain.Entities;

/// <summary>Append-only record of one public view of an event's detail page (D-130).
///
/// <para>Replaces the synchronous <c>events.ViewCount++</c> that used to run on every public read — an
/// UPDATE on a hot row, on the most-read path in the system. Appending here has no row contention.</para>
///
/// <para><b>No PII.</b> <see cref="VisitorKey"/> is a date-salted SHA-256 of the signed-in user id, or of
/// IP + User-Agent for anonymous viewers. The raw IP is never stored, and the daily salt means the same
/// visitor produces a different key tomorrow — so a key cannot track anyone across days.</para>
///
/// <para>V3 §16 (Phase 17): this is a <b>leaf fact</b> — the sole source for "views"/"unique visitors",
/// read at request time by <c>AnalyticsFactSource</c>. The pre-aggregated <c>EventAnalyticsDaily</c>/
/// <c>OrganizationAnalytics</c> rollup tables this comment used to describe were retired in Phase 17: every
/// number they held is reconstructable from this table and the other leaf-fact tables (VAR, Registration,
/// Ticket, Refund), so nothing is lost — only the daily pre-computation step, which read-time aggregation
/// makes unnecessary.</para>
/// </summary>
public class EventView
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
    public string VisitorKey { get; set; } = null!;
    public Guid? UserId { get; set; }
}
