using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

/// <summary>What a caller may do on one event. **Adding a new event capability means adding a value here
/// and one row in <see cref="EventAuthority.Requirements"/> — never writing authorization logic** (D-269).
///
/// <para>Values are deliberately coarse. A finer permission per sub-resource would just be eleven copies
/// of "Manager or better" under different names, which is the duplication this replaced.</para>
///
/// <para>Two families live here, and the distinction is load-bearing (D-272): the <b>management</b>
/// permissions below <see cref="Participate"/> ask "may this caller run the event", while
/// <see cref="Participate"/> and <see cref="ModerateAudience"/> ask "does this caller belong to the
/// event's audience". Audience surfaces must never be gated on a management permission — a ticket
/// holder posting to an event feed is not a manager, and requiring one would be a silent lockout.</para></summary>
public enum EventPermission
{
    /// <summary>See the event at all, including in a non-published state.</summary>
    View,

    /// <summary>Take part in the event's audience surfaces: read its feed, attach a post to it, and belong
    /// to its chat room. The audience floor — anyone with any standing at all, management implied nowhere
    /// (D-272). A ticket holder, an accepted speaker, front-of-house staff, a manager and the owner all
    /// clear it; a <c>Finance</c> seat and a stranger do not.</summary>
    Participate,

    /// <summary>Hold the moderator seat on the event's audience surfaces — chat <c>Host</c>: pin, moderate,
    /// mention-all, and post while the room is hosts-only. Front-of-house and up, matching
    /// <see cref="ViewAttendees"/>'s bar without borrowing its meaning (D-272).</summary>
    ModerateAudience,

    /// <summary>Read the attendee roster and export it. Deliberately below <see cref="ManageContent"/>:
    /// door staff need the list without being able to change what is being sold.</summary>
    ViewAttendees,

    /// <summary>Read aggregate analytics and payment readiness.</summary>
    ViewAnalytics,

    /// <summary>Create/update/delete anything hanging off the event — tickets, schedule, media, speakers,
    /// sponsors, announcements, invitations, certificates, audience rules, assignments, and every future
    /// capability of the same shape.</summary>
    ManageContent,

    /// <summary>Drive the event's own lifecycle: edit its details, transition, clone, publish, cancel.</summary>
    ManageLifecycle,

    /// <summary>Delete the event outright.</summary>
    Delete,
}

/// <summary>How much authority a caller holds over one event, ordered weakest to strongest. A caller is
/// resolved to exactly one level and every permission is a threshold on it.</summary>
public enum EventAuthorityLevel
{
    /// <summary>No relationship to the event.</summary>
    None = 0,

    /// <summary>Belongs to this event's audience, read-only: an accepted programme participation (speaker,
    /// judge, mentor, volunteer, …) <b>or</b> a live ticket (D-272). Both are the same standing — "part of
    /// this event, with no authority over it" — so they resolve to the same rung rather than to two
    /// parallel concepts that every audience surface would then have to check twice.</summary>
    Participant = 1,

    /// <summary>Holds a <c>Staff</c> seat in the organization the event represents — front-of-house.</summary>
    Staff = 2,

    /// <summary>May run this event. Reached three ways, all equal (D-268/D-269):
    /// <list type="bullet">
    /// <item>the event's <b>creator</b> — the owner; Kurx is user-first and this needs no membership;</item>
    /// <item>a <b>Representative</b> of the organization it represents — the D-075 organizer role;</item>
    /// <item>an <b>Owner</b> or <b>Manager</b> seat in that organization — collaborators.</item>
    /// </list></summary>
    Manager = 3,

    /// <summary>Platform administrator (<c>KurxAdmin</c>). Bypasses the rest.</summary>
    Admin = 4,
}

/// <summary>The caller's resolved standing on one event. <see cref="EventExists"/> is separate from
/// <see cref="Level"/> so a caller can be told apart from a missing event — callers that hide existence
/// (404-not-403, D-018) need both facts and must not infer one from the other.</summary>
public sealed record EventAccess(
    bool EventExists,
    EventAuthorityLevel Level,
    Guid OrgId,
    Guid CreatedBy)
{
    public static readonly EventAccess NotFound = new(false, EventAuthorityLevel.None, Guid.Empty, Guid.Empty);

    public bool Can(EventPermission permission) => Level >= EventAuthority.Requirements[permission];

    /// <summary>The caller has some standing on this event (any level above None) — the test for
    /// "may this person know the event exists", independent of what they may do with it.</summary>
    public bool HasStanding => Level > EventAuthorityLevel.None;
}

/// <summary>The permission → minimum-authority table. This is the whole authorization policy for events,
/// in one place, readable end to end.</summary>
public static class EventAuthority
{
    public static readonly IReadOnlyDictionary<EventPermission, EventAuthorityLevel> Requirements =
        new Dictionary<EventPermission, EventAuthorityLevel>
        {
            [EventPermission.View] = EventAuthorityLevel.Participant,
            [EventPermission.Participate] = EventAuthorityLevel.Participant,
            [EventPermission.ModerateAudience] = EventAuthorityLevel.Staff,
            [EventPermission.ViewAttendees] = EventAuthorityLevel.Staff,
            [EventPermission.ViewAnalytics] = EventAuthorityLevel.Manager,
            [EventPermission.ManageContent] = EventAuthorityLevel.Manager,
            [EventPermission.ManageLifecycle] = EventAuthorityLevel.Manager,
            [EventPermission.Delete] = EventAuthorityLevel.Manager,
        };

