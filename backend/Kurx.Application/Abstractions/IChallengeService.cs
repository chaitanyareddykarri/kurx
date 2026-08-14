using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

public record ChallengeIssued(Guid ChallengeId, string Nonce, int MatchNumber, DateTime ExpiresAt);
public record ChallengeVerifyResult(bool Ok, string? Error = null, Guid? UserId = null, Guid? DeviceId = null);

/// <summary>Single-use challenge–response core (AM2, ADR-A3/A9). Issues a signed-nonce challenge and
/// verifies a device signature over it against the device's stored public key, consuming the challenge on
/// success. Pure crypto + challenge lifecycle — device-state and session policy belong to the caller
/// (enrollment: <see cref="ITrustedDeviceService"/>; login: AM4).</summary>
public interface IChallengeService
{
    Task<ChallengeIssued> IssueAsync(Guid userId, AuthChallengePurpose purpose, string? contextJson, CancellationToken ct = default);

    /// <summary>Verifies a device signature over the challenge payload and consumes the challenge.
    /// <paramref name="expectedPurpose"/> is <b>required</b>, not optional: a challenge issued for one
    /// ceremony must never be redeemable in another (a Login challenge is not a step-up, and is not an
    /// enrollment proof). Making it a parameter means no call site can silently forget the check.
    ///
    /// <para><paramref name="enteredMatchNumber"/> is the two-digit code the user read off the browser
    /// and typed on the approving device. For purposes that display one it is <b>required and verified
    /// here</b> — never by the client, which any modified build could simply skip — and it is bound into
    /// the signed payload, so the signature attests to the digits the user actually saw. Whether it is
    /// required is decided centrally from the purpose, so a call site cannot opt out by passing
    /// null.</para></summary>
    Task<ChallengeVerifyResult> VerifySignatureAsync(Guid challengeId, Guid deviceId, string signatureBase64,
        AuthChallengePurpose expectedPurpose, int? enteredMatchNumber, CancellationToken ct = default);
}
