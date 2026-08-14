using Hangfire;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

/// <summary>V3 §17.1 reconciliation (authoritative from Phase 9): periodically proves each pool's authoritative
/// <c>Consumed</c> equals the count of active admissions drawing on it, and alerts on drift. Now that pools are
/// the oversell authority, this is the continuous proof that the conditional-decrement contract and the admission
/// records never disagree.</summary>
[DisableConcurrentExecution(timeoutInSeconds: 60)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 3)]   // a lost run costs 24h, so retry it
public class InventoryReconciliationJob(IInventoryService inventory, ILogger<InventoryReconciliationJob> log)
{
    /// <summary>The <c>type</c> dimension on every metric this job emits.</summary>
    public const string ReconciliationType = "inventory";

    public async Task RunAsync(CancellationToken ct)
    {
        IReadOnlyList<InventoryDrift> drift;
        try
        {
            drift = await inventory.ReconcileAsync(null, ct);
        }
        catch (Exception ex)
        {
            // DB-9: see WalletReconciliationJob — a detector that threw proves nothing, and must not read as
            // a clean run.
            ReconciliationTelemetry.RecordFailure(ReconciliationType);
            log.LogError(ex, "Inventory reconciliation failed to complete; pool/admission agreement is "
                          + "UNVERIFIED for this run, not proven.");
            throw;
        }

        ReconciliationTelemetry.Record(ReconciliationType, drift.Count);
        if (drift.Count == 0) return;
        log.LogWarning("Inventory drift detected in {Count} pool(s); repairing: {Detail}", drift.Count,
            string.Join(", ", drift.Select(d => $"pool {d.PoolId} consumed={d.Consumed} active_admissions={d.ActiveAdmissions}")));

        // Self-heal (review P4): set each drifting pool's Consumed to its active-admission ground truth.
        var repaired = await inventory.RepairAsync(null, ct);
        var residual = await inventory.ReconcileAsync(null, ct);
        if (residual.Count == 0)
            log.LogInformation("Inventory repaired: {Repaired} pool(s) reconciled to their active admissions.", repaired);
        else
        {
            // DB-9: an unconverged self-heal is operationally a failed reconciliation, not merely drift. Drift
            // that repairs itself is routine; drift the repair could not remove means the ground truth and the
            // pool disagree for a reason this job does not understand, and that needs a human rather than
            // another automatic pass tomorrow.
            ReconciliationTelemetry.RecordFailure(ReconciliationType);
            log.LogError("Inventory still drifting after repairing {Repaired} pool(s): {Count} pool(s) remain.", repaired, residual.Count);
        }
    }
}
