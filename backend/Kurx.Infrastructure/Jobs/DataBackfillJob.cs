using Hangfire;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

/// <summary>The V3 strangler-window convergence passes (Phases 1–16), moved off the boot path (D-250).
///
/// <para>Every pass here is driven by a user-data table and is idempotent — it selects only the rows that
/// still lack their projection and writes those. Running inline at startup therefore made boot time scale
/// with table size (<see cref="IEventRegistrationService.BackfillAsync"/> alone anti-joins the whole orders
/// table and then projects order-by-order in its own transaction each), and a rolling deploy had every
/// replica doing that same work over the same rows at once.</para>
///
/// <para><see cref="DisableConcurrentExecutionAttribute"/> is what makes the replica overlap safe: Hangfire
/// takes a distributed lock in its Postgres storage, so a second runner waits instead of duplicating the
/// scan. The timeout is generous because the first run after a large migration is the slow one.</para></summary>
[DisableConcurrentExecution(timeoutInSeconds: 1800)]
public class DataBackfillJob(
    IKindService kinds,
    ICapabilityService capabilities,
    IParticipantService participants,
    IInventoryService inventory,
    IEventRegistrationService registrations,
    ITeamService teams,
    ISearchIndexService searchIndex,
    ILogger<DataBackfillJob> log)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        // Order is load-bearing and matches what the boot path ran: kinds before capabilities (the
        // Kind×Capability defaults key on the kind slug), pools before the registration chain (admissions
        // link to a pool).
        var kindCount = await kinds.BackfillEventKindsAsync(ct);
        var capabilityCount = await capabilities.BackfillEventCapabilitiesAsync(ct);
        var participantCount = await participants.BackfillFromAssignmentsAsync(ct);
        var poolCount = await inventory.BackfillPoolsAsync(ct);
        var registrationCount = await registrations.BackfillAsync(ct);
        var teamCount = await teams.BackfillFromGroupsAsync(ct);
        var searchCount = await searchIndex.BackfillAsync(ct);
        await searchIndex.RefreshSignalsAsync(ct);

        var converged = kindCount + capabilityCount + participantCount + poolCount
            + registrationCount + teamCount + searchCount;
        if (converged == 0) return;   // steady state — the common case, not worth a log line

        log.LogInformation(
            "Backfill converged {Total} row(s): {Kinds} kind, {Capabilities} capability set, {Participants} participant, "
            + "{Pools} pool, {Registrations} order projection, {Teams} team, {SearchDocs} search document.",
            converged, kindCount, capabilityCount, participantCount, poolCount, registrationCount, teamCount, searchCount);
    }
}
