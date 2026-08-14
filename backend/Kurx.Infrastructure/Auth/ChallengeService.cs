using System.Security.Cryptography;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Kurx.Infrastructure.Auth;

/// <summary>Issues single-use signed-nonce challenges and verifies device signatures over them (AM2,
/// ADR-A3/A9). The durable record is Postgres; a Redis hot copy fronts this in AM8 (ADR-AM14). Replay is
/// prevented by single-use consumption + TTL; number-matching (<see cref="AuthChallenge.MatchNumber"/>)
/// defends the push-approval UX against blind approval (AM4).</summary>
public class ChallengeService(KurxDbContext db, IConfiguration config) : IChallengeService
{
    private int TtlSeconds => int.TryParse(config["AUTH_CHALLENGE_TTL_SECONDS"], out var v) ? v : 120;

    /// <summary>
    /// How long a challenge of this purpose stays open.
    /// </summary>
    /// <remarks>
    /// <para>120 s is right for a ceremony the user completes on a device already in their hand: a push
    /// arrives, they glance at it and tap. A short window is a security property there — it bounds how long
    /// a pushed approval can sit waiting for someone to approve it carelessly.</para>
    ///
    /// <para><see cref="AuthChallengePurpose.SecondFactor"/> is a different shape of wait. The user is
    /// waiting on a <b>network they do not control</b> — an SMS crossing a carrier, or an email crossing a
    /// mail provider — and must then read six digits and type them. 120 s routinely expires while a
    /// perfectly valid code is still in flight, and because the OTP itself lives 5 minutes the user is left
    /// holding a code the server will no longer accept. Each retry also burns one of the three permitted
    /// sends to that destination in ten minutes, so a slow carrier could lock a legitimate user out.</para>
    ///
    /// <para>The window is therefore sized to outlive the code it is waiting for, and its ceiling is the
    /// OTP's own 5-minute TTL — a longer challenge cannot extend the credential, only the opportunity to
    /// present one. This deliberately does <b>not</b> widen the device-approval or step-up windows.</para>
    /// </remarks>
    /// <para><see cref="AuthChallengePurpose.PasswordReset"/> (D-330) waits on the same SMS the reset OTP
    /// rides, so 120 s would expire while a valid code is still crossing the carrier. Its ceiling is the
    /// OTP's own 5-minute life: the approval must not outlive the credential it accompanies, and a single
    /// deadline for both halves of the ceremony is one thing to reason about instead of two. This is the
    /// whole lifetime of the approval — issue, device approval and completion all happen inside it.</para>
    private int TtlSecondsFor(AuthChallengePurpose purpose) => purpose switch
    {
        AuthChallengePurpose.SecondFactor =>
            int.TryParse(config["AUTH_SECOND_FACTOR_TTL_SECONDS"], out var s) ? s : 600,
        AuthChallengePurpose.PasswordReset =>
            int.TryParse(config["AUTH_RESET_APPROVAL_TTL_SECONDS"], out var r) ? r : 300,
        _ => TtlSeconds,
    };

    public async Task<ChallengeIssued> IssueAsync(Guid userId, AuthChallengePurpose purpose, string? contextJson, CancellationToken ct = default)
    {
        var nonce = Base64Url(RandomNumberGenerator.GetBytes(32));
        var matchNumber = RandomNumberGenerator.GetInt32(10, 100);   // two-digit anti-fatigue match
        var challenge = new AuthChallenge
        {
            UserId = userId,
            Purpose = purpose,
            Nonce = nonce,
            ContextJson = contextJson,
            MatchNumber = matchNumber,
            Status = AuthChallengeStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddSeconds(TtlSecondsFor(purpose)),
        };
        db.AuthChallenges.Add(challenge);
        await db.SaveChangesAsync(ct);
        return new ChallengeIssued(challenge.Id, nonce, matchNumber, challenge.ExpiresAt);
    }

    /// <summary>Which ceremonies display a match number on a <i>second</i> surface and therefore require
    /// the user to type it back. Login and step-up are begun somewhere the approving device cannot see —
    /// a browser — so confirming the digits is what proves the user is approving <i>that</i> request and
    /// not one an attacker triggered. Enrollment happens on the device itself, where there is no second
    /// screen and nothing to correlate, so demanding a code there would be theatre.</summary>
    /// <para><see cref="AuthChallengePurpose.PasswordReset"/> is included, and it is the control that makes
    /// the reset binding hold against a SIM swap (D-330). The attacker holds the number and therefore the
    /// OTP, and the push lands on the victim's phone — so approval alone would be one careless tap away.
    /// The digits are shown only in the browser that started the reset, which is the attacker's; the victim
    /// cannot type a number they were never shown, and a signature over one match number is not valid for
    /// a challenge carrying another.</para>
    public static bool RequiresMatchConfirmation(AuthChallengePurpose purpose) =>
        purpose is AuthChallengePurpose.Login or AuthChallengePurpose.StepUp
            or AuthChallengePurpose.PasswordReset;

    /// <summary>Exactly what the device signs. When a match number applies it is part of the payload, so
    /// the hardware signature attests to the digits the user saw rather than merely to the nonce. A
    /// signature captured for one match number cannot be replayed against a challenge showing another.
    /// <b>Mobile clients must build this string identically</b> — it is the device contract.</summary>
    public static string SignedPayload(string nonce, int? matchNumber) =>
        matchNumber is null ? nonce : $"{nonce}.{matchNumber.Value:00}";

