namespace Kurx.Domain.Entities;

/// <summary>D-266 M8 — the create/edit wizard's autosave.
///
/// <para>One snapshot per (event, user). Per user rather than per event because two managers editing the
/// same draft are each mid-thought: merging their in-progress forms would produce something neither of them
/// typed, and last-write-wins would silently discard one person's work.</para>
///
/// <para><b>This is not the event.</b> It is unvalidated client state — whatever the organiser has typed so
/// far, including a half-filled step that would fail validation. The event itself is written only when a
/// step is submitted; nothing here is ever read by the publish path, the policy engine or any projection.
/// Storing it as opaque JSON is deliberate: giving it columns would create a second, weaker definition of
/// an event that drifts from the real one every time a field is added.</para>
///
/// <para><b>Content is preserved, not bytes.</b> The column is <c>jsonb</c>, which reparses on write:
/// whitespace and key order are normalised and duplicate keys collapse. That is the right trade — a form
/// restore parses the JSON anyway, and jsonb guarantees at the database level that what was stored is
/// valid — but a caller must not depend on getting back the exact string it sent.</para></summary>
public class EventDraftSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }

    /// <summary>Whose draft this is. Part of the unique key — see the class remarks.</summary>
    public Guid UserId { get; set; }

    /// <summary>The wizard's in-progress form. Never parsed server-side; see the class remarks on jsonb
    /// normalisation.</summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>Which step the organiser was on, so resuming lands where they left off rather than at the
    /// beginning — the difference between an autosave that helps and one that only preserves data.</summary>
    public string? StepKey { get; set; }

    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}
