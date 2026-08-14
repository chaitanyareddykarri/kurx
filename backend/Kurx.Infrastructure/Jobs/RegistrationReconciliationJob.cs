using Hangfire;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

/// <summary>V3 §17.1 reconciliation for the Phase-8 registration shadow: periodically proves the shadow matches
/// the authority per event — registrations == orders, active admissions == active tickets, and every live
/// account-holder admission carries a credential — then self-heals any drift by re-projecting the affected orders
/// (review P3). The dual-write shadow is only trustworthy if continuously verified and repaired against the
/// authoritative Order/Ticket before the Phase-9 cut-over.</summary>
[DisableConcurrentExecution(timeoutInSeconds: 60)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 3)]   // a lost run costs 24h, so retry it
public class RegistrationReconciliationJob(IEventRegistrationService registration, ILogger<RegistrationReconciliationJob> log)
{
    /// <summary>The <c>type</c> dimension on every metric this job emits.</summary>
    public const string ReconciliationType = "registration";

    public async Task RunAsync(CancellationToken ct)
    {
        IReadOnlyList<RegistrationDrift> drift;
        try
        {
            drift = await registration.ReconcileAsync(null, ct);
        }
        catch (Exception ex)
        {
            // DB-9: see WalletReconciliationJob — a detector that threw proves nothing, and must not read as
            // a clean run.
            ReconciliationTelemetry.RecordFailure(ReconciliationType);
            log.LogError(ex, "Registration reconciliation failed to complete; shadow/authority agreement is "
                          + "UNVERIFIED for this run, not proven.");
            throw;
        }

        ReconciliationTelemetry.Record(ReconciliationType, drift.Count);
        if (drift.Count == 0) return;
        log.LogWarning("Registration shadow drift in {Count} event(s); repairing: {Detail}", drift.Count,
            string.Join(", ", drift.Select(d => $"event {d.EventId} orders={d.Orders}/regs={d.Registrations} tickets={d.ActiveTickets}/adms={d.ActiveAdmissions} missingCred={d.AdmissionsMissingCredential}")));

        var repaired = await registration.RepairAsync(null, ct);
        var residual = await registration.ReconcileAsync(null, ct);
        if (residual.Count == 0)
            log.LogInformation("Registration shadow repaired: re-projected {Repaired} order(s), no residual drift.", repaired);
        else
        {
            // DB-9: an unconverged self-heal is a failed reconciliation, not routine drift — see the same
            // branch in InventoryReconciliationJob.
            ReconciliationTelemetry.RecordFailure(ReconciliationType);
            log.LogError("Registration shadow still drifting after repairing {Repaired} order(s): {Count} event(s) remain.", repaired, residual.Count);
        }
    }
}
