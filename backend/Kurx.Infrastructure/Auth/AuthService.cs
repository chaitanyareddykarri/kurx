using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Auth;

/// <summary>
/// OTP login + refresh-token rotation. Rate limits live here against Postgres
/// (docs/DECISIONS.md D-005) so they survive restarts and are testable.
/// </summary>
public class AuthService(KurxDbContext db, TokenService tokens, IOtpService otp, ILogger<AuthService> log,
    IAuthTelemetry telemetry, IConfiguration config)
    : IAuthService
{
    public const int DefaultMaxPerIpPerHour = 10;

    /// <summary>Login and phone-change both mean "prove you control this number", and D-037 has them share
    /// one request endpoint — so both must mint and verify under the SAME purpose or a code issued by
    /// <c>/v1/auth/otp/request</c> would not verify at <c>/v1/me/phone/verify</c>.</summary>
    private const OtpPurpose LoginPurpose = OtpPurpose.PhoneVerification;

    /// <summary>This service's storage form is bare digits (<see cref="NormalizePhone"/>); the OTP platform
    /// keys on a '+'-prefixed destination. Same bridge PasswordResetService/RecoveryCodeService already use.
    /// Passed with <c>destinationIsCanonical: true</c>, so it is a key format only — deliberately NOT run
    /// through libphonenumber, which rejects numbers this login contract has always accepted.</summary>
    private static string ToOtpDestination(string normalizedPhone) => "+" + normalizedPhone;

    // Overridable so a growing integration-test class sharing one fake IP across many OTP-login
    // helper calls doesn't trip the same per-IP ceiling meant for real anonymous traffic (D-038).
    private readonly int _maxPerIpPerHour =
        int.TryParse(config["RATE_LIMIT_OTP_IP_PER_HOUR"], out var maxPerIpPerHour) ? maxPerIpPerHour : DefaultMaxPerIpPerHour;

    public async Task<OtpRequestResult> RequestOtpAsync(string phone, string? requestIp, CancellationToken ct = default)
    {
        phone = NormalizePhone(phone);
        if (phone.Length is < 8 or > 16)
            return new OtpRequestResult(false, "invalid_phone");

        // A number libphonenumber could not place is refused here, before a code is minted.
        //
        // `NormalizePhone` validates but does not REJECT: on failure it returns the input's bare digits
        // (D-290 — "an unplaceable number is returned as its digits rather than assigned a country",
        // which is right, because inventing a country is what sent one user's code to a stranger). The
        // digits then satisfied the length gate above and an OTP was issued to a destination that cannot
        // exist — `+911111111111` passed. Nothing was insecure about it: the code went nowhere and no
        // account is created until VerifyOtpAsync consumes one. It was a dead end presented as a sent
        // message, and it let a single caller mint rows against unbounded fictional destinations.
        //
        // Re-parsing `"+" + phone` cannot reject a number this method just accepted: the success branches
        // of `NormalizePhone` both return `parsed.E164[1..]`, so restoring the '+' reconstructs exactly the
        // E.164 libphonenumber produced. Only the fallback branch — the one that never parsed — fails here.
        // This is also why it does not violate D-290's "never re-normalize a stored bare-digit phone": the
        // value is fresh input normalized microseconds ago, not a column read, and adding back the '+'
        // states no country the digits did not already state.
        if (!PhoneCanonicalizer.TryToE164(ToOtpDestination(phone), out _))
            return new OtpRequestResult(false, "invalid_phone");

        // Per-IP shield stays here: IOtpService caps per DESTINATION, not per source, so delegating
        // without this would drop the one limit that stops a single host enumerating many numbers.
        // Counted over otp_codes — the table the codes now live in.
        if (requestIp is not null)
        {
            var hourAgo = DateTime.UtcNow.AddHours(-1);
            var ipCount = await db.OtpCodes.CountAsync(o => o.RequestIp == requestIp && o.CreatedAt >= hourAgo, ct);
            if (ipCount >= _maxPerIpPerHour)
                return new OtpRequestResult(false, "rate_limited", RetryAfterSeconds: 3600);
        }

        // Delegated to the hardened OTP platform (AM1/ADR-A4). The code is CSPRNG-minted and stored
        // HMAC-peppered in otp_codes; the previous inline path hashed a 6-digit code with UNSALTED
        // SHA-256, whose whole 10^6 keyspace is trivially precomputed — the stored hash was
        // effectively plaintext to anyone who could read the table (dossier finding C5).
        var userId = await db.Users.AsNoTracking().Where(u => u.Phone == phone)
            .Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        // Storage/hashing come from the OTP platform; the rate-limit POLICY stays exactly what this endpoint
        // has always had (3 per destination per 10 min, plus the per-IP shield above). Importing the
        // platform's 30s resend spacing here would refuse a legitimate re-request seconds later — a login
        // UX change, not part of moving the hash off unsalted SHA-256.
        var issued = await otp.IssueAsync(ToOtpDestination(phone), OtpChannelPolicy.For(LoginPurpose), LoginPurpose,
            userId, requestIp, ct, destinationIsCanonical: true, enforceResendCooldown: false);
        if (!issued.Ok)
            return new OtpRequestResult(false, issued.Error, issued.RetryAfterSeconds);

        log.LogInformation("OTP issued for {Phone}", phone);
        return new OtpRequestResult(true);
    }

    public async Task<AuthResult> VerifyOtpAsync(string phone, string code, CancellationToken ct = default)
    {
        phone = NormalizePhone(phone);

        // No bypass branch belongs here, in any build or environment — three have been added and removed
        // already (D-274). Always the real check.
        if (await ValidateAndConsumeOtpAsync(phone, code, ct) is { } error)
            return new AuthResult(false, error);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Phone == phone, ct);
        var isNew = user is null;
        if (user is null)
        {
            // Name/username are collected by the onboarding screen after first login (D-011/D-037).
            user = new User { Phone = phone, Name = "" };
            ApplyCanonicalPhone(user, phone);       // dual-write (D-089): legacy + canonical together
            db.Users.Add(user);
        }

        // A suspended or banned account (D-060) cannot log in. Revoke any lingering sessions so it can't
        // renew either, then refuse — the OTP was already consumed above, so a blocked user still burns it.
        if (!isNew && (user.BannedAt is not null || user.SuspendedAt is not null))
        {
            await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
            return new AuthResult(false, user.BannedAt is not null ? "account_banned" : "account_suspended");
        }

        // Audit log added to the change tracker before IssueTokensAsync so it is saved
        // in the same SaveChangesAsync call that writes the new refresh token.
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = user.Id,
            Action = "auth.login", Entity = "users", EntityId = user.Id,
        });

        try
        {
            var authTokens = await IssueTokensAsync(user, ct);
            return new AuthResult(true, Tokens: authTokens, UserId: user.Id, IsNewUser: isNew);
        }
        catch (DbUpdateException ex) when (isNew && ex.InnerException?.Message?.Contains("23505") == true)
        {
            // Another concurrent request already created this phone's user row — attach to it
            // instead of failing the whole login (D-037).
            db.ChangeTracker.Clear();
            var existing = await db.Users.FirstAsync(u => u.Phone == phone, ct);
            var authTokens = await IssueTokensAsync(existing, ct);
            return new AuthResult(true, Tokens: authTokens, UserId: existing.Id, IsNewUser: false);
        }
    }

    /// <summary>Verifies an OTP sent to a new phone and reassigns it to the authenticated user (D-037).
    /// User.Id and every relationship are untouched — only Phone changes.</summary>
    public async Task<AuthResult> VerifyPhoneChangeAsync(Guid userId, string newPhone, string code, CancellationToken ct = default)
    {
        newPhone = NormalizePhone(newPhone);
        if (await ValidateAndConsumeOtpAsync(newPhone, code, ct) is { } error)
            return new AuthResult(false, error);

        if (await db.Users.AnyAsync(u => u.Phone == newPhone && u.Id != userId, ct))
            return new AuthResult(false, "phone_already_registered");

        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        user.Phone = newPhone;
        ApplyCanonicalPhone(user, newPhone);        // dual-write (D-089): the two must never disagree

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true)
        {
            // Lost a concurrent race for the same new number.
            return new AuthResult(false, "phone_already_registered");
        }

        // A stolen device with a live refresh token must not remain authenticated after the
        // legitimate owner moves to a new phone — revoke every other session (D-038), same
        // reuse-revokes-all guarantee RefreshAsync already gives on token-theft detection
        // (D-009/D-014), just triggered by a phone change instead. The calling request's own
        // access token was already validated by JWT middleware before this handler ran, so
        // revoking refresh tokens here can't retroactively break the in-flight request.
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId,
            Action = "auth.phone_changed", Entity = "users", EntityId = userId,
            DetailsJson = "{\"all_sessions_revoked\":true}",
        });
        await db.SaveChangesAsync(ct);

        var authTokens = await IssueTokensAsync(user, ct);
        return new AuthResult(true, Tokens: authTokens, UserId: userId, Phone: newPhone);
    }

    /// <summary>Shared by login and phone-change. Delegates to the hardened OTP platform, which owns the
    /// peppered hash, the TTL, the attempt cap and single-use consumption. Returns null on success, or the
    /// error code to surface.</summary>
    private async Task<string?> ValidateAndConsumeOtpAsync(string phone, string code, CancellationToken ct)
    {
        var result = await otp.VerifyAsync(ToOtpDestination(phone), OtpChannelPolicy.For(LoginPurpose), LoginPurpose, code, ct,
            destinationIsCanonical: true);
        return result.Ok ? null : result.Error;
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, DeviceProof? proof = null, CancellationToken ct = default)
    {
        var hash = TokenService.Sha256(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null)
            return new AuthResult(false, "invalid_token");

        if (stored.RevokedAt is not null)
        {
            // Reuse of a rotated token means the chain leaked. A device-bound token blast-radius-limits
            // that to its own session family (D-081); a legacy bearer token has no family, so it keeps the
            // original kill-everything response (D-009/D-014).
            await RevokeOnReuseAsync(stored, ct);
            return new AuthResult(false, "invalid_token");
        }
        if (stored.ExpiresAt <= DateTime.UtcNow)
            return new AuthResult(false, "invalid_token");

        var user = await db.Users.FirstAsync(u => u.Id == stored.UserId, ct);
        // A suspended/banned account (D-060) can't renew a session either — kill them all and refuse.
        if (user.BannedAt is not null || user.SuspendedAt is not null)
        {
            await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
            return new AuthResult(false, user.BannedAt is not null ? "account_banned" : "account_suspended");
        }

        // Sender-constrained refresh (D-081): a token minted for a device only rotates for someone who can
        // sign with that device's private key, so a stolen refresh string alone is inert. A bad proof is
        // refused but never revokes — otherwise anyone holding the string could kill the real session.
        if (stored.PoPKeyThumbprint is not null && !await ProofIsValidAsync(stored, proof, refreshToken, ct))
        {
            db.SecurityEvents.Add(new SecurityEvent
            {
                UserId = user.Id, Type = "refresh.pop_failed", Severity = "warning",
                ContextJson = $"{{\"sessionId\":\"{stored.SessionId}\"}}",
            });
            await db.SaveChangesAsync(ct);
            return new AuthResult(false, "proof_required");
        }

        // Validated before the claim below, so a token whose session is already gone is refused without
        // consuming it — the claim is the point of no return.
        AuthSession? session = null;
        if (stored.SessionId is Guid sessionId)
        {
            session = await db.AuthSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
            if (session is null || session.RevokedAt is not null)
                return new AuthResult(false, "invalid_token");    // session revoked from "your devices"
        }

        // Single-use rotation, claimed ATOMICALLY (D-240). The RevokedAt test at the top of this method is
        // only a read: assigning `stored.RevokedAt` here and letting SaveChangesAsync flush it later left a
        // window in which two requests carrying the SAME refresh token both passed that read and both
        // minted a chain. One token then yielded two live sessions and reuse detection never fired — which
        // is the entire purpose of rotation (D-009/D-014). Verified: two parallel /v1/auth/refresh calls
        // with one token both returned 200 before this. The conditional UPDATE is the claim; Postgres
        // evaluates it under the row lock, so exactly one caller sees 1 affected row.
        var rotated = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
        if (rotated == 0)
            // Lost the race. Deliberately NOT RevokeOnReuseAsync (D-240): a client firing two refreshes at
            // once is ordinary behaviour — parallel prefetches, a burst of 401s — not proof of theft, and
            // revoking the family would sign the user out of every device for it. Genuine reuse, the same
            // token presented after rotation has settled, still trips full family revocation through the
            // RevokedAt branch at the top of this method.
            return new AuthResult(false, "invalid_token");

        if (session is not null) session.LastRotatedAt = DateTime.UtcNow;

        // Rotation stays inside the same session and keeps the same sender constraint.
        var authTokens = await IssueTokensAsync(user, ct, stored.SessionId, stored.PoPKeyThumbprint);
        return new AuthResult(true, Tokens: authTokens, UserId: user.Id);
    }

    /// <summary>Verifies the device signature over the presented refresh token. The signing credential must
    /// belong to the session's own device and still be live — a revoked device cannot rotate.</summary>
    private async Task<bool> ProofIsValidAsync(RefreshToken stored, DeviceProof? proof, string refreshToken, CancellationToken ct)
    {
        if (proof is null || string.IsNullOrWhiteSpace(proof.Signature))
            return false;

        var device = await db.TrustedDevices.AsNoTracking().FirstOrDefaultAsync(
            d => d.Id == proof.DeviceId && d.UserId == stored.UserId && d.DeletedAt == null, ct);
        if (device is null || device.LifecycleState != DeviceLifecycleState.Trusted)
            return false;

        var credentials = await db.DeviceCredentials.AsNoTracking()
            .Where(c => c.TrustedDeviceId == proof.DeviceId && c.RevokedAt == null)
            .Select(c => c.PublicKeySpki)
            .ToListAsync(ct);

        // Match the thumbprint the session was minted with, so rotating in a new key doesn't silently
        // widen who can refresh an existing session.
        return credentials.Any(spki => DeviceSignatures.Thumbprint(spki) == stored.PoPKeyThumbprint
                                       && DeviceSignatures.VerifyEs256(spki, refreshToken, proof.Signature));
    }

    private async Task RevokeOnReuseAsync(RefreshToken stored, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var familyOnly = stored.SessionId is not null;

        if (familyOnly)
        {
            var familyId = await db.AuthSessions.AsNoTracking()
                .Where(s => s.Id == stored.SessionId).Select(s => s.FamilyId).FirstOrDefaultAsync(ct);
            var sessionIds = await db.AuthSessions.Where(s => s.FamilyId == familyId)
                .Select(s => s.Id).ToListAsync(ct);

            await db.AuthSessions.Where(s => s.FamilyId == familyId && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now)
                    .SetProperty(x => x.RevokeReason, "reuse_detected"), ct);
            await db.RefreshTokens.Where(t => t.SessionId != null && sessionIds.Contains(t.SessionId.Value) && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        }
        else
        {
            await db.RefreshTokens.Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "system", ActorId = stored.UserId,
            Action = "auth.token_reuse_detected", Entity = "users", EntityId = stored.UserId,
            DetailsJson = familyOnly
                ? $"{{\"session_family_revoked\":\"{stored.SessionId}\"}}"
                : "{\"all_sessions_revoked\":true}",
        });
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = stored.UserId, Type = "refresh.reuse_detected", Severity = "critical",
            ContextJson = $"{{\"scope\":\"{(familyOnly ? "family" : "all")}\"}}",
        });
        await db.SaveChangesAsync(ct);

        log.LogWarning("Refresh token reuse detected for user {UserId}; revoked {Scope}",
            stored.UserId, familyOnly ? "session family" : "all sessions");
        // Token-theft signal: the metric is what an alert fires on, the span event is what an
        // incident timeline reads.
        telemetry.RecordSecurityEvent("refresh.reuse_detected", "critical");
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = TokenService.Sha256(refreshToken);
        // Load the token entity to get UserId for the audit log, then revoke it.
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, ct);
        if (stored is null) return;

        stored.RevokedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = stored.UserId,
            Action = "auth.logout", Entity = "users", EntityId = stored.UserId,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<AuthTokens> IssueTokensAsync(User user, CancellationToken ct,
        Guid? sessionId = null, string? popKeyThumbprint = null)
    {
        var (access, accessExp) = tokens.CreateAccessToken(user);
        var (rawRefresh, refreshHash, refreshExp) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = refreshHash, ExpiresAt = refreshExp,
            SessionId = sessionId, PoPKeyThumbprint = popKeyThumbprint,
        });
        await db.SaveChangesAsync(ct);
        return new AuthTokens(access, accessExp, rawRefresh, refreshExp);
    }

    /// <summary>Dual-write (D-089, phase 4): keeps the canonical columns in step with every legacy
    /// <c>Phone</c> write, so the two representations can never drift apart while both exist.
    ///
    /// <para>The legacy value is bare digits including the country code, so it is parsed as international.
    /// If it cannot be parsed the canonical columns are left null rather than guessed — a null means
    /// "not migrated" and callers fall back to <c>Phone</c>, which is exactly the safe behavior. This
    /// path never invents a region: the legacy region default lives only in the backfill job.</para></summary>
    private static void ApplyCanonicalPhone(User user, string legacyDigits)
    {
        if (PhoneCanonicalizer.TryParse("+" + legacyDigits, region: null, out var parsed))
        {
            user.PhoneE164 = parsed.E164;
            user.CountryCode = parsed.CountryCode;
            user.PhoneNational = parsed.National;
        }
    }

    /// <summary>
    /// The platform's one phone normalizer: canonical E.164 digits, without the leading '+'.
    /// </summary>
    /// <remarks>
    /// <para><b>What this replaces, and why it mattered.</b> This used to be
    /// <c>digits.Length == 10 ? "91" + digits : digits</c> — a length rule applied <i>after</i> stripping the
    /// '+', so it could not tell a national number from a complete international one. Any number whose full
    /// E.164 form is exactly ten digits was silently refiled under India: <c>+65 9123 4567</c> (Singapore)
    /// became <c>+91 6591234567</c>, and the same holds for Norway, Denmark and every other plan with a
    /// two-digit country code and an eight-digit national number. The user could not register, and their
    /// one-time code was delivered to an unrelated real phone in India.</para>
    ///
    /// <para><b>The fix is to honour the '+'.</b> libphonenumber reads the country from the number itself
    /// when the input is international and ignores the region hint entirely, so an explicit E.164 value now
    /// survives exactly as given. No length heuristic remains anywhere.</para>
    ///
    /// <para><b>Bare national input</b> is still accepted, because clients that predate the E.164 contract
    /// send it and rejecting it outright would be a breaking change. It is interpreted in
    /// <see cref="LegacyInputRegion"/> and — this is the part that differs from the old code —
    /// <i>validated</i> there rather than assumed: libphonenumber has to agree the number is real for that
    /// region. A number it cannot place is returned as its digits rather than being assigned a country,
    /// because inventing one is what caused the original defect.</para>
    ///
    /// <para>Storage stays bare digits so existing rows and the dual-read in
    /// <see cref="AuthIdentifiers"/> keep resolving unchanged (D-089).</para>
    /// </remarks>
    public static string NormalizePhone(string phone)
    {
        var trimmed = (phone ?? "").Trim();
        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());

        // The '+' is the caller stating the country. Never second-guess it.
        if (trimmed.StartsWith('+'))
            return PhoneCanonicalizer.TryToE164(trimmed, out var e164) ? e164[1..] : digits;

        // No '+': interpret in the legacy region, but only if it is genuinely a number there.
        return PhoneCanonicalizer.TryParse(digits, LegacyInputRegion, out var parsed) ? parsed.E164[1..] : digits;
    }

    /// <summary>Region used to read a phone number that arrives <b>without</b> a country code, from a client
    /// predating the E.164 contract. It is a parsing hint for ambiguous input, never a prefix applied on
    /// length — an international number carrying its own '+' never reaches this.</summary>
    private const string LegacyInputRegion = "IN";
}
