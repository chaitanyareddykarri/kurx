namespace Kurx.Application.Abstractions;

/// <summary>Where the user is in the registration ceremony (Phase 2D, architecture §4.14/§5). Purely
/// informational — the client uses <see cref="Remaining"/> to drive the flow (verify email → phone →
/// create password → complete profile → enroll a trusted device). Phone is verified implicitly by the OTP
/// login that created the account.</summary>
public record RegistrationStatus(bool HasPassword, string? Email, bool EmailVerified, string? Phone,
    bool PhoneVerified, bool HasTrustedDevice, bool NeedsOnboarding, IReadOnlyList<string> Remaining);

/// <summary>Registration ceremony + email verification (Phase 2D). Email is a verified recovery and
/// notification channel; it is never on its own an authentication factor. All operations are for an
/// already-authenticated user (registration begins with the phone-OTP login that mints the session).</summary>
public interface IRegistrationService
{
    /// <summary>Sends a verification OTP to <paramref name="email"/>. Rejects a malformed address or one
    /// already owned by another account.</summary>
    Task<ServiceResult<bool>> StartEmailVerificationAsync(Guid userId, string email, string? requestIp, CancellationToken ct = default);

    /// <summary>Verifies the emailed code and, on success, sets the user's email and marks it verified.</summary>
    Task<ServiceResult<bool>> CompleteEmailVerificationAsync(Guid userId, string email, string code, CancellationToken ct = default);

    Task<RegistrationStatus> GetStatusAsync(Guid userId, CancellationToken ct = default);
}
