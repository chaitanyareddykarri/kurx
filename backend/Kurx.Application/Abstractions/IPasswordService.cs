namespace Kurx.Application.Abstractions;

/// <summary>Why a password was refused. Returned to the caller so the UI can be specific about a
/// <b>policy</b> failure (which the user must be able to fix) while staying deliberately vague about
/// <b>authentication</b> failure (which must never distinguish "wrong password" from "no such account").</summary>
public enum PasswordRejection
{
    None = 0,
    TooShort,
    TooLong,
    Breached,           // appears in the common/breached deny list
    ContainsIdentifier, // derived from the user's own username / email / phone
    Reused,             // matches a recent password for this account
    SameAsCurrent,
}

/// <summary>Outcome of a password-verification attempt. <paramref name="LockedUntil"/> is set only when
/// the account is currently locked; callers must not leak it to an unauthenticated surface.</summary>
public readonly record struct PasswordCheck(bool Ok, bool Locked, DateTime? LockedUntil);

/// <summary>Password lifecycle — factor 1 of the trusted-device architecture (D-126, D-129).
///
/// <para>Policy follows <b>NIST SP 800-63B</b>: length is the primary control, composition rules
/// ("must contain a symbol") are deliberately absent because they push users toward predictable
/// substitutions, and there is no forced expiry — rotation is triggered by evidence of compromise,
/// not by a calendar.</para></summary>
public interface IPasswordService
{
    /// <summary>Whether this account has a password yet. Existing OTP-era accounts do not until they
    /// complete first-password creation.</summary>
    Task<bool> HasPasswordAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Validates a candidate against policy without touching storage. Pure, so the UI can call
    /// the same rules the server enforces and the two can never drift.</summary>
    PasswordRejection ValidatePolicy(string password, string? username, string? email, string? phone);

    /// <summary>First-time password creation. Refuses if one already exists — changing an existing
    /// password must go through <see cref="ChangeAsync"/>, which proves knowledge of the current one.</summary>
    Task<ServiceResult<bool>> SetInitialAsync(Guid userId, string password, CancellationToken ct = default);

    /// <summary>Changes a password, requiring the current one. Enforces history so a "change" cannot be
    /// a silent no-op reuse.</summary>
    Task<ServiceResult<bool>> ChangeAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>Verifies a password and maintains lockout state. Returns <c>Ok=false</c> for a missing
    /// credential row as well as a wrong password — the caller must not be able to tell them apart.</summary>
    Task<PasswordCheck> VerifyAsync(Guid userId, string password, CancellationToken ct = default);

    /// <summary>Login-time verification for a possibly-null user (the submitted identifier may not resolve).
    /// Runs a real Argon2id hash even when there is no account or no password, so response time cannot
    /// distinguish "no such user" from "wrong password" — anti-enumeration by <b>timing</b> as well as
    /// shape (Phase 2B, architecture §5).</summary>
    Task<PasswordCheck> VerifyForLoginAsync(Guid? userId, string password, CancellationToken ct = default);

    /// <summary>Sets a new password <b>without</b> proving the current one — used only by the reset ceremony
    /// (Phase 2C), which has already re-authenticated the owner via OTP + a second factor. Enforces full
    /// policy and reuse history, then re-secures the account: cascade-revokes every trusted browser and
    /// every session so the reset forces a fresh login (INV-B). Upserts the credential for an account that
    /// had none.</summary>
    Task<ServiceResult<bool>> ResetAsync(Guid userId, string newPassword, CancellationToken ct = default);
}
