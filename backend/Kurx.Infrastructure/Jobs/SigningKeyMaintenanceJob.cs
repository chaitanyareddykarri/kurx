using Hangfire;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

/// <summary>Retires signing keys whose grace period has elapsed (AM10, D-099).
///
/// <para>Deliberately only <b>retires</b> — it does not rotate. Rotation is an operator action
/// (or an incident response), because a surprise rotation on a schedule nobody remembers setting is
/// how a key ends up rotating during a deploy freeze. Retirement, by contrast, is pure cleanup:
/// it only touches keys that already stopped signing and can no longer appear in any live token,
/// so running it unattended is safe.</para>
///
/// Idempotent: a run with nothing to retire is a no-op.</summary>
[DisableConcurrentExecution(timeoutInSeconds: 60)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 3)]   // a lost run costs 24h, so retry it
public class SigningKeyMaintenanceJob(ISigningKeyService keys, ILogger<SigningKeyMaintenanceJob> log)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var retired = await keys.RetireExpiredAsync(ct);
        if (retired > 0) log.LogInformation("Signing-key maintenance retired {Count} key(s)", retired);
    }
}
