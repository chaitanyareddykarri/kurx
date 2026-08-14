using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>A behavioural archetype (D-266 §D12) — the unit that actually decides how an event
/// behaves.
///
/// <para><b>Why this exists alongside the taxonomy.</b> <see cref="EventCategory"/> answers *what kind
/// of event is this* for a human picking from a list (104 types). An archetype answers *how does the
/// platform behave* (14 groups). Two types share an archetype only when their modules, workflow,
/// organiser experience and post-event behaviour are identical — so `Hackathon` and `Olympiad` are one
/// archetype while `Workshop` and `Conference` are not, however similar the words look.</para>
///
/// <para>This supersedes the 21-entry <see cref="EventKind"/> registry, whose capability defaults were
/// reached through the name-matched <c>KindAlias</c> chain that D-188 itself flags as fragile. Kind is
/// archived rather than dropped: existing events reference it.</para></summary>
public class EventArchetype
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Stable identifier, e.g. <c>competitive</c>. Never changes once events reference it.</summary>
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = "";
    /// <summary>Which product this archetype belongs to. Every archetype is wholly Public or wholly
    /// Private — an archetype spanning both would reintroduce the "Both" classification D-266 removed.</summary>
    public EventProduct Product { get; set; } = EventProduct.Public;
    public int Sort { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>D-266 M5 — D12 §6's <i>Representation: Required</i> column (A7 Recruitment · A12 Festival ·
    /// A13 Ceremonial). These archetypes cannot be run by an individual in a personal capacity: a campus
    /// recruitment drive, a college fest and an award ceremony are all acts of an institution, and one
    /// filed as self-represented is misfiled rather than merely unusual.
    ///
    /// <para>Stored on the archetype rather than branched on in code so the matrix stays data — D12's
    /// standing rule that no validation is hardcoded anywhere.</para></summary>
    public bool RequiresRepresentation { get; set; }

    /// <summary>D-266 M7 — D12 §6's <i>Review: Required + financial</i> cell (A11 Fundraising alone). A
    /// fundraiser solicits money for a cause, so someone in FinanceOps confirms the money path before it
    /// goes live; every other archetype passes ordinary review only.
    ///
    /// <para>Keyed on the archetype rather than on "is it paid", because the risk is <b>soliciting on
    /// behalf of a cause</b>, not taking payment — a ticketed concert is A8 and needs no financial review.
    /// Stored as data for the same reason as <see cref="RequiresRepresentation"/>.</para></summary>
    public bool RequiresFinancialReview { get; set; }
}
