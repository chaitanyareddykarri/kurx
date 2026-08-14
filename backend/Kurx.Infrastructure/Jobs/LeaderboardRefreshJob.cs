using Hangfire;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

[DisableConcurrentExecution(timeoutInSeconds: 10)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 0)]   // next tick is soon; a failed run is not worth replaying
public class LeaderboardRefreshJob
{
    private readonly IGamificationService _gamificationService;
    private readonly ILogger<LeaderboardRefreshJob> _log;

    public LeaderboardRefreshJob(IGamificationService gamificationService, ILogger<LeaderboardRefreshJob> log)
    {
        _gamificationService = gamificationService;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _log.LogInformation("Starting leaderboard refresh background job...");
        await _gamificationService.RefreshLeaderboardsAsync(ct);
        _log.LogInformation("Leaderboard refresh completed.");
    }
}
