using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

/// <summary>
/// Maps a notification's free-form <c>Kind</c> onto the <see cref="NotificationCategory"/> a user
/// actually gets to switch off (D-263).
///
/// <para>The two vocabularies are deliberately different. <c>Kind</c> is what the code emits and there
/// are dozens of them, invented per feature (<c>post_like</c>, <c>ally.requested</c>, <c>chat_mention</c>,
/// <c>event_announcement</c>…). A category is what a person recognises in a settings screen. Collapsing
/// them here — rather than renaming every emitter — is what let preferences ship without touching 23
/// call sites.</para>
///
/// <para><b>An unmapped kind falls back to <see cref="NotificationCategory.System"/>, not to "send
/// anyway".</b> A kind nobody mapped is a notification the user was never offered a choice about, and
/// silently exempting it from preferences is how a preferences screen becomes a lie. System is
/// suppressible, so the failure mode is "a new notification type respects the user's System setting"
/// rather than "a new notification type ignores every setting".</para>
/// </summary>
public static class NotificationCategories
{
    /// <summary>Prefix → category. Ordered longest-prefix-first at lookup, so <c>post_</c> cannot
    /// shadow a more specific <c>posts_digest_</c> added later.</summary>
    private static readonly (string Prefix, NotificationCategory Category)[] Map =
    [
        ("security", NotificationCategory.Security),
        ("account.security", NotificationCategory.Security),
        ("login", NotificationCategory.Security),
        ("device", NotificationCategory.Security),
        ("password", NotificationCategory.Security),
        ("recovery", NotificationCategory.Security),
        ("step_up", NotificationCategory.Security),
        ("email_change", NotificationCategory.Security),
        ("phone_change", NotificationCategory.Security),
        ("account_deletion", NotificationCategory.Security),

        ("post_", NotificationCategory.Posts),
        ("chat_", NotificationCategory.Messages),
        ("dm_", NotificationCategory.Messages),
        ("ally.", NotificationCategory.ConnectionRequests),
        ("connection", NotificationCategory.ConnectionRequests),

        ("event_announcement", NotificationCategory.Announcements),
        ("announcement", NotificationCategory.Announcements),
        ("event_", NotificationCategory.EventUpdates),

        ("staff_invitation", NotificationCategory.StaffInvitations),
        ("staff.", NotificationCategory.StaffInvitations),
        ("team_invitation", NotificationCategory.TeamInvitations),
        ("team.", NotificationCategory.TeamInvitations),
        ("invitation", NotificationCategory.Invitations),
        ("invite", NotificationCategory.Invitations),

        ("payment", NotificationCategory.Payments),
        ("refund", NotificationCategory.Payments),
        ("payout", NotificationCategory.Payments),
        ("order", NotificationCategory.Payments),
        ("wallet", NotificationCategory.Payments),

        ("certificate", NotificationCategory.Certificates),
    ];

    public static NotificationCategory For(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return NotificationCategory.System;
        var k = kind.Trim().ToLowerInvariant();

        NotificationCategory? best = null;
        var bestLength = -1;
        foreach (var (prefix, category) in Map)
            if (k.StartsWith(prefix, StringComparison.Ordinal) && prefix.Length > bestLength)
            {
                best = category;
                bestLength = prefix.Length;
            }

        return best ?? NotificationCategory.System;
    }

    /// <summary>Security notices are never suppressible (D-263) — a user cannot consent to being kept
    /// quiet about their own account being taken over. Checked server-side on both the write path (the
    /// PATCH refuses) and the dispatch path (this), because either alone is one bug away from silence.</summary>
    public static bool IsAlwaysDelivered(NotificationCategory category)
        => category == NotificationCategory.Security;

    /// <summary>Wire name, snake_case, matching the settings contract.</summary>
    public static string Wire(NotificationCategory category) => category switch
    {
        NotificationCategory.EventUpdates => "event_updates",
        NotificationCategory.Invitations => "invitations",
        NotificationCategory.StaffInvitations => "staff_invitations",
        NotificationCategory.TeamInvitations => "team_invitations",
        NotificationCategory.ConnectionRequests => "connection_requests",
        NotificationCategory.Payments => "payments",
        NotificationCategory.Certificates => "certificates",
        NotificationCategory.Messages => "messages",
        NotificationCategory.Posts => "posts",
        NotificationCategory.Announcements => "announcements",
        NotificationCategory.Security => "security",
        _ => "system",
    };

    public static NotificationCategory? Parse(string? wire) => (wire ?? "").Trim().ToLowerInvariant() switch
    {
        "event_updates" => NotificationCategory.EventUpdates,
        "invitations" => NotificationCategory.Invitations,
        "staff_invitations" => NotificationCategory.StaffInvitations,
        "team_invitations" => NotificationCategory.TeamInvitations,
        "connection_requests" => NotificationCategory.ConnectionRequests,
        "payments" => NotificationCategory.Payments,
        "certificates" => NotificationCategory.Certificates,
        "messages" => NotificationCategory.Messages,
        "posts" => NotificationCategory.Posts,
        "announcements" => NotificationCategory.Announcements,
        "security" => NotificationCategory.Security,
        "system" => NotificationCategory.System,
        _ => null,
    };

    public static IReadOnlyList<NotificationCategory> All { get; } =
        Enum.GetValues<NotificationCategory>();
}
