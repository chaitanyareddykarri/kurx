using System.Text;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Auth;

/// <summary>WebAuthn / passkeys (AM3, ADR-A3). The second credential rail: a passkey enrolls as a
/// <see cref="TrustedDevice"/> exactly like a device key does, so everything built on top of that
/// aggregate — "your devices" (AM5), revocation, step-up (AM6), risk (AM8) — works unchanged.
///
/// <para><b>Two storage notes, both deliberate.</b> (1) <c>DeviceCredential.PublicKeySpki</c> holds the
/// COSE-encoded key for WebAuthn rows, not SPKI — the column is the aggregate's generic public-key
/// material and the two rails are told apart by <c>CredentialType</c>; adding a parallel column would
/// have meant a migration for zero behavioral gain. (2) The in-flight ceremony options live in
/// <c>auth_challenges.ContextJson</c> rather than server session state, so the flow is stateless across
/// instances and audited like every other challenge.</para>
///
/// <para><b>No PoP constraint on a passkey session.</b> A WebAuthn authenticator signs WebAuthn
/// assertions, not arbitrary payloads, so it cannot produce the raw-token signature that AM5's
/// sender-constrained refresh (D-081) requires. Passkey sessions are therefore device-bound (they carry
/// a session + trusted device) but not sender-constrained — stating this plainly because silently
/// issuing a token that *looks* constrained and is not would be worse than the gap.</para></summary>
public class PasskeyService(KurxDbContext db, IFido2 fido2, TokenService tokens, IRiskEngine risk,
    ILogger<PasskeyService> log) : IPasskeyService
{
    public async Task<PasskeyOptions> BeginRegistrationAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);

        // Exclude what's already registered so the authenticator refuses to enroll a duplicate.
        var existing = await ExistingDescriptorsAsync(userId, ct);

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User
            {
                Id = userId.ToByteArray(),
                Name = user.Username ?? user.Phone ?? userId.ToString(),
                DisplayName = string.IsNullOrWhiteSpace(user.Name) ? (user.Username ?? "Kurx user") : user.Name,
            },
            ExcludeCredentials = existing,
            AuthenticatorSelection = AuthenticatorSelection.Default,
            AttestationPreference = AttestationConveyancePreference.None,
        });

        var challengeId = await StoreCeremonyAsync(userId, AuthChallengePurpose.PasskeyRegister,
            options.Challenge, options.ToJson(), ct);
        return new PasskeyOptions(challengeId, options.ToJson());
    }

    public async Task<ServiceResult<Guid>> CompleteRegistrationAsync(Guid userId, Guid challengeId,
        string attestationJson, string? deviceName, CancellationToken ct = default)
    {
        var challenge = await LoadPendingAsync(challengeId, AuthChallengePurpose.PasskeyRegister, ct);
        if (challenge is null || challenge.UserId != userId)
            return new ServiceResult<Guid>(false, "not_found");

        AuthenticatorAttestationRawResponse? response;
        try
        {
            response = System.Text.Json.JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return new ServiceResult<Guid>(false, "invalid_attestation");
        }
        if (response is null)
            return new ServiceResult<Guid>(false, "invalid_attestation");

        RegisteredPublicKeyCredential credential;
        try
        {
            credential = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = response,
                OriginalOptions = CredentialCreateOptions.FromJson(challenge.ContextJson!),   // non-null per LoadPendingAsync
                IsCredentialIdUniqueToUserCallback = async (args, token) =>
                {
                    var id = Base64Url(args.CredentialId);
                    return !await db.DeviceCredentials.AnyAsync(c => c.WebAuthnCredentialId == id, token);
                },
            }, ct);
        }
        catch (Exception ex) when (ex is Fido2VerificationException or System.Text.Json.JsonException)
        {
            // Covers a wrong origin, a bad signature, a replayed/duplicate credential — and a
            // challenge whose ContextJson is not WebAuthn options at all. The purpose check in
            // LoadPendingAsync now makes that last case unreachable through the API (D-098), but it
            // is caught anyway: an unparseable stored context must be a clean refusal, never an
            // unhandled 500.
            db.SecurityEvents.Add(new SecurityEvent
            {
                UserId = userId, Type = "passkey.registration_failed", Severity = "warning",
                ContextJson = $"{{\"challengeId\":\"{challengeId}\"}}",
            });
            challenge.Status = AuthChallengeStatus.Rejected;
            challenge.ConsumedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            log.LogWarning(ex, "Passkey registration failed for user {UserId}", userId);
            return new ServiceResult<Guid>(false, "invalid_attestation");
        }

        var now = DateTime.UtcNow;
        var device = new TrustedDevice
        {
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(deviceName) ? "Passkey" : deviceName,
            Platform = "web",
            // A verified attestation *is* the proof of possession, so unlike the device-key rail there is
            // no second round trip — the passkey is Trusted immediately.
            LifecycleState = DeviceLifecycleState.Trusted,
            StateChangedAt = now,
            LastSeenAt = now,
        };
        db.TrustedDevices.Add(device);
        db.DeviceCredentials.Add(new DeviceCredential
        {
            TrustedDeviceId = device.Id,
            CredentialType = DeviceCredentialType.WebAuthn,
            PublicKeySpki = Convert.ToBase64String(credential.PublicKey),   // COSE key — see class remarks
            Alg = "WebAuthn",
            WebAuthnCredentialId = Base64Url(credential.Id),
            SignatureCounter = credential.SignCount,
        });

        challenge.Status = AuthChallengeStatus.Consumed;
        challenge.ConsumedAt = now;
        challenge.ApprovedByDeviceId = device.Id;
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "passkey.registered", Severity = "info",
            ContextJson = $"{{\"deviceId\":\"{device.Id}\"}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "passkey.registered",
            Entity = "trusted_devices", EntityId = device.Id,
        });
        await db.SaveChangesAsync(ct);
        return new ServiceResult<Guid>(true, Value: device.Id);
    }

    public async Task<PasskeyOptions> BeginLoginAsync(string identifier, CancellationToken ct = default)
    {
        var user = await AuthIdentifiers.ResolveUserAsync(db, identifier, ct);
        var descriptors = user is null
            ? []
            : await ExistingDescriptorsAsync(user.Id, ct);

        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = descriptors,
            UserVerification = UserVerificationRequirement.Preferred,
        });

        // Anti-enumeration: an unknown identifier, a moderated account, or a user with no passkey all get
        // well-formed options with nothing that can satisfy them, and no challenge is persisted. The shape
        // is identical, so the response never reveals which case it was.
        if (user is null || user.BannedAt is not null || user.SuspendedAt is not null || descriptors.Count == 0)
            return new PasskeyOptions(Guid.CreateVersion7(), options.ToJson());

        var challengeId = await StoreCeremonyAsync(user.Id, AuthChallengePurpose.PasskeyLogin,
            options.Challenge, options.ToJson(), ct);
        return new PasskeyOptions(challengeId, options.ToJson());
    }

    public async Task<PasskeyLoginResult> CompleteLoginAsync(Guid challengeId, string assertionJson,
        CancellationToken ct = default)
    {
        var challenge = await LoadPendingAsync(challengeId, AuthChallengePurpose.PasskeyLogin, ct);
        if (challenge is null)
            return new PasskeyLoginResult(false, "invalid_assertion");   // decoy challenge ids land here too

        AuthenticatorAssertionRawResponse? response;
        try
        {
            response = System.Text.Json.JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertionJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return new PasskeyLoginResult(false, "invalid_assertion");
        }
        if (response is null)
            return new PasskeyLoginResult(false, "invalid_assertion");

        // RawId, not Id: the raw bytes re-encode to exactly what registration stored.
        var credentialId = Base64Url(response.RawId);
        var credential = await db.DeviceCredentials
            .Where(c => c.WebAuthnCredentialId == credentialId && c.RevokedAt == null)
            .FirstOrDefaultAsync(ct);
        if (credential is null)
            return new PasskeyLoginResult(false, "invalid_assertion");

        var device = await db.TrustedDevices.FirstOrDefaultAsync(
            d => d.Id == credential.TrustedDeviceId && d.DeletedAt == null, ct);
        // The credential must still belong to the challenged user and to a live device — a revoked or
        // suspended passkey (e.g. after an account recovery, D-083) must not sign anyone in.
        if (device is null || device.UserId != challenge.UserId || device.LifecycleState != DeviceLifecycleState.Trusted)
            return new PasskeyLoginResult(false, "invalid_assertion");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == device.UserId, ct);
        if (user is null || user.BannedAt is not null || user.SuspendedAt is not null)
            return new PasskeyLoginResult(false, "invalid_assertion");

        var decision = await risk.EvaluateLoginAsync(user.Id, null, null, ct);
        if (decision.Action == RiskAction.Deny)
        {
            log.LogWarning("Passkey login denied by risk engine for user {UserId} (score {Score})",
                user.Id, decision.Score);
            return new PasskeyLoginResult(false, "invalid_assertion");
        }

        VerifyAssertionResult result;
        try
        {
            result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = response,
                OriginalOptions = AssertionOptions.FromJson(challenge.ContextJson!),          // non-null per LoadPendingAsync
                StoredPublicKey = Convert.FromBase64String(credential.PublicKeySpki),
                StoredSignatureCounter = (uint)credential.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                    Task.FromResult(new Guid(args.UserHandle) == device.UserId),
            }, ct);
        }
        catch (Exception ex) when (ex is Fido2VerificationException or System.Text.Json.JsonException)
        {
            // Includes the cloned-authenticator case: the library refuses a counter that went
            // backwards. Also catches an unparseable stored context, so a malformed challenge is a
            // clean `invalid_assertion` rather than an unhandled 500 (D-098).
            db.SecurityEvents.Add(new SecurityEvent
            {
                UserId = user.Id, Type = "passkey.assertion_failed", Severity = "warning",
                ContextJson = $"{{\"deviceId\":\"{device.Id}\"}}",
            });
            challenge.Status = AuthChallengeStatus.Rejected;
            challenge.ConsumedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            log.LogWarning(ex, "Passkey assertion failed for user {UserId}", user.Id);
            return new PasskeyLoginResult(false, "invalid_assertion");
        }

        var now = DateTime.UtcNow;
        credential.SignatureCounter = result.SignCount;     // clone detection depends on this being stored
        device.LastSeenAt = now;
        challenge.Status = AuthChallengeStatus.Consumed;
        challenge.ConsumedAt = now;
        challenge.ApprovedByDeviceId = device.Id;

        var session = new AuthSession { UserId = user.Id, TrustedDeviceId = device.Id };
        db.AuthSessions.Add(session);
        var (access, accessExpires) = tokens.CreateAccessToken(user);
        var (rawRefresh, refreshHash, refreshExpires) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = refreshHash, ExpiresAt = refreshExpires, SessionId = session.Id,
            // No PoPKeyThumbprint by design — see the class remarks.
        });

        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = user.Id, Type = "login.approved", Severity = "info",
            ContextJson = $"{{\"deviceId\":\"{device.Id}\",\"sessionId\":\"{session.Id}\",\"method\":\"passkey\"}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = user.Id, Action = "auth.login_passkey",
            Entity = "users", EntityId = user.Id,
        });
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "login.approved",
            PayloadJson = $"{{\"userId\":\"{user.Id}\",\"sessionId\":\"{session.Id}\"}}",
            IdempotencyKey = $"login.approved:{session.Id}",
        });
        await db.SaveChangesAsync(ct);

        return new PasskeyLoginResult(true,
            Tokens: new AuthTokens(access, accessExpires, rawRefresh, refreshExpires), UserId: user.Id);
    }

    public async Task<IReadOnlyList<TrustedDeviceView>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.TrustedDevices.AsNoTracking()
            .Where(d => d.UserId == userId && d.DeletedAt == null
                        && db.DeviceCredentials.Any(c => c.TrustedDeviceId == d.Id
                                                        && c.CredentialType == DeviceCredentialType.WebAuthn))
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Name, d.Platform, d.LifecycleState, d.LastSeenAt, d.CreatedAt })
            .ToListAsync(ct);
        // Enum → string in memory, never inside the SQL projection (D-061).
        return rows
            .Select(d => new TrustedDeviceView(d.Id, d.Name, d.Platform, d.LifecycleState.ToString(), d.LastSeenAt, d.CreatedAt))
            .ToList();
    }

    private async Task<List<PublicKeyCredentialDescriptor>> ExistingDescriptorsAsync(Guid userId, CancellationToken ct)
    {
        var ids = await db.DeviceCredentials.AsNoTracking()
            .Where(c => c.CredentialType == DeviceCredentialType.WebAuthn && c.RevokedAt == null
                        && c.WebAuthnCredentialId != null
                        && db.TrustedDevices.Any(d => d.Id == c.TrustedDeviceId && d.UserId == userId && d.DeletedAt == null))
            .Select(c => c.WebAuthnCredentialId!)
            .ToListAsync(ct);
        return ids.Select(id => new PublicKeyCredentialDescriptor(FromBase64Url(id))).ToList();
    }

    /// <summary>Persists an in-flight ceremony as an <c>auth_challenges</c> row so the flow needs no
    /// sticky server session and lands in the same audit trail as every other challenge.</summary>
    private async Task<Guid> StoreCeremonyAsync(Guid userId, AuthChallengePurpose purpose, byte[] challengeBytes,
        string optionsJson, CancellationToken ct)
    {
        var record = new AuthChallenge
        {
            UserId = userId,
            Purpose = purpose,
            Nonce = Base64Url(challengeBytes),
            ContextJson = optionsJson,
            Status = AuthChallengeStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        };
        db.AuthChallenges.Add(record);
        await db.SaveChangesAsync(ct);
        return record.Id;
    }

    private async Task<AuthChallenge?> LoadPendingAsync(Guid challengeId, AuthChallengePurpose purpose, CancellationToken ct)
    {
        var challenge = await db.AuthChallenges.FirstOrDefaultAsync(c => c.Id == challengeId, ct);
        if (challenge is null || challenge.Purpose != purpose || challenge.ContextJson is null)
            return null;
        if (challenge.Status != AuthChallengeStatus.Pending)
            return null;                                    // single-use: a replayed ceremony is refused
        if (challenge.ExpiresAt <= DateTime.UtcNow)
        {
            challenge.Status = AuthChallengeStatus.Expired;
            await db.SaveChangesAsync(ct);
            return null;
        }
        return challenge;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}
