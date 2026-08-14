using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Auth;

/// <summary>Trusted-device push-approval login (AM4). No OTP: the device's private key is the factor.
/// Security properties: (1) anti-enumeration — an unknown identifier gets a decoy challenge, so responses
/// are indistinguishable; (2) the waiting web client is bound by a poll token whose hash is stored on the
/// challenge, so knowing a challenge id is not enough to collect tokens; (3) tokens are minted once, only
/// after a valid device signature, and the challenge is then Consumed.</summary>
public class LoginApprovalService(KurxDbContext db, IChallengeService challenges, TokenService tokens,
    IPushSender push, IRiskEngine risk, IRealtimeBroadcaster realtime, IAuthTelemetry telemetry,
    IPasswordService passwords, ITrustedBrowserService browsers, IOtpService otp,
    IConfiguration config, ILogger<LoginApprovalService> log) : ILoginApprovalService
{
    private int TtlSeconds => int.TryParse(config["AUTH_CHALLENGE_TTL_SECONDS"], out var v) ? v : 120;

    public async Task<PasswordLoginResult> PasswordLoginAsync(string identifier, string password,
        string? trustedBrowserCookie, bool rememberBrowser, TrustedBrowserContext browserContext,
        string? ip, string? userAgent, AuthSurface surface = AuthSurface.Web, CancellationToken ct = default)
    {
        using var span = telemetry.StartOperation("login.password");
        var user = await ResolveUserAsync(identifier, ct);

        // Factor 1 — password. Timing-equalized: a missing account runs a real hash too, so "no such user"
        // is indistinguishable from "wrong password" by response time as well as by shape.
        var pw = await passwords.VerifyForLoginAsync(user?.Id, password, ct);
        if (pw.Locked)
        {
            telemetry.RecordAuthAttempt("password", "locked");
            return new PasswordLoginResult("failed", "account_locked");
        }
        // A wrong password, an unknown identifier, and a moderated account all report the same generic
        // failure — none of them may reveal which it was.
        if (!pw.Ok || user is null || user.BannedAt is not null || user.SuspendedAt is not null)
        {
            telemetry.RecordAuthAttempt("password", "failed");
            return new PasswordLoginResult("failed", "invalid_credentials");
        }

        // Factor 2a — a valid trusted-browser cookie completes the login without a device round-trip.
        if (await browsers.VerifyAsync(user.Id, trustedBrowserCookie, ct))
        {
            var (browserTokens, _) = await IssueBrowserSessionAsync(user, ct);
            telemetry.RecordAuthAttempt("password", "success_browser");
            return new PasswordLoginResult("session", Tokens: browserTokens, UserId: user.Id);
        }

        var decision = await risk.EvaluateLoginAsync(user.Id, ip, userAgent, ct);
        if (decision.Action == RiskAction.Deny)
        {
            log.LogWarning("Password login denied by risk engine for user {UserId} (score {Score})", user.Id, decision.Score);
            telemetry.RecordSecurityEvent("risk.denied", "critical");
            return new PasswordLoginResult("failed", "invalid_credentials");
        }

        // Factor 2 — whatever this account actually holds, ranked for this surface (D-283).
        var methods = await AvailableSecondFactorsAsync(user, surface, ct);
        if (methods.Count == 0)
        {
            // Nothing at all: no device, no passkey, no phone, no verified email, no recovery code. Report it
            // plainly so the client can route to enrollment rather than pushing a challenge nothing can answer.
            telemetry.RecordAuthAttempt("password", "no_second_factor");
            return new PasswordLoginResult("failed", "no_second_factor");
        }

        var pollToken = Base64Url(RandomNumberGenerator.GetBytes(32));
        var contextJson = JsonSerializer.Serialize(new
        {
            ip,
            ua = userAgent,
            poll_hash = TokenService.Sha256(pollToken),
            remember = rememberBrowser,
            browser = browserContext.Browser,
            os = browserContext.OperatingSystem,
            loc = browserContext.ApproxLocation,
        });

        // Device approval stays the preferred web path and keeps its existing shape exactly: the challenge is
        // pushed immediately and the client waits on /status. That ceremony — match number, signed nonce,
        // first-approval-wins — is untouched.
        if (methods[0].Method == MethodTrustedDevice)
        {
            var challenge = await challenges.IssueAsync(user.Id, AuthChallengePurpose.Login, contextJson, ct);
            await PushToDevicesAsync(user.Id, challenge, ip, userAgent, ct);
            telemetry.RecordAuthAttempt("password", "challenge_issued");
            return new PasswordLoginResult("device_approval",
                ChallengeId: challenge.ChallengeId, PollToken: pollToken,
                MatchNumber: challenge.MatchNumber, ExpiresAt: challenge.ExpiresAt, Methods: methods);
        }

        // Otherwise the client chooses. The challenge carries the same poll-token binding, under its own
        // purpose so a second-factor continuation can never be replayed against a device-approval login
        // (the D-098 rule, applied to a new ceremony).
        var pending = await challenges.IssueAsync(user.Id, AuthChallengePurpose.SecondFactor, contextJson, ct);
        telemetry.RecordAuthAttempt("password", "second_factor_offered");
        return new PasswordLoginResult("second_factor",
            ChallengeId: pending.ChallengeId, PollToken: pollToken,
            ExpiresAt: pending.ExpiresAt, Methods: methods);
    }

    // Machine keys the clients switch on. Deliberately constants rather than an enum crossing the wire:
    // the contract is the string, and a renamed enum member must not silently change it.
    private const string MethodTrustedDevice = "trusted_device";
    private const string MethodPasskey = "passkey";
    private const string MethodSmsOtp = "sms_otp";
    private const string MethodEmailOtp = "email_otp";
    private const string MethodRecoveryCode = "recovery_code";

    /// <summary>
    /// The second factors this account can actually use, ranked for the calling surface (D-283).
    /// </summary>
    /// <remarks>
    /// Availability and ranking both live here so all three clients agree by construction. Only available
    /// methods are returned — an unavailable one is absent rather than disabled, so a client cannot leak the
    /// difference through a greyed-out button.
    ///
    /// <para>Ranking differs by surface for a real reason, not a cosmetic one. On the web the approving phone
    /// is a separate physical object, so device approval is the strongest factor available and leads. On a
    /// handset the trusted device is usually the very device signing in, which is not a second factor at all,
    /// so a delivered code leads there. Email outranks SMS once the address is verified: it is not
    /// SIM-swappable and costs roughly a thirtieth as much.</para>
    /// </remarks>
    private async Task<IReadOnlyList<SecondFactorMethod>> AvailableSecondFactorsAsync(
        User user, AuthSurface surface, CancellationToken ct)
    {
        var hasTrustedDevice = await db.TrustedDevices.AnyAsync(
            d => d.UserId == user.Id && d.DeletedAt == null && d.LifecycleState == DeviceLifecycleState.Trusted, ct);

        // A passkey is a WebAuthn credential on one of this user's live devices.
        var hasPasskey = await db.DeviceCredentials.AsNoTracking()
            .Where(c => c.CredentialType == DeviceCredentialType.WebAuthn && c.RevokedAt == null)
            .Join(db.TrustedDevices.Where(d => d.UserId == user.Id && d.DeletedAt == null),
                c => c.TrustedDeviceId, d => d.Id, (c, d) => c.Id)
            .AnyAsync(ct);

        var hasPhone = !string.IsNullOrWhiteSpace(user.Phone);
        // Verified only, always (D-282): an unverified address may belong to somebody else, so sending a
        // login factor to it would be an account-takeover primitive rather than a convenience.
        var hasVerifiedEmail = user.EmailVerifiedAt is not null && !string.IsNullOrWhiteSpace(user.Email);
        var hasRecoveryCode = await db.RecoveryCodes.AnyAsync(r => r.UserId == user.Id && r.UsedAt == null, ct);

        var mobile = surface == AuthSurface.Mobile;
        var methods = new List<SecondFactorMethod>();

        if (hasTrustedDevice)
            methods.Add(new SecondFactorMethod(MethodTrustedDevice, mobile ? 30 : 10,
                "Approve on a trusted device", null));
        if (hasPasskey)
            methods.Add(new SecondFactorMethod(MethodPasskey, mobile ? 40 : 20, "Use a passkey", null));
        if (hasVerifiedEmail)
            methods.Add(new SecondFactorMethod(MethodEmailOtp, mobile ? 10 : 30,
                "Email me a code", MaskEmail(user.Email!)));
        if (hasPhone)
            // Masked from the E.164 form so the hint reads the same regardless of whether this row has been
            // through the D-089 backfill.
            methods.Add(new SecondFactorMethod(MethodSmsOtp, mobile ? 20 : 40,
                "Text me a code", MaskPhone(PhoneDestination(user))));
        if (hasRecoveryCode)
            methods.Add(new SecondFactorMethod(MethodRecoveryCode, 50, "Use a recovery code", null));

        return methods.OrderBy(m => m.Rank).ToList();
    }

    public async Task<ServiceResult<bool>> SendSecondFactorCodeAsync(Guid challengeId, string continuationToken,
        string method, string? ip, CancellationToken ct = default)
    {
        var (challenge, user, error) = await ResolveSecondFactorAsync(challengeId, continuationToken, ct);
        if (error is not null) return new ServiceResult<bool>(false, error);

        // Re-derived, never trusted from the request: a client asking for a method this account does not hold
        // must be refused, not silently downgraded to one it does.
        var available = await AvailableSecondFactorsAsync(user!, AuthSurface.Web, ct);
        if (!available.Any(m => m.Method == method))
            return new ServiceResult<bool>(false, "method_unavailable");

        var (destination, purpose) = method switch
        {
            MethodSmsOtp => (PhoneDestination(user!), OtpPurpose.PhoneVerification),
            MethodEmailOtp => (user!.Email!, OtpPurpose.EmailLogin),
            // trusted_device / passkey / recovery_code are not code-delivered; they have their own ceremonies.
            _ => (null, OtpPurpose.PhoneVerification),
        };
        if (destination is null) return new ServiceResult<bool>(false, "method_not_code_based");

        // Cooldown off, same reason and same policy as the legacy login path: this is the *first* code of
        // this ceremony, but the destination may have received one moments ago from registration or an
        // earlier sign-in, and refusing the user's actual login for that would be a UX failure rather than a
        // security control. The per-destination cap (3 per 10 min) and the per-IP shield both still apply,
        // and they are the limits that actually bound enumeration.
        var issued = await otp.IssueAsync(destination, OtpChannelPolicy.For(purpose), purpose, user!.Id, ip, ct,
            destinationIsCanonical: method == MethodSmsOtp, enforceResendCooldown: false);
        if (!issued.Ok)
        {
            telemetry.RecordAuthAttempt("second_factor", "send_failed");
            return new ServiceResult<bool>(false, issued.Error);
        }

        // Bind the code to THIS challenge. Without it, verification accepted any live code for the same
        // (destination, purpose) — and PhoneVerification is shared with the legacy passwordless login and
        // the phone-change ceremony, so a code minted elsewhere satisfied the second factor. That never
        // bypassed authentication (the caller still needs the password and the phone), but it meant this
        // step proved possession of the phone rather than participation in this login. Recording the id
        // makes the two the same claim.
        //
        // A re-send overwrites it, which is correct: the newest code is the one the user is looking at.
        // Computed before the call, not inside the SetProperty lambda: EF must be able to send this as a
        // parameter, and a method call in the value expression is not reliably translated.
        var boundContext = WithOtpId(challenge!.ContextJson, issued.OtpId!.Value);
        await db.AuthChallenges
            .Where(c => c.Id == challenge.Id && c.Status == AuthChallengeStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ContextJson, boundContext), ct);

        telemetry.RecordAuthAttempt("second_factor", "code_sent");
        return new ServiceResult<bool>(true, Value: true);
    }

    /// <summary>Adds (or replaces) <c>otp_id</c> on a challenge's context, preserving everything already
    /// there — the poll hash, the remember flag and the browser forensics all still have to survive.</summary>
    private static string WithOtpId(string? contextJson, Guid otpId)
    {
        var fields = new Dictionary<string, JsonElement>();
        if (!string.IsNullOrWhiteSpace(contextJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(contextJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    foreach (var p in doc.RootElement.EnumerateObject())
                        fields[p.Name] = p.Value.Clone();
            }
            catch (JsonException) { /* unreadable context is replaced rather than propagated */ }
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in fields)
            {
                if (name == "otp_id") continue;      // superseded below
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }
            writer.WriteString("otp_id", otpId);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The OTP id this challenge last sent, or null if it has sent none.</summary>
    private static Guid? BoundOtpId(string? contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(contextJson);
            return doc.RootElement.TryGetProperty("otp_id", out var v) && v.TryGetGuid(out var id) ? id : null;
        }
        catch (JsonException) { return null; }
    }

    public async Task<LoginStatusResult> VerifySecondFactorCodeAsync(Guid challengeId, string continuationToken,
        string code, CancellationToken ct = default)
    {
        var (challenge, user, error) = await ResolveSecondFactorAsync(challengeId, continuationToken, ct);
        if (error is not null) return new LoginStatusResult(error == "expired" ? "expired" : "pending");

        // Nothing to verify against: /second-factor/send was never called for this challenge.
        var bound = BoundOtpId(challenge!.ContextJson);
        if (bound is null)
        {
            telemetry.RecordAuthAttempt("second_factor", "no_code_requested");
            return new LoginStatusResult("pending");
        }

        // Try both code-bearing purposes: the client does not tell us which method it used, and the code is
        // single-use under exactly one of them. Phone first because it is the more common path.
        Guid? verifiedOtpId = null;
        if (!string.IsNullOrWhiteSpace(user!.Phone))
        {
            var r = await otp.VerifyAsync(PhoneDestination(user), OtpChannel.Sms, OtpPurpose.PhoneVerification,
                code, ct, destinationIsCanonical: true);
            if (r.Ok) verifiedOtpId = r.OtpId;
        }
        if (verifiedOtpId is null && user.EmailVerifiedAt is not null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var r = await otp.VerifyAsync(user.Email, OtpChannel.Email, OtpPurpose.EmailLogin, code, ct);
            if (r.Ok) verifiedOtpId = r.OtpId;
        }

        // The code must be the one THIS challenge sent (F5). A correct code minted by another ceremony —
        // the legacy passwordless login, or a phone-change verification — is refused here even though it
        // is genuinely this user's code, because it is not an answer to this login.
        if (verifiedOtpId is null || verifiedOtpId != bound)
        {
            telemetry.RecordAuthAttempt("second_factor",
                verifiedOtpId is null ? "invalid_code" : "code_not_bound_to_challenge");
            return new LoginStatusResult("pending");   // the OTP platform owns the attempt cap
        }

        // Consume the challenge with a conditional UPDATE, exactly as the device path does: two callers
        // presenting the same code concurrently must not both mint a session.
        var claimed = await db.AuthChallenges
            .Where(c => c.Id == challenge!.Id && c.Status == AuthChallengeStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, AuthChallengeStatus.Consumed)
                .SetProperty(c => c.ConsumedAt, DateTime.UtcNow), ct);
        if (claimed == 0) return new LoginStatusResult("consumed");

        var (issuedTokens, _) = await IssueBrowserSessionAsync(user, ct, "login.second_factor_otp",
            "auth.login_second_factor_otp");

        // "Remember this browser" (F3). The flag was captured onto the challenge at password time and was
        // honoured only on the device-approval leg, so ticking the box on this path did nothing at all and
        // the user had no way to tell. Issued after the session commits, exactly as the device path does, so
        // a trusted-browser row is never created for a login that failed to mint a session.
        string? browserToken = null;
        DateTime? browserExpires = null;
        if (RememberRequested(challenge.ContextJson))
        {
            var browser = await browsers.IssueAsync(user.Id, BrowserContextFrom(challenge.ContextJson), ct);
            browserToken = browser.Token;
            browserExpires = browser.ExpiresAt;
        }

        telemetry.RecordAuthAttempt("second_factor", "success");
        return new LoginStatusResult("approved", issuedTokens, user.Id, browserToken, browserExpires);
    }

    /// <summary>Shared guard for the two second-factor calls: the challenge must exist, be this ceremony's
    /// purpose, still be pending, not have expired, and the caller must hold the continuation token whose hash
    /// is on it. Returns the error code to surface, or null on success.</summary>
    private async Task<(AuthChallenge? Challenge, User? User, string? Error)> ResolveSecondFactorAsync(
        Guid challengeId, string continuationToken, CancellationToken ct)
    {
        var challenge = await db.AuthChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == challengeId, ct);
        if (challenge is null || challenge.Purpose != AuthChallengePurpose.SecondFactor)
            return (null, null, "not_found");
        if (!PollTokenMatches(challenge.ContextJson, continuationToken))
            return (null, null, "not_found");     // same shape as a missing challenge — never confirms one exists
        if (challenge.Status != AuthChallengeStatus.Pending)
            return (null, null, "consumed");
        if (challenge.ExpiresAt <= DateTime.UtcNow)
            return (null, null, "expired");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == challenge.UserId, ct);
        if (user is null || user.BannedAt is not null || user.SuspendedAt is not null)
            return (null, null, "not_found");

        return (challenge, user, null);
    }

    /// <summary>The OTP destination for this user's phone, in E.164.
    ///
    /// <para>Prefers the canonical <c>PhoneE164</c> column and falls back to the legacy digits with a '+'
    /// for rows the D-089 backfill has not reached. The two are the same string whenever the number parses
    /// — <c>ApplyCanonicalPhone</c> derives E164 from exactly that expression — so this changes the *source*
    /// of truth without changing the key, which matters because the destination is the issue/verify lookup
    /// key and the rate-limit partition. Issue and verify both call this, so they cannot drift.</para></summary>
    private static string PhoneDestination(User user)
        => string.IsNullOrWhiteSpace(user.PhoneE164) ? "+" + user.Phone : user.PhoneE164!;

    /// <summary>Last four digits only. Enough for the owner to recognise their own number, useless to anyone
    /// who does not already know it.</summary>
    private static string MaskPhone(string phone)
        => phone.Length <= 4 ? "••••" : "•••• " + phone[^4..];

    /// <summary>First character plus the domain — the shape people recognise from every other login screen.</summary>
    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "•••";
        var local = email[..at];
        return $"{local[0]}{new string('•', Math.Min(local.Length - 1, 5))}{email[at..]}";
    }

    /// <summary>Issues a device-less session for the password + trusted-browser path (factor 1 + factor 2).
    /// The refresh token is a plain bearer (no PoP): there is no device to constrain it to, and the browser
    /// cookie is what constrains the *next* login.</summary>
    private async Task<(AuthTokens Tokens, Guid UserId)> IssueBrowserSessionAsync(User user, CancellationToken ct,
        string securityEventType = "login.password_browser", string auditAction = "auth.login_password_browser")
    {
        var session = new AuthSession { UserId = user.Id, TrustedDeviceId = null };
        db.AuthSessions.Add(session);
        var (access, accessExpires) = tokens.CreateAccessToken(user);
        var (rawRefresh, refreshHash, refreshExpires) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = refreshHash, ExpiresAt = refreshExpires, SessionId = session.Id,
        });
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = user.Id, Type = securityEventType, Severity = "info",
            ContextJson = $"{{\"sessionId\":\"{session.Id}\"}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = user.Id, Action = auditAction,
            Entity = "users", EntityId = user.Id,
        });
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "login.succeeded",
            PayloadJson = $"{{\"userId\":\"{user.Id}\",\"sessionId\":\"{session.Id}\"}}",
            IdempotencyKey = $"login.succeeded:{session.Id}",
        });
        await db.SaveChangesAsync(ct);
        return (new AuthTokens(access, accessExpires, rawRefresh, refreshExpires), user.Id);
    }

    public async Task<IReadOnlyList<PendingLoginView>> ListPendingAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await db.AuthChallenges.AsNoTracking()
            .Where(c => c.UserId == userId && c.Purpose == AuthChallengePurpose.Login
                        && c.Status == AuthChallengeStatus.Pending && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new PendingLoginView(c.Id, c.Nonce, c.MatchNumber, c.ContextJson, c.ExpiresAt))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<bool>> ApproveAsync(Guid userId, Guid challengeId, Guid deviceId, string signatureBase64,
        int? matchNumber, CancellationToken ct = default)
    {
        var challenge = await db.AuthChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == challengeId, ct);
        if (challenge is null || challenge.UserId != userId || challenge.Purpose != AuthChallengePurpose.Login)
            return new ServiceResult<bool>(false, "not_found");

        var device = await db.TrustedDevices.AsNoTracking().FirstOrDefaultAsync(
            d => d.Id == deviceId && d.UserId == userId && d.DeletedAt == null, ct);
        if (device is null)
            return new ServiceResult<bool>(false, "not_found");
        if (device.LifecycleState != DeviceLifecycleState.Trusted)
            return new ServiceResult<bool>(false, "device_not_trusted");

        var result = await challenges.VerifySignatureAsync(challengeId, deviceId, signatureBase64,
            AuthChallengePurpose.Login, matchNumber, ct);
        if (!result.Ok)
            return new ServiceResult<bool>(false, result.Error);

        // Tell the waiting browser immediately instead of making it wait out a poll interval (AM9).
        // Status only — it still calls /status to collect the session.
        await NotifyWatchersAsync(challengeId, "approved", ct);
        return new ServiceResult<bool>(true, Value: true);
    }

    /// <summary>A realtime push is a UX accelerator, never the source of truth — polling remains the
    /// required fallback, so a broadcast failure must not fail the approval itself.</summary>
    private async Task NotifyWatchersAsync(Guid challengeId, string status, CancellationToken ct)
    {
        try
        {
            await realtime.BroadcastLoginStatusAsync(challengeId, status, ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Login status broadcast failed for challenge {ChallengeId}", challengeId);
        }
    }

    public async Task<ServiceResult<bool>> RejectAsync(Guid userId, Guid challengeId, CancellationToken ct = default)
    {
        var challenge = await db.AuthChallenges.FirstOrDefaultAsync(c => c.Id == challengeId, ct);
        if (challenge is null || challenge.UserId != userId)
            return new ServiceResult<bool>(false, "not_found");
        if (challenge.Status != AuthChallengeStatus.Pending)
            return new ServiceResult<bool>(false, "challenge_consumed");

        challenge.Status = AuthChallengeStatus.Rejected;
        challenge.ConsumedAt = DateTime.UtcNow;
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "login.rejected", Severity = "warning",
            ContextJson = $"{{\"challengeId\":\"{challengeId}\"}}",
        });
        await db.SaveChangesAsync(ct);
        await NotifyWatchersAsync(challengeId, "rejected", ct);
        return new ServiceResult<bool>(true, Value: true);
    }

    public async Task<LoginStatusResult> StatusAsync(Guid challengeId, string pollToken, CancellationToken ct = default)
    {
        var challenge = await db.AuthChallenges.FirstOrDefaultAsync(c => c.Id == challengeId, ct);
        if (challenge is null)
            return new LoginStatusResult("pending");            // decoy / unknown — indistinguishable

        // Bind the poller: only the client that started this login may collect its tokens.
        if (!PollTokenMatches(challenge.ContextJson, pollToken))
            return new LoginStatusResult("pending");

        var now = DateTime.UtcNow;
        switch (challenge.Status)
        {
            case AuthChallengeStatus.Rejected:
                return new LoginStatusResult("rejected");
            case AuthChallengeStatus.Consumed:
                return new LoginStatusResult("consumed");
            case AuthChallengeStatus.Expired:
                return new LoginStatusResult("expired");
            case AuthChallengeStatus.Pending when challenge.ExpiresAt <= now:
                challenge.Status = AuthChallengeStatus.Expired;
                await db.SaveChangesAsync(ct);
                return new LoginStatusResult("expired");
            case AuthChallengeStatus.Pending:
                return new LoginStatusResult("pending");
            case AuthChallengeStatus.Approved:
                var (issuedTokens, userId, browserToken, browserExpires) = await IssueSessionAsync(challenge, ct);
                telemetry.RecordAuthAttempt("device", "success");
                return new LoginStatusResult("approved", issuedTokens, userId, browserToken, browserExpires);
            default:
                return new LoginStatusResult("pending");
        }
    }

    /// <summary>Mints the device-bound session exactly once (challenge → Consumed in the same transaction).</summary>
    private async Task<(AuthTokens Tokens, Guid UserId, string? BrowserToken, DateTime? BrowserExpires)> IssueSessionAsync(AuthChallenge challenge, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == challenge.UserId, ct);
        var session = new AuthSession { UserId = user.Id, TrustedDeviceId = challenge.ApprovedByDeviceId };
        db.AuthSessions.Add(session);

        // Pin the approving device's key to the session: from here on only that device can rotate the
        // refresh token (sender-constrained refresh, AM5/D-081).
        var popThumbprint = await db.DeviceCredentials.AsNoTracking()
            .Where(c => c.TrustedDeviceId == challenge.ApprovedByDeviceId && c.RevokedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.PublicKeySpki)
            .FirstOrDefaultAsync(ct) is { } spki ? DeviceSignatures.Thumbprint(spki) : null;

        var (access, accessExpires) = tokens.CreateAccessToken(user);
        var (rawRefresh, refreshHash, refreshExpires) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = refreshExpires,
            SessionId = session.Id,
            PoPKeyThumbprint = popThumbprint,
        });

        challenge.Status = AuthChallengeStatus.Consumed;
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = user.Id, Type = "login.approved", Severity = "info",
            ContextJson = $"{{\"deviceId\":\"{challenge.ApprovedByDeviceId}\",\"sessionId\":\"{session.Id}\"}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = user.Id, Action = "auth.login_device_approved",
            Entity = "users", EntityId = user.Id,
        });
        // Same transaction as the session (ADR-AM16): the "new sign-in" alert cannot be lost to a push
        // outage, which is precisely the alert a victim needs when it wasn't them.
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "login.approved",
            PayloadJson = $"{{\"userId\":\"{user.Id}\",\"sessionId\":\"{session.Id}\"}}",
            IdempotencyKey = $"login.approved:{session.Id}",
        });
        await db.SaveChangesAsync(ct);

        // "Remember this browser" (2B): honored only after a full device approval, from the flag the
        // password-login step wrote onto the challenge. Issued after the session commit so a trusted-browser
        // row is never created for a login that failed to mint a session.
        string? browserToken = null;
        DateTime? browserExpires = null;
        if (RememberRequested(challenge.ContextJson))
        {
            var issued = await browsers.IssueAsync(user.Id, BrowserContextFrom(challenge.ContextJson), ct);
            browserToken = issued.Token;
            browserExpires = issued.ExpiresAt;
        }
        return (new AuthTokens(access, accessExpires, rawRefresh, refreshExpires), user.Id, browserToken, browserExpires);
    }

    private async Task PushToDevicesAsync(Guid userId, ChallengeIssued challenge, string? ip, string? ua, CancellationToken ct)
    {
        var fcmTokens = await db.Devices.AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => d.FcmToken)
            .ToListAsync(ct);

        var data = new Dictionary<string, string>
        {
            ["type"] = "login_approval",
            ["challenge_id"] = challenge.ChallengeId.ToString(),
            ["match_number"] = challenge.MatchNumber.ToString(),
            ["expires_at"] = challenge.ExpiresAt.ToString("o"),
            ["ip"] = ip ?? "",
            ["ua"] = ua ?? "",
        };

        foreach (var token in fcmTokens)
        {
            try
            {
                await push.SendAsync(token, "Approve sign-in?",
                    $"Tap to approve. Match number: {challenge.MatchNumber}", data, ct);
            }
            catch (Exception ex)
            {
                // A failed push must not fail the login start — the app can still poll /login/pending.
                log.LogWarning(ex, "Login-approval push failed for user {UserId}", userId);
            }
        }
    }

    private Task<User?> ResolveUserAsync(string identifier, CancellationToken ct)
        => AuthIdentifiers.ResolveUserAsync(db, identifier, ct);

    public async Task<bool> CanWatchAsync(Guid challengeId, string pollToken, CancellationToken ct = default)
    {
        var challenge = await db.AuthChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == challengeId, ct);
        return challenge is not null && PollTokenMatches(challenge.ContextJson, pollToken);
    }

    /// <summary>The poll-token binding rule (D-080), in one place — the HTTP poll and the realtime
    /// subscription must never disagree about who is allowed to watch a login.</summary>
    private static bool PollTokenMatches(string? contextJson, string pollToken)
    {
        var expected = ExtractPollHash(contextJson);
        if (expected is null) return false;
        var actual = TokenService.Sha256(pollToken);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }

    private static string? ExtractPollHash(string? contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(contextJson);
            return doc.RootElement.TryGetProperty("poll_hash", out var v) ? v.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Whether the password-login step asked to remember this browser (stored on the challenge
    /// context so it survives the round-trip to the approving device).</summary>
    private static bool RememberRequested(string? contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(contextJson);
            return doc.RootElement.TryGetProperty("remember", out var v) && v.ValueKind == JsonValueKind.True;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Rebuilds the browser forensic context captured at password-login time, for the trusted-browser
    /// row issued after approval.</summary>
    private static TrustedBrowserContext BrowserContextFrom(string? contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson)) return new TrustedBrowserContext();
        try
        {
            using var doc = JsonDocument.Parse(contextJson);
            var r = doc.RootElement;
            string? S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            return new TrustedBrowserContext(Browser: S("browser"), OperatingSystem: S("os"),
                UserAgent: S("ua"), Ip: S("ip"), ApproxLocation: S("loc"));
        }
        catch (JsonException) { return new TrustedBrowserContext(); }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