    /// <summary>The single role → authority mapping in the platform (D-269). Every surface reaches this one
    /// switch; before D-269 there were eleven of them and they had drifted.</summary>
    public static EventAuthorityLevel LevelFor(OrgRole? role) => role switch
    {
        // Representative is manage-capable by D-075's own definition ("a verified, manage-capable role that
        // is not Owner"). Nine sub-resources had silently omitted it.
        OrgRole.Owner or OrgRole.Manager or OrgRole.Representative => EventAuthorityLevel.Manager,
        OrgRole.Staff => EventAuthorityLevel.Staff,
        // Finance holds money authority over the organization, not operational authority over its events —
        // it reads the wallet, never the ticket types, and it is not part of an event's audience.
        OrgRole.Finance => EventAuthorityLevel.None,
        _ => EventAuthorityLevel.None,
    };

    /// <summary>The seats reaching <paramref name="level"/> or better, for the handful of places that must
    /// <i>filter or enumerate</i> rather than resolve one caller — an EF predicate cannot call
    /// <see cref="LevelFor"/>, and a hand-written role list beside it would be exactly the drift D-269
    /// removed. Derived from <see cref="LevelFor"/> at load, so the two can never disagree (D-272).</summary>
    public static OrgRole[] RolesAtLeast(EventAuthorityLevel level) =>
        Enum.GetValues<OrgRole>().Where(r => LevelFor(r) >= level).ToArray();

    /// <summary>Seats carrying audience standing on the organization's events — the query-side mirror of
    /// <see cref="EventPermission.Participate"/>.</summary>
    public static readonly OrgRole[] AudienceRoles = RolesAtLeast(EventAuthorityLevel.Participant);

    /// <summary>Seats carrying the moderator seat on an event's audience surfaces — the query-side mirror
    /// of <see cref="EventPermission.ModerateAudience"/>.</summary>
    public static readonly OrgRole[] ModeratorRoles = RolesAtLeast(EventAuthorityLevel.Staff);

    /// <summary>Seats that make someone a **chat Host** (D-300).
    ///
    /// <para>Deliberately <see cref="EventAuthorityLevel.Manager"/>, one rung above
    /// <see cref="ModeratorRoles"/>: a <c>Staff</c> seat is operational authority over an event, not
    /// moderation authority over the conversation in it. D-300 draws that line — "Being staff is an
    /// operational role. It is not moderation authority." Staff still joins the room, as a Member.</para>
    ///
    /// <para>A separate array rather than a reuse of <see cref="ModeratorRoles"/> precisely so that
    /// changing one cannot silently move the other: they answer different questions, and before D-300
    /// chat borrowed the moderator list and inherited a bar it did not want.</para></summary>
    public static readonly OrgRole[] ChatHostRoles = RolesAtLeast(EventAuthorityLevel.Manager);
}

/// <summary>**The single source of truth for event authorization** (D-269).
///
/// <para>Before this, eleven services each carried their own <c>CanManage</c>/<c>RoleAsync</c> pair or an
/// inline membership query. Seven were byte-identical; the rest had drifted, and the drift was invisible
/// because nothing compared them. Two consequences shipped: the <c>Representative</c> role — the platform's
/// primary organizer role since D-075 — could create and publish an event but not add a ticket type to it,
/// and after D-268 an event's own creator held no authority over its sub-resources.</para>
///
/// <para>No event sub-resource may implement its own permission logic. Resolve once, then ask
/// <see cref="EventAccess.Can"/>.</para></summary>
public interface IEventAuthority
{
    /// <summary>Resolves the caller's standing on an event. <paramref name="userId"/> null means
    /// anonymous. Returns <see cref="EventAccess.NotFound"/> when the event does not exist or
    /// is soft-deleted, so callers can answer 404 without a second read.
    ///
    /// <para>Costs one to four sequential queries — event, then organization seat, then programme
    /// participation, then ticket — short-circuiting at the first that resolves. The event's own
    /// creator stops at one and a seated manager at two, so the common management call is cheap; a
    /// caller with no standing at all is the one that pays for all four. Nothing is cached, and that
    /// is deliberate: D-015 requires the resolution to be live, so a revoked seat or a voided ticket
    /// stops granting access on the very next request rather than at the next login.</para></summary>
    Task<EventAccess> ResolveAsync(Guid? userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Resolves the caller's standing on an <b>organization</b>, for the reusable assets an
    /// organization owns rather than an event does — its speaker roster, sponsor roster and venue book.
    /// Those are library entries shared across many events, so "may I edit this org's speakers" is
    /// genuinely an organization question and cannot be answered from an event.
    ///
    /// <para>It lives on this interface rather than in a second service so there is still exactly one role
    /// → authority mapping in the codebase. Splitting it out would recreate, one layer up, precisely the
    /// divergence D-269 removed.</para></summary>
    Task<OrgAuthority> ResolveOrgAsync(Guid userId, Guid orgId, bool isAdmin, CancellationToken ct = default);
}

/// <summary>The caller's standing on an organization. <see cref="IsMember"/> is deliberately separate from
/// <see cref="Level"/>: several org-asset <i>reads</i> accept any seat at all — including Finance, which
/// holds no operational authority — and collapsing the two would silently narrow them.</summary>
public sealed record OrgAuthority(bool IsMember, EventAuthorityLevel Level)
{
    public static readonly OrgAuthority None = new(false, EventAuthorityLevel.None);

    public bool CanManage => Level >= EventAuthorityLevel.Manager;
}
