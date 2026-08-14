using Hangfire;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

// Runs hourly. Deletes storage objects for media whose post was deleted, and for uploads that were
// confirmed but never claimed by a post (D-262) — the same sweep, for the same reason, as
// ChatAttachmentCleanupJob.
//
// Orphans are unavoidable: confirm happens before create, so a composer that is closed, a create that
// fails, or an app that is killed all leave a stored object with no post. Without this sweep they
// accumulate forever.
//
// Idempotent — rows are removed only after their object is gone, so a re-run finds nothing.
[DisableConcurrentExecution(timeoutInSeconds: 10)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 0)]   // next tick is soon; a failed run is not worth replaying
public class PostMediaCleanupJob(IPostService posts, ILogger<PostMediaCleanupJob> log)
{
    public async Task RunAsync(CancellationToken ct)
    {
        await posts.CleanupMediaAsync(ct);
        log.LogDebug("Post media cleanup sweep complete");
    }
}
