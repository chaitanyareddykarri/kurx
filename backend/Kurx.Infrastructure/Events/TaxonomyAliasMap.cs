namespace Kurx.Infrastructure.Events;

/// <summary>D-266 M3 Step 6 Phase 1 — the explicit legacy→canonical taxonomy mapping.
///
/// <para><b>Nothing here is inferred from names.</b> Every entry is a decision recorded in D11 (the final
/// taxonomy audit) or in a direct product ruling. Two types with similar names may map differently — Science
/// <i>Fair</i> is judged and Science <i>Exhibition</i> is a showcase, so they stay apart — and that is
/// exactly why name-similarity is not a mapping rule.</para>
///
/// <para>This file is the mapping artifact only. It performs no data change: Phase 2 verifies every existing
/// event against it, Phase 3 makes it a runtime alias layer, and only Phase 4 migrates rows.</para></summary>
public static class TaxonomyAliasMap
{
    public enum MappingKind
    {
        /// <summary>Survives D11 unchanged.</summary>
        Unchanged,
        /// <summary>Same product, new globally-understandable name (D11 §2).</summary>
        Rename,
        /// <summary>A true duplicate folded into another type — identical across lifecycle, workflow,
        /// permissions, registration, payment, modules and post-event behaviour (D11 §3).</summary>
        Merge,
        /// <summary>Collapsed into a broader type where the distinction becomes an attribute (D11 §4).</summary>
        AttributeConversion,
        /// <summary>Withdrawn as a product. Existing events keep resolving through the alias; no new events
        /// may be created with it (D11 §1).</summary>
        Removed,
    }

    /// <param name="Canonical">The canonical type name, or null when the type is Removed with no successor.</param>
    /// <param name="Attribute">Attribute name and value the conversion sets, e.g. ("sport", "Cricket").</param>
    public record Mapping(string Legacy, string? Canonical, MappingKind Kind, string Reason,
        (string Name, string Value)? Attribute = null);

    private static Mapping M(string legacy, string canonical, MappingKind kind, string reason) =>
        new(legacy, canonical, kind, reason);

    private static Mapping Attr(string legacy, string canonical, string name, string value, string reason) =>
        new(legacy, canonical, MappingKind.AttributeConversion, reason, (name, value));

