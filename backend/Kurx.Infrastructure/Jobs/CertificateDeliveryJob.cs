using Hangfire;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

// Drains queued certificate emails (D-355, Phase 8).
//
// Recurring rather than enqueued-per-send: the queue is the record of intent, and a job that simply drains
// whatever is pending recovers from a crash, a provider outage, or a process restart without anyone having
// to re-press a button. A per-send job would lose exactly the sends that were in flight when it died.
//
// Idempotent: a row moves out of Pending only once it has been accepted by the provider or given up on, so
// a re-run picks up where the last one stopped rather than re-sending.
[DisableConcurrentExecution(timeoutInSeconds: 30)]  // one drainer at a time across every replica
[AutomaticRetry(Attempts = 0)]   // the next tick is soon; a failed run is not worth replaying
public class CertificateDeliveryJob(
    ICertificateDeliveryService deliveries, ILogger<CertificateDeliveryJob> log)
{
    /// <summary>How many are attempted per tick. Bounded so one enormous run cannot monopolise the worker
    /// or the email provider's rate limit; the rest are picked up on the next tick.</summary>
    private const int BatchSize = 100;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var result = await deliveries.DispatchPendingAsync(BatchSize, ct);

        if (result.Sent > 0 || result.Failed > 0)
            log.LogInformation("Certificate delivery: {Sent} sent, {Failed} failed, {Skipped} skipped",
                result.Sent, result.Failed, result.Skipped);
    }
}
