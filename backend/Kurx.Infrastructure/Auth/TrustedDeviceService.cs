using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Auth;

/// <summary>Trusted-device enrollment + lifecycle FSM (AM2, ADR-A3/AM12). Enrollment proves possession:
/// register the public key (PendingVerification), then the device signs the enrollment challenge to reach
/// Trusted. Every transition writes a security_event + audit_log row.</summary>
public class TrustedDeviceService(KurxDbContext db, IChallengeService challenges) : ITrustedDeviceService
{
    public async Task<DeviceEnrollment> BeginEnrollmentAsync(Guid userId, string? name, string platform,
        string publicKeySpki, string alg, string? attestationJson, CancellationToken ct = default)
    {
        var device = new TrustedDevice
        {
            UserId = userId,
            Name = name,
            Platform = platform,
            LifecycleState = DeviceLifecycleState.PendingVerification,
            AttestationJson = attestationJson,
            StateChangedAt = DateTime.UtcNow,
        };
        db.TrustedDevices.Add(device);
        var credential = new DeviceCredential
        {
            TrustedDeviceId = device.Id,
            CredentialType = DeviceCredentialType.DeviceKey,
            PublicKeySpki = publicKeySpki,
            Alg = string.IsNullOrWhiteSpace(alg) ? "ES256" : alg,
        };
        db.DeviceCredentials.Add(credential);
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "device.enroll_started", Severity = "info",
            ContextJson = $"{{\"deviceId\":\"{device.Id}\",\"platform\":\"{platform}\"}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "device.enroll_started",
            Entity = "trusted_devices", EntityId = device.Id,
        });
        await db.SaveChangesAsync(ct);

        var challenge = await challenges.IssueAsync(userId, AuthChallengePurpose.DeviceEnroll,
            $"{{\"deviceId\":\"{device.Id}\"}}", ct);
        return new DeviceEnrollment(device.Id, credential.Id, challenge.ChallengeId, challenge.Nonce, challenge.MatchNumber, challenge.ExpiresAt);
    }

    public async Task<ServiceResult<bool>> CompleteEnrollmentAsync(Guid userId, Guid challengeId, Guid deviceId,
        string signatureBase64, CancellationToken ct = default)
    {
        var device = await db.TrustedDevices.FirstOrDefaultAsync(
            d => d.Id == deviceId && d.UserId == userId && d.DeletedAt == null, ct);
        if (device is null)
            return new ServiceResult<bool>(false, "not_found");
        if (device.LifecycleState != DeviceLifecycleState.PendingVerification)
            return new ServiceResult<bool>(false, "invalid_state");

        // No match number: enrollment happens on the device itself, so there is no second screen showing
        // digits to correlate against. ChallengeService decides this centrally from the purpose.
        var result = await challenges.VerifySignatureAsync(challengeId, deviceId, signatureBase64,
            AuthChallengePurpose.DeviceEnroll, enteredMatchNumber: null, ct);
        if (!result.Ok || result.UserId != userId)
            return new ServiceResult<bool>(false, result.Error ?? "verification_failed");

        device.LifecycleState = DeviceLifecycleState.Trusted;
        device.StateChangedAt = DateTime.UtcNow;
        device.LastSeenAt = DateTime.UtcNow;
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "device.trusted", Severity = "info",
            ContextJson = $"{{\"deviceId\":\"{deviceId}\"}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "device.trusted",
            Entity = "trusted_devices", EntityId = deviceId,
        });
        await db.SaveChangesAsync(ct);
        return new ServiceResult<bool>(true, Value: true);
    }

    public async Task<IReadOnlyList<TrustedDeviceView>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        // Project the enum column, map to string IN MEMORY — never enum.ToString() inside the SQL
        // projection (EF can't translate it; D-061 CI lesson).
        var rows = await db.TrustedDevices.AsNoTracking()
            .Where(d => d.UserId == userId && d.DeletedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Name, d.Platform, d.LifecycleState, d.LastSeenAt, d.CreatedAt })
            .ToListAsync(ct);
        return rows
            .Select(d => new TrustedDeviceView(d.Id, d.Name, d.Platform, d.LifecycleState.ToString(), d.LastSeenAt, d.CreatedAt))
            .ToList();
    }

    public async Task<ServiceResult<bool>> RevokeAsync(Guid userId, Guid deviceId, CancellationToken ct = default)
    {
        var device = await db.TrustedDevices.FirstOrDefaultAsync(
            d => d.Id == deviceId && d.UserId == userId && d.DeletedAt == null, ct);
        if (device is null)
            return new ServiceResult<bool>(false, "not_found");

        var now = DateTime.UtcNow;
        device.LifecycleState = DeviceLifecycleState.Revoked;
        device.StateChangedAt = now;
        await db.DeviceCredentials
            .Where(c => c.TrustedDeviceId == deviceId && c.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.RevokedAt, now), ct);

        // Revoking a device must sign it out, not just stop it enrolling again (AM5) — otherwise a lost
        // phone keeps refreshing its existing session until the refresh token expires.
        var sessionIds = await db.AuthSessions.Where(s => s.TrustedDeviceId == deviceId)
            .Select(s => s.Id).ToListAsync(ct);
        await db.AuthSessions.Where(s => s.TrustedDeviceId == deviceId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.RevokeReason, "device_revoked"), ct);
        await db.RefreshTokens
            .Where(t => t.SessionId != null && sessionIds.Contains(t.SessionId.Value) && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "device.revoked", Severity = "warning",
            ContextJson = $"{{\"deviceId\":\"{deviceId}\",\"sessionsRevoked\":{sessionIds.Count}}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "device.revoked",
            Entity = "trusted_devices", EntityId = deviceId,
        });
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "device.revoked",
            PayloadJson = $"{{\"userId\":\"{userId}\",\"deviceId\":\"{deviceId}\"}}",
            IdempotencyKey = $"device.revoked:{deviceId}:{now.Ticks}",
        });
        await db.SaveChangesAsync(ct);
        return new ServiceResult<bool>(true, Value: true);
    }

    public async Task<IReadOnlyList<AuthSessionView>> ListSessionsAsync(Guid userId, Guid? currentSessionId = null,
        CancellationToken ct = default)
    {
        var rows = await db.AuthSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id, s.TrustedDeviceId, s.CreatedAt, s.LastRotatedAt,
                DeviceName = db.TrustedDevices.Where(d => d.Id == s.TrustedDeviceId).Select(d => d.Name).FirstOrDefault(),
                Platform = db.TrustedDevices.Where(d => d.Id == s.TrustedDeviceId).Select(d => d.Platform).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return rows
            .Select(s => new AuthSessionView(s.Id, s.TrustedDeviceId, s.DeviceName, s.Platform,
                s.Id == currentSessionId, s.CreatedAt, s.LastRotatedAt))
            .ToList();
    }

    public async Task<ServiceResult<bool>> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        // Scoped by userId so one user can never sign another out; unknown ids are 404, not 403 (D-018).
        var session = await db.AuthSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (session is null)
            return new ServiceResult<bool>(false, "not_found");
        if (session.RevokedAt is not null)
            return new ServiceResult<bool>(false, "already_revoked");

        var now = DateTime.UtcNow;
        session.RevokedAt = now;
        session.RevokeReason = "user_revoked";
        await db.RefreshTokens.Where(t => t.SessionId == sessionId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "session.revoked", Severity = "warning",
            ContextJson = $"{{\"sessionId\":\"{sessionId}\"}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "session.revoked",
            Entity = "auth_sessions", EntityId = sessionId,
        });
        await db.SaveChangesAsync(ct);
        return new ServiceResult<bool>(true, Value: true);
    }
}