    public async Task<ChallengeVerifyResult> VerifySignatureAsync(Guid challengeId, Guid deviceId, string signatureBase64,
        AuthChallengePurpose expectedPurpose, int? enteredMatchNumber, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var challenge = await db.AuthChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == challengeId, ct);
        if (challenge is null)
            return new ChallengeVerifyResult(false, "challenge_not_found");
        // Purpose binding, enforced centrally so every ceremony gets it (D-088). Reported as not-found so
        // probing with a challenge id cannot reveal which ceremony it belongs to.
        if (challenge.Purpose != expectedPurpose)
            return new ChallengeVerifyResult(false, "challenge_not_found");
        if (challenge.Status != AuthChallengeStatus.Pending)
            return new ChallengeVerifyResult(false, "challenge_consumed");     // single-use / already decided
        if (challenge.ExpiresAt <= now)
        {
            await db.AuthChallenges.Where(c => c.Id == challengeId && c.Status == AuthChallengeStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, AuthChallengeStatus.Expired), ct);
            return new ChallengeVerifyResult(false, "challenge_expired");
        }

        var matchRequired = RequiresMatchConfirmation(challenge.Purpose);
        if (matchRequired)
        {
            var check = await VerifyMatchNumberAsync(challenge, enteredMatchNumber, deviceId, ct);
            if (check is not null)
                return check;
        }

        var credential = await db.DeviceCredentials.AsNoTracking()
            .Where(c => c.TrustedDeviceId == deviceId && c.RevokedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (credential is null)
            return new ChallengeVerifyResult(false, "credential_not_found");

        var payload = SignedPayload(challenge.Nonce, matchRequired ? challenge.MatchNumber : null);
        if (!DeviceSignatures.VerifyEs256(credential.PublicKeySpki, payload, signatureBase64))
        {
            db.SecurityEvents.Add(new SecurityEvent
            {
                UserId = challenge.UserId, Type = "challenge.signature_invalid", Severity = "warning",
                ContextJson = $"{{\"deviceId\":\"{deviceId}\",\"challengeId\":\"{challengeId}\"}}",
            });
            await db.SaveChangesAsync(ct);
            return new ChallengeVerifyResult(false, "invalid_signature");
        }

        // First approval wins. The status transition is a single conditional UPDATE rather than a
        // read-then-write, because two devices approving the same challenge concurrently would otherwise
        // both observe Pending and both be told they succeeded — issuing two sessions from one challenge.
        // Postgres serialises the row, so exactly one caller sees a row affected.
        var claimed = await db.AuthChallenges
            .Where(c => c.Id == challengeId && c.Status == AuthChallengeStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, AuthChallengeStatus.Approved)
                .SetProperty(c => c.ConsumedAt, now)
                .SetProperty(c => c.ApprovedByDeviceId, deviceId), ct);
        if (claimed == 0)
            return new ChallengeVerifyResult(false, "challenge_consumed");     // lost the race

        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = challenge.UserId, Type = "challenge.approved", Severity = "info",
            ContextJson = $"{{\"deviceId\":\"{deviceId}\",\"purpose\":\"{challenge.Purpose}\"}}",
        });
        await db.SaveChangesAsync(ct);
        return new ChallengeVerifyResult(true, UserId: challenge.UserId, DeviceId: deviceId);
    }

    /// <summary>Returns null when the entered digits are correct, otherwise the failure to report.
    /// The counter is incremented with a conditional UPDATE so parallel guesses cannot both read the
    /// same count and write it back — that would make a 3-attempt cap trivially exceedable.</summary>
    private async Task<ChallengeVerifyResult?> VerifyMatchNumberAsync(
        AuthChallenge challenge, int? entered, Guid deviceId, CancellationToken ct)
    {
        if (entered is not null && entered == challenge.MatchNumber)
            return null;

        var attempts = challenge.MatchAttempts + 1;
        await db.AuthChallenges
            .Where(c => c.Id == challenge.Id && c.Status == AuthChallengeStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.MatchAttempts, c => c.MatchAttempts + 1), ct);
        attempts = await db.AuthChallenges.AsNoTracking()
            .Where(c => c.Id == challenge.Id).Select(c => c.MatchAttempts).FirstOrDefaultAsync(ct);

        if (attempts < AuthChallenge.MaxMatchAttempts)
        {
            db.SecurityEvents.Add(new SecurityEvent
            {
                UserId = challenge.UserId, Type = "challenge.match_number_invalid", Severity = "warning",
                ContextJson = $"{{\"deviceId\":\"{deviceId}\",\"challengeId\":\"{challenge.Id}\",\"attempts\":{attempts}}}",
            });
            await db.SaveChangesAsync(ct);
            return new ChallengeVerifyResult(false, "invalid_match_number");
        }

        // Cap reached: reject the challenge outright rather than merely failing this attempt. Wrong digits
        // mean the person approving is not looking at the browser that started this login — which is the
        // phishing case the whole step exists to catch — so the request does not deserve another try.
        await db.AuthChallenges
            .Where(c => c.Id == challenge.Id && c.Status == AuthChallengeStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, AuthChallengeStatus.Rejected)
                .SetProperty(c => c.ConsumedAt, DateTime.UtcNow), ct);
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = challenge.UserId, Type = "challenge.match_attempts_exhausted", Severity = "critical",
            ContextJson = $"{{\"deviceId\":\"{deviceId}\",\"challengeId\":\"{challenge.Id}\"}}",
        });
        await db.SaveChangesAsync(ct);
        return new ChallengeVerifyResult(false, "challenge_rejected");
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
