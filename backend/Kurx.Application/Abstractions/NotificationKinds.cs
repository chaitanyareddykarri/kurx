using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

/// <summary>The notification `Kind` catalog (D-20x). `Kind` itself stays a free-form string on the
/// entity (12+ existing call sites already use ad-hoc literals — retrofitting a DB enum for those
/// is out of scope here), but every NEW call site should reference a constant from here instead of
/// inventing another one-off casing/scheme. Every notification's `data` should also include a
/// `route` field (an in-app path, e.g. <c>/u/username</c>) — the one deep-link convention both
/// clients now follow generically, regardless of `Kind`; mobile already had an unused `route`
/// fallback in its parser, this makes it the real, only convention going forward.</summary>
public static class NotificationKinds
{
    public const string AllyRequested = "ally.requested";
    public const string AllyAccepted = "ally.accepted";
    public const string AllyDeclined = "ally.declined";
    public const string AllyRemoved = "ally.removed";

    /// <summary>An event staff assignment was created and is waiting on the invitee (D-319). The
    /// <c>staff.</c> prefix lands it on <see cref="NotificationCategory.StaffInvitations"/>, a category
    /// that has been switchable in settings since D-263 while nothing ever emitted into it.</summary>
    public const string StaffInvited = "staff.invited";
}
