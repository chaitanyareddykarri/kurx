using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Kurx.Infrastructure.Auth;

/// <summary>Step-up authentication (AM6, ADR-A10). Sensitive actions require a recent device signature on
/// top of a valid session, so possession of an access token is not by itself authority to do damage.
///
/// The grant needs no new table: an <c>auth_challenges</c> row with purpose StepUp that reached Approved
/// within the window <i>is</i> the grant. Reusing the challenge record keeps one audit trail for "who
/// proved what, with which device, when" instead of a second parallel one that could disagree.</summary>
public class StepUpService(KurxDbContext db, IChallengeService challenges, IConfiguration config) : IStepUpService
{
    private TimeSpan Window => TimeSpan.FromSeconds(
        int.TryParse(config["AUTH_STEPUP_TTL_SECONDS"], out var v) ? v : 300);

    public async Task<ServiceResult<StepUpChallenge>> StartAsync(Guid userId, string? action, CancellationToken ct = default)
    {
        if (!await CanStepUpAsync(userId, ct))
            return new ServiceResult<StepUpChallenge>(false, "no_trusted_device");

        var issued = await challenges.IssueAsync(userId, AuthChallengePurpose.StepUp,
            action is { Length: > 0 } ? $"{{\"action\":\"{action}\"}}" : null, ct);
        return new ServiceResult<StepUpChallenge>(true,
            Value: new StepUpChallenge(issued.ChallengeId, issued.Nonce, issued.MatchNumber, issued.ExpiresAt));
    }

    public async Task<ServiceResult<bool>> CompleteAsync(Guid userId, Guid challengeId, Guid deviceId,
        string signature, int? matchNumber, CancellationToken ct = default)
    {
        var challenge = await db.AuthChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == challengeId, ct);
        // Scoped to the caller and to StepUp: a Login challenge must never be redeemable as a step-up.
        if (challenge is null || challenge.UserId != userId || challenge.Purpose != AuthChallengePurpose.StepUp)
            return new ServiceResult<bool>(false, "not_found");

        var device = await db.TrustedDevices.AsNoTracking().FirstOrDefaultAsync(
            d => d.Id == deviceId && d.UserId == userId && d.DeletedAt == null, ct);
        if (device is null)
            return new ServiceResult<bool>(false, "not_found");
        if (device.LifecycleState != DeviceLifecycleState.Trusted)
            return new ServiceResult<bool>(false, "device_not_trusted");

        var result = await challenges.VerifySignatureAsync(challengeId, deviceId, signature,
            AuthChallengePurpose.StepUp, matchNumber, ct);
        if (!result.Ok)
            return new ServiceResult<bool>(false, result.Error);

        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "stepup.satisfied", Severity = "info",
            ContextJson = $"{{\"deviceId\":\"{deviceId}\",\"challengeId\":\"{challengeId}\"}}",
        });
        await db.SaveChangesAsync(ct);
        return new ServiceResult<bool>(true, Value: true);
    }

    public async Task<StepUpStatus> StatusAsync(Guid userId, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - Window;
        var latest = await db.AuthChallenges.AsNoTracking()
            .Where(c => c.UserId == userId && c.Purpose == AuthChallengePurpose.StepUp
                        && c.Status == AuthChallengeStatus.Approved && c.ConsumedAt != null && c.ConsumedAt > since)
            .OrderByDescending(c => c.ConsumedAt)
            .Select(c => c.ConsumedAt)
            .FirstOrDefaultAsync(ct);

        return latest is null ? new StepUpStatus(false, null) : new StepUpStatus(true, latest.Value + Window);
    }

    public Task<bool> CanStepUpAsync(Guid userId, CancellationToken ct = default)
        => db.TrustedDevices.AnyAsync(
            d => d.UserId == userId && d.DeletedAt == null && d.LifecycleState == DeviceLifecycleState.Trusted, ct);
}
