using Hangfire;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

/// <summary>Keeps the V3 §15 discovery index's asynchronous ranking signals current (Phase 16): recent-view velocity
/// over the window, conversion counts, and the RECURRING series primary occurrence (which drifts as time passes). The
/// document text itself is maintained by the outbox-fed projector on event writes; this job only refreshes the
/// time-varying signals, so the ranking stays fresh without a synchronous write on any read path.</summary>
[DisableConcurrentExecution(timeoutInSeconds: 10)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 0)]   // next tick is soon; a failed run is not worth replaying
public class SearchIndexRefreshJob(ISearchIndexService searchIndex, ILogger<SearchIndexRefreshJob> log)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var n = await searchIndex.RefreshSignalsAsync(ct);
        if (n > 0) log.LogInformation("Search index signals refreshed for {Count} documents", n);
    }
}
