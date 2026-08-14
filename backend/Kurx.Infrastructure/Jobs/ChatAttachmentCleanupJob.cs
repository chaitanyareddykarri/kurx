using Hangfire;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

// Runs hourly. Deletes storage objects for attachments whose message was deleted, and for uploads
// that were confirmed but never claimed by a message (D-110).
//
// Orphans are unavoidable: confirm happens before send, so a composer that is closed, a send that
// fails, or an app that is killed all leave a stored object with no message. Without this sweep they
// accumulate forever.
//
// Idempotent — rows are removed only after their object is gone, so a re-run finds nothing.
[DisableConcurrentExecution(timeoutInSeconds: 10)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 0)]   // next tick is soon; a failed run is not worth replaying
public class ChatAttachmentCleanupJob
{
    private readonly IChatService _chat;
    private readonly ILogger<ChatAttachmentCleanupJob> _log;

    public ChatAttachmentCleanupJob(IChatService chat, ILogger<ChatAttachmentCleanupJob> log)
    {
        _chat = chat;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await _chat.CleanupAttachmentsAsync(ct);
        _log.LogDebug("Chat attachment cleanup sweep complete");
    }
}
