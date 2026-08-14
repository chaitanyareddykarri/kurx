namespace Kurx.Domain.Entities;

/// <summary>V3 §2 — the closed 20-Kind catalog (Event Architecture V3, D-131). A Kind is a named,
/// analysable bundle of capability *defaults*; it never gates engine behaviour (V3 §11.3, D-132).
/// Seeded rather than an enum so the registry can carry aliases and metadata. <see cref="Slug"/> is the
/// stable identifier stamped onto <c>Event.KindSlug</c>.</summary>
public class EventKind
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = null!;       // stable id, e.g. "hackathon"
    public string Name { get; set; } = null!;       // display, e.g. "Hackathon"
    public string GroupSlug { get; set; } = null!;  // one of the 7 V3 groups, e.g. "competitive"
    public string GroupName { get; set; } = null!;  // display group, e.g. "Competitive"
    public int Sort { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>V3 §2 — a retired event-type name survives as a searchable alias on the Kind registry; it is
/// never stored on an event (the event carries <c>KindSlug</c>). The 145 legacy taxonomy Type names are
/// seeded here mapped to their Kind. Data-driven: correcting an alias's <see cref="KindSlug"/> is a row
/// edit, not a code change — the seeder only inserts missing rows and never overwrites an existing one.</summary>
public class KindAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Alias { get; set; } = null!;          // display, e.g. "Ideathon"
    public string NormalizedAlias { get; set; } = "";   // the legacy Type slug, e.g. "ideathon" (unique)
    public string KindSlug { get; set; } = null!;        // → EventKind.Slug
}
