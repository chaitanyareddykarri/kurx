namespace Kurx.Application.Abstractions;

public record DeviceEnrollment(Guid DeviceId, Guid CredentialId, Guid ChallengeId, string Nonce, int MatchNumber, DateTime ExpiresAt);
public record TrustedDeviceView(Guid Id, string? Name, string Platform, string State, DateTime? LastSeenAt, DateTime CreatedAt);

/// <summary>A live sign-in as shown on the "your devices" screen (AM5).</summary>
public record AuthSessionView(Guid Id, Guid? DeviceId, string? DeviceName, string? Platform,
    bool IsCurrent, DateTime CreatedAt, DateTime? LastRotatedAt);

/// <summary>Trusted-device enrollment + lifecycle (AM2, ADR-A3/AM12). Enrollment is a two-step
/// proof-of-possession: register the device's public key (device enters PendingVerification), then the
/// device signs a challenge to prove it holds the private key (→ Trusted). Every lifecycle transition
/// writes a security_event + audit_log row.</summary>
public interface ITrustedDeviceService
{
    Task<DeviceEnrollment> BeginEnrollmentAsync(Guid userId, string? name, string platform,
        string publicKeySpki, string alg, string? attestationJson, CancellationToken ct = default);

    Task<ServiceResult<bool>> CompleteEnrollmentAsync(Guid userId, Guid challengeId, Guid deviceId,
        string signatureBase64, CancellationToken ct = default);

    Task<IReadOnlyList<TrustedDeviceView>> ListAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Revokes the device and every session it holds — the "lost my phone" button.</summary>
    Task<ServiceResult<bool>> RevokeAsync(Guid userId, Guid deviceId, CancellationToken ct = default);

    /// <summary>The user's live sessions for the "your devices" screen (AM5).</summary>
    Task<IReadOnlyList<AuthSessionView>> ListSessionsAsync(Guid userId, Guid? currentSessionId = null, CancellationToken ct = default);

    /// <summary>Signs one session out remotely, killing its refresh-token chain.</summary>
    Task<ServiceResult<bool>> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
}
