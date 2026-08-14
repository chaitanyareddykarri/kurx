namespace Kurx.Application.Abstractions;

/// <summary>
/// Builds <c>Notification.DedupKey</c> values (DB-3).
///
/// <para><b>Why a type rather than a string literal at each site.</b> The key is written by a producer and
/// read back by a completely different consumer — the announcement fan-out writes it and re-reads it on a
/// resumed send; the reminder job writes it and re-reads it on the next hourly tick. Two hand-built strings
/// that disagree by one character do not fail: they silently stop deduplicating, and the symptom is a user
/// receiving the same reminder every hour. One builder makes that class of bug impossible.</para>
///
/// <para><b>Only two kinds appear here, and that is the design.</b> Every other notification kind
/// legitimately repeats. Adding a kind to this file means asserting that a second such notification to the
/// same user is always wrong — which is true for an announcement and a reminder, and false for a material
/// change, an invitation, an ally request or an authorization verdict. When in doubt, leave
/// <c>DedupKey</c> null: an un-deduplicated notification is a nuisance, a wrongly-deduplicated one is a
/// message the user never receives.</para>
/// </summary>
public static class NotificationDedup
{
    /// <summary>The kind string the announcement fan-out writes. A literal here rather than in
    /// <c>NotificationKinds</c> because that file is the catalog for NEW call sites, while this value is
    /// load-bearing for rows that already exist and must not be renamed casually.</summary>
    public const string EventAnnouncementKind = "event_announcement";

    /// <summary>The kind string <c>EventReminderJob</c> writes. Note the PascalCase: it is what the job has
    /// always emitted, and existing rows carry it. Renaming it to match the <c>event_*</c> convention is a
    /// data migration, not a cosmetic edit, and is deliberately not done here.</summary>
    public const string EventReminderKind = "EventReminder";

    /// <summary>One in-app notification per recipient per announcement.</summary>
    public static string ForAnnouncement(Guid announcementId) => $"{EventAnnouncementKind}:{announcementId}";

    /// <summary>One reminder per recipient per event.</summary>
    public static string ForEventReminder(Guid eventId) => $"{EventReminderKind}:{eventId}";
}
