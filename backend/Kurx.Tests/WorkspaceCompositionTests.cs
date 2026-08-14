using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Events;

namespace Kurx.Tests;

/// <summary>D-266 M2 — the organiser workspace is composed from every platform layer, not generated from
/// the capability catalog. These are pure composition tests: the guarantee is that removing a capability
/// cannot remove a subsystem's tab, which is precisely the coupling that broke when `registration` stopped
/// being an event capability.</summary>
public class WorkspaceCompositionTests
{
    private sealed class Fake(params WorkspaceSurface[] surfaces) : IWorkspaceContributor
    {
        public Task<IReadOnlyList<WorkspaceSurface>> GetSurfacesAsync(Guid eventId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkspaceSurface>>(surfaces);
    }

    private static WorkspaceComposer Composer(params IWorkspaceContributor[] c) => new(c);
    private static readonly Guid Ev = Guid.NewGuid();

    [Fact]
    public async Task Removing_every_capability_leaves_subsystem_tabs_standing()
    {
        // The capability contributor returns nothing — the exact scenario that deleted the Registrations
        // tab before M2. Infrastructure and Registration surfaces must survive it untouched.
        var composed = await Composer(
            new InfrastructureWorkspaceContributor(),
            new Fake()).ComposeAsync(Ev);

        var ids = composed.Select(s => s.TabId).ToList();
        Assert.Contains("registrations", ids);
        Assert.Contains("check-in", ids);
        Assert.Contains("invitations", ids);
        Assert.Contains("schedule", ids);
    }

    [Fact]
    public async Task Registration_tab_is_owned_by_the_registration_subsystem_not_the_capability_engine()
    {
        var composed = await Composer(new InfrastructureWorkspaceContributor()).ComposeAsync(Ev);

        var reg = composed.Single(s => s.TabId == "registrations");
        Assert.Equal("registration", reg.Owner);
        Assert.NotEqual("capabilities", reg.Owner);
    }

    [Fact]
    public async Task Capability_surfaces_and_subsystem_surfaces_stay_independent()
    {
        var withCaps = await Composer(
            new InfrastructureWorkspaceContributor(),
            new Fake(new WorkspaceSurface("judging", "Judging", 100, "capabilities"))).ComposeAsync(Ev);
        var withoutCaps = await Composer(new InfrastructureWorkspaceContributor()).ComposeAsync(Ev);

        // Adding or removing a capability changes only capability-owned tabs.
        Assert.Contains(withCaps, s => s.TabId == "judging");
        Assert.DoesNotContain(withoutCaps, s => s.TabId == "judging");
        Assert.Equal(
            withoutCaps.Where(s => s.Owner != "capabilities").Select(s => s.TabId),
            withCaps.Where(s => s.Owner != "capabilities").Select(s => s.TabId));
    }

    [Fact]
    public async Task Composition_is_deterministic_regardless_of_contributor_order()
    {
        var a = new Fake(new WorkspaceSurface("alpha", "Alpha", 1, "x"));
        var b = new Fake(new WorkspaceSurface("beta", "Beta", 2, "y"));
        var infra = new InfrastructureWorkspaceContributor();

        var one = (await Composer(a, b, infra).ComposeAsync(Ev)).Select(s => s.TabId);
        var two = (await Composer(infra, b, a).ComposeAsync(Ev)).Select(s => s.TabId);

        Assert.Equal(one, two);
        // Order alone decides position; contributor registration order is irrelevant.
        Assert.Equal(["alpha", "beta"], one.Take(2));
    }

    [Fact]
    public async Task A_missing_dependency_fails_loudly_rather_than_vanishing()
    {
        var ex = await Assert.ThrowsAsync<WorkspaceComposer.WorkspaceConfigurationException>(() =>
            Composer(new Fake(new WorkspaceSurface("finance", "Finance", 60, "finance", DependsOn: ["tickets"])))
                .ComposeAsync(Ev));
        Assert.Contains("tickets", ex.Message);
        Assert.Contains("finance", ex.Message);
    }

    [Fact]
    public async Task Duplicate_order_values_fail_with_both_contributors_named()
    {
        var ex = await Assert.ThrowsAsync<WorkspaceComposer.WorkspaceConfigurationException>(() =>
            Composer(new Fake(new WorkspaceSurface("a", "A", 5, "one")),
                     new Fake(new WorkspaceSurface("b", "B", 5, "two"))).ComposeAsync(Ev));
        Assert.Contains("Order 5", ex.Message);
        Assert.Contains("one/a", ex.Message);
        Assert.Contains("two/b", ex.Message);
    }

    [Fact]
    public async Task Dependency_cycles_are_detected()
    {
        var ex = await Assert.ThrowsAsync<WorkspaceComposer.WorkspaceConfigurationException>(() =>
            Composer(new Fake(
                new WorkspaceSurface("x", "X", 1, "o", DependsOn: ["y"]),
                new WorkspaceSurface("y", "Y", 2, "o", DependsOn: ["x"]))).ComposeAsync(Ev));
        Assert.Contains("cycle", ex.Message);
    }

    [Fact]
    public async Task Two_layers_claiming_one_tab_id_fail_rather_than_one_silently_winning()
    {
        var ex = await Assert.ThrowsAsync<WorkspaceComposer.WorkspaceConfigurationException>(() =>
            Composer(new Fake(new WorkspaceSurface("audit", "Audit A", 5, "one")),
                     new InfrastructureWorkspaceContributor()).ComposeAsync(Ev));
        Assert.Contains("audit", ex.Message);
    }

    [Fact]
    public async Task The_real_contributor_set_composes_cleanly()
    {
        // Guards the startup gate: the shipped contributors must satisfy unique Order and resolvable deps.
        var composed = await Composer(new InfrastructureWorkspaceContributor()).ComposeAsync(Ev);
        Assert.Equal(composed.Select(s => s.Order).Distinct().Count(), composed.Count);
    }
}
