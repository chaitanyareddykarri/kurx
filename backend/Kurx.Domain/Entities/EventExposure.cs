using System.Linq.Expressions;
using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>D-266 M3 Step 4 — the one rule for public exposure.
///
/// <para><b>An event is publicly visible only when its Product is Public AND its Visibility is Listed.</b>
/// Everything else — Unlisted, InviteOnly, the legacy Private, and every Private product regardless of its
/// visibility value — is excluded from every discoverable surface.</para>
///
/// <para>This exists as one expression rather than twenty-three copies of the same boolean because the
/// audit found the copies had already drifted: the inclusion guards tested <c>== Listed</c> while the
/// exclusion guards tested <c>!= Private</c>, so an Unlisted or InviteOnly event was hidden from search and
/// the feed and then shown on public profiles. Two spellings of one rule is how that happens.</para>
///
/// <para><b>Never write a visibility comparison inline.</b> If a query needs to know whether an event may be
/// shown publicly, it composes <see cref="PubliclyVisible"/>. Anything else is a second source of truth.</para>
///
/// <para>Discoverability only. This says nothing about who may <i>act</i> on an event (D-269) or what an
/// event <i>supports</i> (D-266 M2 capabilities). A direct link to an Unlisted event still resolves — link
/// possession is that event's discovery mechanism; this predicate governs the surfaces that list events, not
/// the ones that fetch a known one.</para></summary>
public static class EventExposure
{
    /// <summary>EF-translatable. Compose into any query that feeds a public surface:
    /// <c>db.Events.Where(EventExposure.PubliclyVisible)</c>.</summary>
    public static Expression<Func<Event, bool>> PubliclyVisible =>
        e => e.Product == EventProduct.Public
          && e.Visibility == EventVisibility.Listed
          && e.DeletedAt == null;

    /// <summary>In-memory form, for a materialised event. Same rule; kept beside the expression so the two
    /// cannot drift.</summary>
    public static bool IsPubliclyVisible(Event e) =>
        e.Product == EventProduct.Public
        && e.Visibility == EventVisibility.Listed
        && e.DeletedAt == null;
}
