using System.Security.Cryptography;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Auth;

/// <summary>Hardened OTP platform (AM1, ADR-A4). Bootstrap/recovery only — trusted-device login never
/// issues an OTP. Codes are CSPRNG, stored HMAC-peppered in <c>otp_codes</c>, single-use, 5-min TTL,
/// attempt-capped, resend-cooldowned, and audited via <c>security_events</c>. Rate limits are durable in
/// Postgres so they survive restarts and are deterministically testable (same rationale as D-005); a Redis
/// velocity layer (ADR-AM14) fronts this in AM8. Runs alongside the legacy <see cref="AuthService"/> OTP
/// path — nothing cuts over until the client migration (AM9), so this is additive.</summary>
// `config` carries the SNS DLT routing values (sender/entity/template ids) — configuration, not
// secrets. The pepper alone goes through ISecretProvider.
public class OtpService(KurxDbContext db, ISmsProvider sms, IWhatsAppSender whatsapp, IEmailSender email,
    ISecretProvider secrets, IConfiguration config, ILogger<OtpService> log) : IOtpService
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    public const int MaxVerifyAttempts = 5;
    public const int ResendCooldownSeconds = 30;
    public const int MaxPerDestinationPer10Min = 3;

    // Pepper makes the stored hash useless without a server-side secret even if the table leaks.
    //
    // Resolved through ISecretProvider with NO fallback (D-115). It previously fell back to a literal
    // committed to this repository, which meant a deploy that forgot OTP_PEPPER silently hashed every
    // code under a publicly-known pepper — the failure was invisible precisely because the fallback
    // worked. Dev keeps zero-config by setting the value explicitly in .env.example / the test host,
    // so the convenience is visible configuration rather than a hidden default.
    //
    // Reading through the provider (not IConfiguration) matters when SECRETS_PROVIDER=aws: startup
    // validation checks Secrets Manager, so use must read the same source or validation proves nothing.
    // PepperVersion supports rotation without invalidating live codes.
    private const int CurrentPepperVersion = 1;

    public async Task<OtpIssueResult> IssueAsync(string destination, OtpChannel channel, OtpPurpose purpose,
        Guid? userId, string? requestIp, CancellationToken ct = default, bool destinationIsCanonical = false,
        bool enforceResendCooldown = true)
    {
        if (!destinationIsCanonical && !TryNormalize(destination, channel, out destination, out var normError))
            return new OtpIssueResult(false, normError);

        var now = DateTime.UtcNow;

        var last = await db.OtpCodes.AsNoTracking()
            .Where(o => o.Destination == destination && o.Purpose == purpose)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (last is not null && enforceResendCooldown)
        {
            var since = (int)(now - last.CreatedAt).TotalSeconds;
            if (since < ResendCooldownSeconds)
                return new OtpIssueResult(false, "resend_cooldown", RetryAfterSeconds: ResendCooldownSeconds - since);
        }

        var tenMinAgo = now.AddMinutes(-10);
        var recent = await db.OtpCodes.CountAsync(
            o => o.Destination == destination && o.Purpose == purpose && o.CreatedAt >= tenMinAgo, ct);
        if (recent >= MaxPerDestinationPer10Min)
            return new OtpIssueResult(false, "rate_limited", RetryAfterSeconds: 600);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var issued = new OtpCode
        {
            UserId = userId,
            Destination = destination,
            Channel = channel,
            Purpose = purpose,
            CodeHash = await HashAsync(code, ct),
            PepperVersion = CurrentPepperVersion,
            RequestIp = requestIp,
            ExpiresAt = now.Add(Ttl),
        };
        db.OtpCodes.Add(issued);
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId,
            Type = "otp.issued",
            Severity = "info",
            ContextJson = $"{{\"channel\":\"{channel}\",\"purpose\":\"{purpose}\"}}",
        });
        await db.SaveChangesAsync(ct);

        // Delivery failure is reported, not swallowed (D-281). This previously returned Ok regardless, so a
        // user whose send failed saw success and waited for a code that was never sent — and no caller could
        // fall back to another channel, because none of them could tell. The row stays committed and simply
        // expires: re-sending the same code after a failure would defeat single-use, and a code delivered
        // after its 5-minute TTL is worse than none.
        if (!await DeliverAsync(destination, channel, purpose, code, ct))
        {
            log.LogWarning("OTP delivery failed dest={Destination} channel={Channel} purpose={Purpose}",
                Mask(destination, channel), channel, purpose);
            return new OtpIssueResult(false, "delivery_failed");
        }

        log.LogInformation("OTP issued dest={Destination} channel={Channel} purpose={Purpose}",
            Mask(destination, channel), channel, purpose);
        return new OtpIssueResult(true, OtpId: issued.Id);
    }

    /// <summary>The India DLT template id registered for this purpose's message body.
    ///
    /// <para>DLT registers a template id <b>per message body</b>, and a single global
    /// <c>SNS_TEMPLATE_ID</c> therefore cannot serve login, password reset, recovery and registration — the
    /// carrier rejects a send whose body does not match the id it was declared under. Resolution is
    /// <c>SNS_TEMPLATE_ID_&lt;PURPOSE&gt;</c> first, falling back to the global value so a single-template
    /// deployment (and every existing test) keeps working unchanged.</para>
    ///
    /// <para>All purposes currently share one body, so one id is correct <i>today</i>; the per-purpose key
    /// exists because the first time they diverge the failure is a silent carrier rejection, not a code
    /// error.</para></summary>
    private string? DltTemplateId(OtpPurpose purpose)
        => config[$"SNS_TEMPLATE_ID_{purpose.ToString().ToUpperInvariant()}"] ?? config["SNS_TEMPLATE_ID"];

    /// <summary>The destination, reduced to what an operator needs and nothing more.
    ///
    /// <para>These lines previously carried the complete phone number or email address at Information level.
    /// Logs are routinely shipped to a third-party aggregator, so that made every log reader's access a
    /// roster of every number that has ever attempted to sign in — the PII-in-logs rule exists for exactly
    /// this. A masked value still supports the only real diagnostic question ("is this the destination I
    /// expect?") because the person asking already knows the number.</para></summary>
    private static string Mask(string destination, OtpChannel channel)
    {
        if (channel == OtpChannel.Email)
        {
            var at = destination.IndexOf('@');
            return at <= 0 ? "•••" : $"{destination[0]}•••{destination[at..]}";
        }
        return PhoneCanonicalizer.Mask(destination);
    }

    public async Task<OtpVerifyResult> VerifyAsync(string destination, OtpChannel channel, OtpPurpose purpose,
        string code, CancellationToken ct = default, bool destinationIsCanonical = false)
    {
        if (!destinationIsCanonical && !TryNormalize(destination, channel, out destination, out var normError))
            return new OtpVerifyResult(false, normError);

        var now = DateTime.UtcNow;
        // AsNoTracking is deliberate: this row is mutated ONLY through the conditional updates below, so
        // there must be no tracked copy that a later SaveChanges could write back over them.
        var otp = await db.OtpCodes.AsNoTracking()
            .Where(o => o.Destination == destination && o.Purpose == purpose && !o.Consumed && o.ExpiresAt > now)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (otp is null)
            return new OtpVerifyResult(false, "otp_not_found");

        // Claim an attempt atomically. Read-compare-then-increment let N parallel guesses all observe the
        // same count and all write count+1, so the cap advanced roughly once per BATCH rather than once
        // per guess — with enough concurrency a 5-attempt limit over a 6-digit keyspace stops bounding
        // anything. The WHERE is the proof we were under the cap; 0 rows means we were not. Same shape as
        // ChallengeService.VerifyMatchNumberAsync, whose comment describes this identical hazard.
        var attemptClaimed = await db.OtpCodes
            .Where(o => o.Id == otp.Id && o.VerifyAttempts < MaxVerifyAttempts)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.VerifyAttempts, o => o.VerifyAttempts + 1), ct);
        if (attemptClaimed == 0)
            return new OtpVerifyResult(false, "too_many_attempts");

        // Hash with the pepper version the code was minted under (rotation-safe). The attempt is already
        // durably counted at this point, so a wrong guess costs one whether or not anything later throws.
        if (otp.CodeHash != await HashAsync(code, ct, otp.PepperVersion))
            return new OtpVerifyResult(false, "invalid_code");

        // Single-use, claimed the same way. Assigning Consumed on a tracked entity let two callers
        // presenting the same correct code concurrently both succeed — one OTP, two sessions.
        var consumed = await db.OtpCodes
            .Where(o => o.Id == otp.Id && !o.Consumed)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Consumed, true), ct);
        if (consumed == 0)
            return new OtpVerifyResult(false, "otp_not_found");   // lost the race; it is spent

        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = otp.UserId,
            Type = "otp.verified",
            Severity = "info",
            ContextJson = $"{{\"channel\":\"{channel}\",\"purpose\":\"{purpose}\"}}",
        });
        await db.SaveChangesAsync(ct);
        return new OtpVerifyResult(true, OtpId: otp.Id);
    }

    /// <summary>Hands the code to the channel's provider. Returns false when the send did not happen, so
    /// <see cref="IssueAsync"/> can tell the caller rather than claiming success (D-281).
    ///
    /// <para>Provider exceptions are caught here rather than propagated: a provider outage is an expected
    /// operational condition on this path, and letting it escape would turn "we could not text you" into a
    /// 500 that leaks which provider is down. <see cref="ISmsProvider"/> already reports failure in-band and
    /// never throws, so its result is used directly.</para></summary>
    private async Task<bool> DeliverAsync(string destination, OtpChannel channel, OtpPurpose purpose,
        string code, CancellationToken ct)
    {
        var text = $"Your Kurx code is {code}. It expires in 5 minutes.";
        try
        {
            switch (channel)
            {
                case OtpChannel.Sms:
                    var result = await sms.SendAsync(destination, text, new SmsSendOptions(
                        SenderId: config["SNS_SENDER_ID"], EntityId: config["SNS_ENTITY_ID"],
                        TemplateId: DltTemplateId(purpose)), ct);
                    return result.Ok;
                case OtpChannel.WhatsApp:
                    await whatsapp.SendTextAsync(destination, text, ct);
                    return true;
                case OtpChannel.Email:
                    await email.SendAsync(destination, "Your Kurx code", $"<p>{text}</p>", null, ct);
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "OTP delivery threw on channel {Channel}", channel);
            return false;
        }
    }

    /// <summary>Throws <see cref="MissingSecretException"/> when OTP_PEPPER is unset — deliberately.
    /// Issuing a code under a guessable pepper is worse than not issuing one.</summary>
    private async Task<string> HashAsync(string code, CancellationToken ct,
        int pepperVersion = CurrentPepperVersion)
    {
        // Single pepper today; the version column lets a future rotation resolve the right key here.
        _ = pepperVersion;
        var pepper = await secrets.GetRequiredAsync("OTP_PEPPER", ct);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
    }

    private static bool TryNormalize(string destination, OtpChannel channel, out string normalized, out string? error)
    {
        error = null;
        if (channel == OtpChannel.Email)
        {
            normalized = destination.Trim().ToLowerInvariant();
            if (normalized.Length < 3 || !normalized.Contains('@'))
            {
                error = "invalid_email";
                return false;
            }
            return true;
        }

        if (!PhoneCanonicalizer.TryToE164(destination, out normalized))
        {
            error = "invalid_phone";
            return false;
        }
        return true;
    }
}
