using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

/// <param name="OtpId">The row this call minted, or null when nothing was issued. Callers that need to
/// prove the code they later verify is the one <b>they</b> asked for record this and compare it against
/// <see cref="OtpVerifyResult.OtpId"/> — otherwise any live code for the same (destination, purpose) would
/// satisfy the check, including one minted by a different ceremony.</param>
public record OtpIssueResult(bool Ok, string? Error = null, int? RetryAfterSeconds = null, Guid? OtpId = null);
public record OtpVerifyResult(bool Ok, string? Error = null, Guid? OtpId = null);

/// <summary>Hardened OTP platform (AM1, ADR-A4). Bootstrap/recovery only — never trusted-device login.
/// Codes are CSPRNG, stored HMAC-peppered in <c>otp_codes</c>, single-use, TTL-bound, attempt-capped,
/// with a resend cooldown; delivery is routed per channel (SMS via <see cref="ISmsProvider"/>, WhatsApp,
/// email). Destinations are E.164 (phone) or lowercased email (ADR-A6).</summary>
public interface IOtpService
{
    /// <param name="destinationIsCanonical">Skip this service's own phone canonicalization because the
    /// caller already owns one. Set only by the legacy login/phone-change path, whose accepted-number
    /// contract predates E.164 (D-089) and is deliberately wider than libphonenumber's validity check —
    /// routing it through the strict parser would stop numbers that can log in today from logging in.
    /// The destination must already be in its final, stable form: it is the issue/verify lookup key, so
    /// a caller that normalizes inconsistently would mint a code it can never verify.</param>
    /// <param name="enforceResendCooldown">Apply the 30s per-destination resend spacing. Set false only by
    /// the legacy login/phone-change path, which predates this platform and whose contract allows three
    /// back-to-back requests inside the 10-minute cap (still enforced). Adding the cooldown there would
    /// refuse a legitimate "Resend code" tap seconds after the first — a UX change, not a security fix.</param>
    Task<OtpIssueResult> IssueAsync(string destination, OtpChannel channel, OtpPurpose purpose,
        Guid? userId, string? requestIp, CancellationToken ct = default, bool destinationIsCanonical = false,
        bool enforceResendCooldown = true);

    Task<OtpVerifyResult> VerifyAsync(string destination, OtpChannel channel, OtpPurpose purpose,
        string code, CancellationToken ct = default, bool destinationIsCanonical = false);
}
