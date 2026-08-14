using System.Net.Mail;
using Kurx.Application.Abstractions;
using Kurx.Domain;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Auth;

/// <summary>Registration ceremony + email verification (Phase 2D). Reuses the OTP service for delivery and
/// the password service for the has-password check — it introduces no new credential mechanism. Email
/// uniqueness is enforced against the existing unique index; a verified email is a recovery/notification
/// channel, never an authentication factor.</summary>
public class RegistrationService(
    KurxDbContext db,
    IOtpService otp,
    IPasswordService passwords,
    DisposableEmailPolicy disposableEmail) : IRegistrationService
{
    public async Task<ServiceResult<bool>> StartEmailVerificationAsync(Guid userId, string email, string? requestIp,
        CancellationToken ct = default)
    {
        var normalized = Normalize(email);
        if (!IsValidEmail(normalized))
            return new ServiceResult<bool>(false, "invalid_email");
        if (await db.Users.AsNoTracking().AnyAsync(u => u.Id != userId && u.Email == normalized, ct))
            return new ServiceResult<bool>(false, "email_taken");

        var issue = await otp.IssueAsync(normalized, OtpChannel.Email, OtpPurpose.EmailVerification, userId, requestIp, ct);
        return issue.Ok ? new ServiceResult<bool>(true, Value: true) : new ServiceResult<bool>(false, issue.Error);
    }

    public async Task<ServiceResult<bool>> CompleteEmailVerificationAsync(Guid userId, string email, string code,
        CancellationToken ct = default)
    {
        var normalized = Normalize(email);
        if (!IsValidEmail(normalized))
            return new ServiceResult<bool>(false, "invalid_email");

        var verify = await otp.VerifyAsync(normalized, OtpChannel.Email, OtpPurpose.EmailVerification, code, ct);
        if (!verify.Ok)
            return new ServiceResult<bool>(false, "invalid_code");
        // Re-check ownership just before committing — the address may have been claimed since /start.
        if (await db.Users.AsNoTracking().AnyAsync(u => u.Id != userId && u.Email == normalized, ct))
            return new ServiceResult<bool>(false, "email_taken");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return new ServiceResult<bool>(false, "not_found");

        user.Email = normalized;
        user.EmailVerifiedAt = DateTime.UtcNow;
        db.SecurityEvents.Add(new SecurityEvent { UserId = userId, Type = "email.verified", Severity = "info" });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "auth.email_verified", Entity = "users", EntityId = userId,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true)
        {
            return new ServiceResult<bool>(false, "email_taken");   // lost the race on the unique index
        }

        await RecordDisposableEmailSignalAsync(userId, normalized, ct);
        return new ServiceResult<bool>(true, Value: true);
    }

    /// <summary>Records a <see cref="FraudSignalKind.DisposableContact"/> signal when a <b>verified</b>
    /// address turns out to be on a throwaway domain.
    ///
    /// <para><b>After the commit, and only on success.</b> Running it on the submitted address instead
    /// would score people who typed a domain and never proved they hold it — including anyone who simply
    /// mistyped. The signal is a statement about an address the user demonstrably controls.</para>
    ///
    /// <para><b>Exactly once per address.</b> Re-verifying the same address — a second device, a repeated
    /// ceremony — must not add score, or a user could be pushed over the threshold by doing a legitimate
    /// thing twice. Keyed on the stored <c>Value</c>, which is why the address is written there.</para>
    ///
    /// <para><b>It never fails the caller.</b> The verification has already committed; a fraud-signal write
    /// that throws must not turn a completed verification into an error the user sees, so this is
    /// deliberately best-effort. Recording nothing is a measurement gap, not a security one — the gate it
    /// feeds fails closed on identity, not on the absence of a signal.</para></summary>
    private async Task RecordDisposableEmailSignalAsync(Guid userId, string email, CancellationToken ct)
    {
        if (disposableEmail.IsEmpty || !disposableEmail.IsDisposable(email)) return;

        try
        {
            var already = await db.FraudSignals.AsNoTracking().AnyAsync(
                s => s.SubjectType == VerificationSubjectType.UserIdentity
                    && s.SubjectId == userId
                    && s.Kind == FraudSignalKind.DisposableContact
                    && s.Value == email, ct);
            if (already) return;

            db.FraudSignals.Add(new FraudSignal
            {
                SubjectType = VerificationSubjectType.UserIdentity,
                SubjectId = userId,
                Kind = FraudSignalKind.DisposableContact,
                Value = email,
                Score = DisposableEmailPolicy.SignalScore,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent verification of the same address won the insert. One signal exists, which is
            // the invariant; losing the race is the correct outcome, not an error.
        }
    }

    public async Task<RegistrationStatus> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var hasPassword = await passwords.HasPasswordAsync(userId, ct);
        var hasDevice = await db.TrustedDevices.AsNoTracking().AnyAsync(
            d => d.UserId == userId && d.DeletedAt == null && d.LifecycleState == DeviceLifecycleState.Trusted, ct);

        var phone = user.PhoneE164 ?? (string.IsNullOrEmpty(user.Phone) ? null : user.Phone);
        var emailVerified = user.EmailVerifiedAt is not null;

        // Both the flag and the step list come from Kurx.Domain.Onboarding, so this endpoint and /v1/me
        // can no longer answer "is this user onboarded" differently — they used to, on a blank-vs-empty
        // name check.
        var needsOnboarding = Onboarding.IsIncomplete(user, hasPassword);
        var remaining = Onboarding.Remaining(user, hasPassword, hasDevice);

        return new RegistrationStatus(hasPassword, user.Email, emailVerified, phone,
            PhoneVerified: phone is not null, hasDevice, needsOnboarding, remaining);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Accepts only a bare address (no display name) with a dotted domain — enough to stop obvious
    /// garbage without trying to fully validate deliverability, which only the OTP round-trip can.</summary>
    private static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && MailAddress.TryCreate(email, out var addr)
        && addr.Address == email && email.Contains('.');
}