    /// <summary>Every legacy type that changes. Types absent from this list are <see cref="MappingKind.Unchanged"/>
    /// — asserted exhaustively by the Phase 2 verification test, so a new legacy type cannot slip through
    /// unmapped by being forgotten here.</summary>
    public static readonly Mapping[] Mappings =
    [
        // ── D11 §2 · Renames — region-locked names replaced with globally understandable ones ──────
        M("Placement Drive", "Campus Recruitment", MappingKind.Rename, "India/South Asia only"),
        M("Internship Drive", "Internship Recruitment", MappingKind.Rename, "'drive' is regional"),
        M("Industrial Visit", "Site Visit", MappingKind.Rename, "elsewhere a site/field visit"),
        M("Freshers Party", "Welcome Event", MappingKind.Rename, "India/UK phrasing"),
        M("Farewell Party", "Farewell Event", MappingKind.Rename, "'party' narrows a formal send-off"),
        M("Annual Day", "Annual Celebration", MappingKind.Rename, "India school convention"),
        M("College Fest", "Campus Fest", MappingKind.Rename, "'college' differs by country"),
        M("Blood Donation Camp", "Blood Drive", MappingKind.Rename, "'camp' is regional"),
        M("Health Camp", "Health Screening", MappingKind.Rename, "regional phrasing"),
        M("Tree Plantation Drive", "Tree Planting", MappingKind.Rename, "regional phrasing"),
        M("Industry Interaction Session", "Industry Talk", MappingKind.Rename, "awkward regionalism"),

        // ── D11 §3 · Merges — true duplicates only ────────────────────────────────────────────────
        M("Job Fair", "Career Fair", MappingKind.Merge, "identical: exhibitors, slots, unpaid, no certificates"),
        M("Industry Expo", "Trade Show", MappingKind.Merge, "identical: exhibitors, booths, ticketed, sponsors"),
        M("Community Service Event", "Volunteer Event", MappingKind.Merge, "identical modules, registration, post-event"),
        M("Society Event", "Club Event", MappingKind.Merge, "a society IS a club; identical in every dimension"),
        M("Student Chapter Event", "Club Event", MappingKind.Merge, "a chapter IS a club; identical in every dimension"),
        M("Marathon", "Running Event", MappingKind.Merge, "identical modules; distance is an attribute"),
        M("Bachelor Party", "Pre-Wedding Celebration", MappingKind.Merge, "gendered US term; one neutral product"),
        M("Bachelorette Party", "Pre-Wedding Celebration", MappingKind.Merge, "gendered US term; one neutral product"),

        // ── D11 §4 · Attribute conversions — the distinction becomes a field, not a type ───────────
        Attr("Cricket Tournament", "Sports Tournament", "sport", "Cricket", "sport is an attribute"),
        Attr("Football Tournament", "Sports Tournament", "sport", "Football", "sport is an attribute"),
        Attr("Basketball Tournament", "Sports Tournament", "sport", "Basketball", "sport is an attribute"),
        Attr("Volleyball Tournament", "Sports Tournament", "sport", "Volleyball", "sport is an attribute"),
        Attr("Badminton Tournament", "Sports Tournament", "sport", "Badminton", "sport is an attribute"),
        Attr("Chess Tournament", "Sports Tournament", "sport", "Chess", "sport is an attribute"),
        Attr("Athletics Meet", "Sports Tournament", "sport", "Athletics", "sport is an attribute"),
        Attr("Public Sports Tournament", "Sports Tournament", "sport", "General", "audience is not a type"),

        // Submission-based contests. Kept apart from Performance Competition because submissions and staged
        // judging are different workflows (D11 §4 note) — format is the type, discipline the attribute.
        Attr("Art Competition", "Creative Competition", "discipline", "Art", "submission-based"),
        Attr("Photography Competition", "Creative Competition", "discipline", "Photography", "submission-based"),
        Attr("Film Competition", "Creative Competition", "discipline", "Film", "submission-based"),
        Attr("Writing Competition", "Creative Competition", "discipline", "Writing", "submission-based"),
        Attr("Design Competition", "Creative Competition", "discipline", "Design", "submission-based"),

        Attr("Music Competition", "Performance Competition", "discipline", "Music", "live/staged"),
        Attr("Dance Competition", "Performance Competition", "discipline", "Dance", "live/staged"),
        Attr("Talent Competition", "Performance Competition", "discipline", "Talent", "live/staged"),
        Attr("Fashion Competition", "Performance Competition", "discipline", "Fashion", "live/staged"),

        Attr("Art Exhibition", "Exhibition", "subject", "Art", "subject is an attribute"),
        Attr("Photography Exhibition", "Exhibition", "subject", "Photography", "subject is an attribute"),

        Attr("Destination Wedding", "Wedding", "venue_type", "Destination", "a destination wedding is a wedding with a location"),

        // ── D11 §1 · Removed — no successor. Existing events resolve through the alias; creation blocked ──
        M("Pooja Ceremony", "Private Ceremony", MappingKind.Removed, "religion-specific; neutral cover retained"),
        M("Religious Ceremony", "Private Ceremony", MappingKind.Removed, "religion-framed"),
        M("Prayer Meeting", "Private Ceremony", MappingKind.Removed, "religion-framed"),
        M("Festival Celebration", "Private Ceremony", MappingKind.Removed, "religion/culture-framed"),
        M("Religious Gathering", "Community Meetup", MappingKind.Removed,
          "last religion-framed type; Public product, so its successor must also be Public — mapping it to "
          + "Private Ceremony would have flipped the product, which taxonomy migration must never do"),
        M("Traditional Family Function", "Private Ceremony", MappingKind.Removed, "culture-framed"),
        M("Coming-of-Age Ceremony", "Private Ceremony", MappingKind.Removed, "culture-specific rite"),
        M("Memorial Service", "Private Ceremony", MappingKind.Removed, "removed per ruling; Private Ceremony suffices"),
        M("Gender Reveal Party", "Private Celebration", MappingKind.Removed, "US-specific, culturally contested"),
        M("Sleepover Party", "Private Celebration", MappingKind.Removed, "frequently involves minors; not a Kurx product"),
        M("Investor Meet", "Networking Event", MappingKind.Removed, "removed per ruling (D-266 Q3)"),
    ];

    private static readonly Dictionary<string, Mapping> ByLegacy =
        Mappings.ToDictionary(m => m.Legacy, StringComparer.OrdinalIgnoreCase);

    /// <summary>Canonical name for a legacy type. A type with no entry is Unchanged and returns itself, so
    /// resolution is total: no event can fail to resolve a type.</summary>
    public static string Resolve(string legacyTypeName)
        => ByLegacy.TryGetValue(legacyTypeName, out var m) ? m.Canonical ?? legacyTypeName : legacyTypeName;

    public static bool TryGet(string legacyTypeName, out Mapping mapping)
        => ByLegacy.TryGetValue(legacyTypeName, out mapping!);

    /// <summary>Types that may no longer be chosen for a NEW event. Existing events keep resolving.</summary>
    public static IReadOnlySet<string> RetiredForCreation { get; } =
        Mappings.Where(m => m.Kind is MappingKind.Removed or MappingKind.Merge or MappingKind.AttributeConversion
                                   or MappingKind.Rename)
                .Select(m => m.Legacy).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
