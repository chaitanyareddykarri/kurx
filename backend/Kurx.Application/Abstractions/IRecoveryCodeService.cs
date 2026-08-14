namespace Kurx.Application.Abstractions;

public record RecoveryRedeemResult(bool Ok, string? Error = null, AuthTokens? Tokens = null, Guid? UserId = null);

/// <summary>One-time account recovery codes (AM7, ADR-A4/A11) — the way back in when every trusted device
/// is gone. Codes are high-entropy, hashed at rest, single-use, and generating a new set invalidates the
/// old one. Redeeming is deliberately <b>two-factor</b>: a code alone is never enough, it must be paired
/// with an OTP delivered to the account's registered phone.</summary>
public interface IRecoveryCodeService
{
    /// <summary>Mints a fresh set, invalidating any unused codes. The plaintext is returned <b>once</b> —
    /// it is never recoverable afterwards.</summary>
    Task<IReadOnlyList<string>> GenerateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>How many unused codes remain, for the "you have N codes left" prompt.</summary>
    Task<int> RemainingAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Sends the recovery OTP. Anti-enumeration: always reports success.</summary>
    Task StartAsync(string identifier, string? requestIp, CancellationToken ct = default);

    /// <summary>Consumes one code plus the recovery OTP and issues a session. A successful recovery
    /// revokes every existing session and suspends every trusted device — recovery implies the old
    /// devices may be lost or hostile.</summary>
    Task<RecoveryRedeemResult> RedeemAsync(string identifier, string otpCode, string recoveryCode,
        CancellationToken ct = default);

    /// <summary>Consumes a single unused recovery code for the user (marks it used). Returns false if the
    /// code is unknown or already spent. Used as the "no trusted device" second factor of the password
    /// reset ceremony (D-127) without duplicating the code-hashing rule.</summary>
    Task<bool> ConsumeAsync(Guid userId, string code, CancellationToken ct = default);
}
