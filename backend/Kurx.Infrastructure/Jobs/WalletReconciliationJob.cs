using Hangfire;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

/// <summary>Continuous proof that each org's cached wallet still equals the sum of its append-only ledger
/// (D-103, D-240) — the money-side sibling of <see cref="InventoryReconciliationJob"/> and
/// <see cref="RegistrationReconciliationJob"/>.
///
/// <para><b>Why this exists.</b> Inventory and registrations each had a job proving their invariant; money
/// did not. That asymmetry is precisely how the wallet lost-update bug (D-240) could have run in production
/// unnoticed: the ledger stayed correct throughout while the cached balance silently diverged, and no
/// request ever failed. A measured six-payment run credited one. The write paths are fixed; this is the
/// detector that would have caught it, and will catch the next one.</para>
///
/// <para>Unlike the inventory job this does <b>not</b> self-heal by default. An inventory pool can be reset
/// from its active admissions with no consequence beyond seat counts; a wallet balance is money, and
/// silently rewriting it would destroy the evidence an operator needs to work out what went wrong. Drift is
/// alerted; <see cref="IWalletService.RepairAsync"/> exists for a deliberate, human-triggered repair.</para></summary>
[DisableConcurrentExecution(timeoutInSeconds: 60)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 3)]   // a lost run costs 24h, so retry it
public class WalletReconciliationJob(IWalletService wallets, ILogger<WalletReconciliationJob> log)
{
    /// <summary>The <c>type</c> dimension on every metric this job emits.</summary>
    public const string ReconciliationType = "wallet";

    public async Task RunAsync(CancellationToken ct)
    {
        IReadOnlyList<WalletDrift> drift;
        try
        {
            drift = await wallets.ReconcileAsync(null, ct);
        }
        catch (Exception ex)
        {
            // DB-9: an invariant that could not be CHECKED is not an invariant that HELD. Before this, a
            // throwing reconciliation left no metric at all, and "no drift metric" is indistinguishable from
            // "no drift" on a dashboard — the failure mode that lets a money bug run unnoticed for exactly as
            // long as the detector is broken. Recorded, then rethrown so Hangfire still retries and still
            // records the job as failed.
            ReconciliationTelemetry.RecordFailure(ReconciliationType);
            log.LogError(ex, "Wallet reconciliation failed to complete; wallet/ledger agreement is UNVERIFIED "
                          + "for this run, not proven.");
            throw;
        }

        ReconciliationTelemetry.Record(ReconciliationType, drift.Count);
        if (drift.Count == 0) return;

        // Logged at Error, not Warning: a wallet disagreeing with its ledger is a money-correctness
        // failure, and it should page rather than sit in a dashboard nobody reads. DB-9 is what makes the
        // page real — the metric above is what CloudWatch alarms on; this line is the detail an operator
        // reads once paged. Org ids and paise deltas only: no bank details, no payment references, nothing
        // that would turn a log line into a disclosure.
        log.LogError(
            "Wallet drift detected in {Count} org(s) — cached balance disagrees with the ledger: {Detail}. " +
            "The ledger is authoritative (D-103); investigate before running IWalletService.RepairAsync.",
            drift.Count,
            string.Join(", ", drift.Select(d =>
                $"org {d.OrgId} cached={d.CachedPaise} ledger={d.LedgerPaise} delta={d.DeltaPaise}")));
    }
}
