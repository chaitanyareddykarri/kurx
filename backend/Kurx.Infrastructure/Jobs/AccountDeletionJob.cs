using Hangfire;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

// Runs daily. Anonymises accounts whose 30-day grace window has elapsed (D-263).
//
// Without this the deletion feature is a scheduled date nothing acts on — a promise to the user, and to
// the DPDP obligation behind it, that the platform never keeps. Daily rather than hourly because the
// grace window is measured in days: a few hours of extra latency on a 30-day clock is invisible, and a
// slower cadence means fewer chances to half-apply a batch.
//
// Idempotent: a row is selected only while AnonymizedAt is null, so a re-run finds nothing.
[DisableConcurrentExecution(timeoutInSeconds: 30)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 0)]   // tomorrow is soon enough; a failed run is not worth replaying
public class AccountDeletionJob(IAccountService accounts, ILogger<AccountDeletionJob> log)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var anonymized = await accounts.RunScheduledDeletionsAsync(ct);
        if (anonymized > 0) log.LogInformation("Anonymised {Count} account(s) past their grace window", anonymized);
    }
}
