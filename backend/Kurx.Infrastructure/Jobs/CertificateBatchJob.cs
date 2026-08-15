using Hangfire;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

// Renders an approved certificate batch (D-355, Phase 7).
//
// Enqueued once, at approval. Rendering hundreds of PDFs cannot happen inside a request: the organiser
// would sit on a spinner for minutes, and a dropped connection would leave the run half-finished with
// nothing tracking it.
//
// Idempotent by construction: the service skips any row that already has a certificate, and the database
// enforces one certificate per (batch, recipient). That is what makes retrying safe rather than
// duplicative — a retry finishes the run instead of issuing everything twice.
[DisableConcurrentExecution(timeoutInSeconds: 60)]  // one worker per batch, across every replica
[AutomaticRetry(Attempts = 3)]   // worth replaying: a resumable run costs only the rows in flight
public class CertificateBatchJob(ICertificateBatchService batches, ILogger<CertificateBatchJob> log)
{
    public async Task RunAsync(Guid batchId, CancellationToken ct)
    {
        var result = await batches.RunAsync(batchId, ct);

        if (result.Failed > 0)
            log.LogWarning("Certificate batch {BatchId} finished with {Failed} failed row(s)",
                batchId, result.Failed);
    }
}
