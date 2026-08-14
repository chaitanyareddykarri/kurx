namespace Kurx.Application.Abstractions;

/// <summary>A pending WebAuthn ceremony: the challenge id we correlate on, plus the raw
/// options JSON the browser hands straight to <c>navigator.credentials</c>.</summary>
public record PasskeyOptions(Guid ChallengeId, string OptionsJson);

public record PasskeyLoginResult(bool Ok, string? Error = null, AuthTokens? Tokens = null, Guid? UserId = null);

/// <summary>WebAuthn / passkeys (AM3, ADR-A3 rail 2). The second credential rail alongside the custom
/// device-key rail: same <c>trusted_devices</c> + <c>device_credentials</c> aggregate, different
/// signature format. Passkeys are phishing-resistant by construction — the authenticator binds the
/// assertion to the origin, so a lookalike domain cannot harvest a usable signature.</summary>
public interface IPasskeyService
{
    /// <summary>Registration ceremony step 1, for an already-authenticated user adding a passkey.</summary>
    Task<PasskeyOptions> BeginRegistrationAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Registration step 2: verifies the attestation and enrolls the passkey as a trusted device.</summary>
    Task<ServiceResult<Guid>> CompleteRegistrationAsync(Guid userId, Guid challengeId, string attestationJson,
        string? deviceName, CancellationToken ct = default);

    /// <summary>Login ceremony step 1. Anti-enumeration: an unknown identifier still receives
    /// well-formed options, just with no credentials to satisfy them.</summary>
    Task<PasskeyOptions> BeginLoginAsync(string identifier, CancellationToken ct = default);

    /// <summary>Login step 2: verifies the assertion and issues a device-bound session.</summary>
    Task<PasskeyLoginResult> CompleteLoginAsync(Guid challengeId, string assertionJson, CancellationToken ct = default);

    /// <summary>The user's registered passkeys, for management UI.</summary>
    Task<IReadOnlyList<TrustedDeviceView>> ListAsync(Guid userId, CancellationToken ct = default);
}
