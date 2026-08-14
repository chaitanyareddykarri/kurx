namespace Kurx.Application.Abstractions;

public record AuthTokens(string AccessToken, DateTime AccessExpiresAt, string RefreshToken, DateTime RefreshExpiresAt);

public record OtpRequestResult(bool Ok, string? Error = null, int? RetryAfterSeconds = null);

public record AuthResult(bool Ok, string? Error = null, AuthTokens? Tokens = null, Guid? UserId = null, bool IsNewUser = false, string? Phone = null);

/// <summary>Proof of possession for a sender-constrained refresh (AM5, D-081): the enrolled device
/// signs the raw refresh token with the private key whose public half minted the session.</summary>
public record DeviceProof(Guid DeviceId, string Signature);

public interface IAuthService
{
    /// <summary>Sends a one-time code over WhatsApp. Enforces 3/phone/10min and 10/IP/hour against the database.</summary>
    Task<OtpRequestResult> RequestOtpAsync(string phone, string? requestIp, CancellationToken ct = default);

    /// <summary>Verifies the latest pending code for the phone (max 5 attempts). Creates the user on first login.</summary>
    Task<AuthResult> VerifyOtpAsync(string phone, string code, CancellationToken ct = default);

    /// <summary>Rotates a refresh token. A device-bound token (AM5) is sender-constrained: it only rotates
    /// when <paramref name="proof"/> carries a valid device signature, and reuse revokes just that session
    /// family. A legacy bearer token keeps the D-009/D-014 revoke-every-session behavior.</summary>
    Task<AuthResult> RefreshAsync(string refreshToken, DeviceProof? proof = null, CancellationToken ct = default);

    /// <summary>Revokes one refresh token (no-op when unknown).</summary>
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Verifies an OTP sent to a new phone and reassigns it to the already-authenticated user (D-037).</summary>
    Task<AuthResult> VerifyPhoneChangeAsync(Guid userId, string newPhone, string code, CancellationToken ct = default);
}
