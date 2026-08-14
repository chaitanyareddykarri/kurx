using Hangfire;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

// Runs hourly. Locks the chat room of any event that ended more than 7 days ago or has been archived,
// posting a system message and broadcasting the status change (ChatService.LockExpiredRoomsAsync).
//
// Cancellation and archive lock a room immediately from EventService.TransitionAsync; this sweep is the
// catch-all for events that simply ended and were never explicitly closed. Idempotent — it only selects
// rooms still in Active, so a re-run locks nothing twice.
[DisableConcurrentExecution(timeoutInSeconds: 10)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 0)]   // next tick is soon; a failed run is not worth replaying
public class LockExpiredChatRoomsJob
{
    private readonly IChatService _chat;
    private readonly ILogger<LockExpiredChatRoomsJob> _log;

    public LockExpiredChatRoomsJob(IChatService chat, ILogger<LockExpiredChatRoomsJob> log)
    {
        _chat = chat;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await _chat.LockExpiredRoomsAsync(ct);
        _log.LogDebug("Expired chat room sweep complete");
    }
}
