namespace Kurx.Application.Abstractions;

public record StepUpChallenge(Guid ChallengeId, string Nonce, int MatchNumber, DateTime ExpiresAt);
public record StepUpStatus(bool Satisfied, DateTime? ValidUntil);

/// <summary>Step-up authentication / assurance level (AM6, ADR-A10). A normal session is enough to browse;
/// a genuinely sensitive action additionally demands a <b>fresh</b> device signature, so a stolen access
/// token alone cannot perform it. The grant is time-boxed, not permanent.</summary>
public interface IStepUpService
{
    /// <summary>Issues a StepUp challenge for the user's trusted devices to sign.</summary>
    Task<ServiceResult<StepUpChallenge>> StartAsync(Guid userId, string? action, CancellationToken ct = default);

    /// <summary>Completes a step-up with a device signature over the challenge payload.
    /// <paramref name="matchNumber"/> is the two-digit code shown where the step-up was requested,
    /// verified server-side and bound into the signature.</summary>
    Task<ServiceResult<bool>> CompleteAsync(Guid userId, Guid challengeId, Guid deviceId, string signature,
        int? matchNumber, CancellationToken ct = default);

    /// <summary>Whether the user stepped up recently enough to satisfy a sensitive action right now.</summary>
    Task<StepUpStatus> StatusAsync(Guid userId, CancellationToken ct = default);

    /// <summary>True when the user has a trusted device, i.e. step-up is actually possible for them.
    /// A user with no device cannot be asked to step up — that would lock them out permanently.</summary>
    Task<bool> CanStepUpAsync(Guid userId, CancellationToken ct = default);
}
