namespace Kurx.Application.Abstractions;

/// <summary>D-266 M2 — one organiser workspace surface, contributed by the subsystem that owns it.
///
/// <para>Before M2 every tab came from <c>CapDef.WorkspaceTab</c>, so a subsystem had to pretend to be an
/// event capability just to show a page. Reducing the capability engine to D12's 30 event behaviours
/// removed the Registrations tab as a side effect — which is what proved the coupling wrong. Capabilities
/// answer "does this archetype support this behaviour"; they do not answer "what pages exist".</para></summary>
/// <param name="TabId">Stable identifier, e.g. <c>registrations</c>. Routes and ordering key on this.</param>
/// <param name="Order">Ascending. Ties break on <paramref name="TabId"/> so composition is deterministic.</param>
/// <param name="DependsOn">Tab ids that must also be present; a surface whose dependency is absent is dropped.</param>
/// <summary>One organiser-workspace surface a subsystem offers.
///
/// <para><b>There is deliberately no route (D-310).</b> A <c>Route</c> field existed, was built by every
/// contributor, and was read by nothing — it never even reached the wire. It could not have been correct:
/// web routes event management at <c>/host/events/{id}/{tab}</c> and Flutter at
/// <c>/events/{id}/manage/{tab}</c>, and one server-side string cannot be right for both. The server says
/// **which surfaces exist**, identified by <see cref="TabId"/>; resolving that to a URL is the client's
/// concern.</para></summary>
public record WorkspaceSurface(
    string TabId,
    string DisplayName,
    int Order,
    string Owner,
    IReadOnlyList<string>? DependsOn = null);

/// <summary>A platform layer that contributes organiser workspace surfaces. Implement once per subsystem —
/// Infrastructure, Registration, Ticketing, Invitations, Scheduling, Finance — and register it in DI. The
/// composer asks every contributor; no layer is named in a hardcoded list anywhere.</summary>
public interface IWorkspaceContributor
{
    /// <summary>Which surfaces this subsystem offers for this event. Returning nothing is the normal way to
    /// say "not applicable here" — visibility is the contributor's own rule to apply, because only it knows
    /// what makes its pages relevant (a Finance tab needs money to be possible; a Ticketing tab does not
    /// exist for an event that cannot sell).</summary>
    Task<IReadOnlyList<WorkspaceSurface>> GetSurfacesAsync(Guid eventId, CancellationToken ct = default);
}
