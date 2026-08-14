using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

// ════════════════════════════════════════════════════════════════════════════
// Trusted Device Authentication substrate (AM0).
//
// A standalone Identity & Access aggregate — NOT mixed with the User profile or any
// business domain (ADR-AM11). These tables are live: the trusted-device rails (AM2-AM7)
// read and write them on every enrollment, login approval, step-up and recovery.
//
// Ids are UUID v7 (Guid.CreateVersion7(), ADR-AM13) for index locality on high-insert
// tables. Existing tables (users/refresh_tokens) keep their v4 ids — not rewritten.
// Enums persist as text via the KurxDbContext convention.
// Design: docs/auth/AUTHENTICATION_DATABASE.md §2.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>A device a user has enrolled for trusted-device auth. Its trust is an explicit
/// lifecycle FSM (<see cref="DeviceLifecycleState"/>, ADR-AM12), never a bare bool. Public key
/// material lives in the child <see cref="DeviceCredential"/> rows — the device keeps the private
/// key in Android Keystore / iOS Secure Enclave and it never leaves the device.</summary>
public class TrustedDevice
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public string? Name { get; set; }                       // user-facing label ("Pixel 8")
    public string Platform { get; set; } = null!;           // android | ios | web
    public DeviceLifecycleState LifecycleState { get; set; } = DeviceLifecycleState.PendingRegistration;
    public string? AttestationJson { get; set; }            // jsonb — Play Integrity / DeviceCheck payload
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }
    public DateTime? StateChangedAt { get; set; }           // set on every FSM transition
    public DateTime? DeletedAt { get; set; }                // soft delete
}

/// <summary>A rotatable public key bound to a <see cref="TrustedDevice"/>. Covers both rails
/// (ADR-A3): the custom device-key rail and WebAuthn/passkey credentials. The server stores only
/// the public key; the private key is non-exportable on the device.</summary>
public class DeviceCredential
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TrustedDeviceId { get; set; }
    public DeviceCredentialType CredentialType { get; set; } = DeviceCredentialType.DeviceKey;
    public string PublicKeySpki { get; set; } = null!;      // base64 SubjectPublicKeyInfo
    public string Alg { get; set; } = "ES256";              // COSE/JOSE alg id
    public string? WebAuthnCredentialId { get; set; }       // set only for WebAuthn credentials
    public long SignatureCounter { get; set; }              // WebAuthn clone-detection counter
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}

/// <summary>A single-use challenge the device signs to prove possession (login / step-up / enrollment).
/// The durable audit copy lives here; the hot nonce/status also lives in Redis (ADR-AM14). Context is
/// shown to the user on approval (IP/geo/UA/timestamp) and defended by number-matching (ADR-A9).</summary>
public class AuthChallenge
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public AuthChallengePurpose Purpose { get; set; } = AuthChallengePurpose.Login;
    public string Nonce { get; set; } = null!;              // CSPRNG, what the device signs
    public string? ContextJson { get; set; }                // jsonb — {ip, geo, ua, ts}
    public int? MatchNumber { get; set; }                   // anti-push-fatigue number match

    /// <summary>Wrong two-digit confirmations entered on the approving device. Capped at
    /// <see cref="MaxMatchAttempts"/>: the match number is only 2 digits (100 possibilities), so without
    /// a cap it is guessable. Exhausting the cap rejects the challenge outright rather than merely
    /// failing the attempt — a wrong code means the person approving is not looking at the browser that
    /// started this login, which is exactly the phishing case this step exists to catch.</summary>
    public int MatchAttempts { get; set; }

    public const int MaxMatchAttempts = 3;

    public AuthChallengeStatus Status { get; set; } = AuthChallengeStatus.Pending;
    public Guid? ApprovedByDeviceId { get; set; }           // the device that signed it
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A first-class, device-bound session (ADR-AM11/A8). Replaces "the refresh token IS the
/// session". Refresh tokens are children; <see cref="FamilyId"/> is the revoke unit — reuse revokes the
/// family, not every session (ADR-A8).</summary>
public class AuthSession
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid? TrustedDeviceId { get; set; }
    public Guid FamilyId { get; set; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRotatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }               // logout | reuse_detected | user_revoked | ...
}

/// <summary>Append-only auth forensics feed (ADR-AM11). Consumed by the risk engine (ADR-AM22) and the
/// admin console. UserId is nullable — some events precede a known user (e.g. enumeration attempts).</summary>
public class SecurityEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? UserId { get; set; }
    public string Type { get; set; } = null!;               // open taxonomy, matches audit_log.Action idiom
    public string Severity { get; set; } = "info";          // info | warning | critical
    public string? ContextJson { get; set; }                // jsonb
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One-time account-recovery code (hashed at rest, ADR-A4/A11). Consumed on use.</summary>
public class RecoveryCode
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = null!;
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Password credential — factor 1 of the target architecture.
///
/// <para>Deliberately a <b>separate table from <c>users</c></b> (ADR-AM11: auth is its own aggregate).
/// <c>users</c> is projected by dozens of queries across orgs, events, orders and admin; putting a
/// password hash on it would put credential material one careless <c>Select</c> away from a response
/// body. Nothing outside the auth module has a reason to join this table.</para>
///
/// <para><see cref="PasswordHash"/> is a full PHC-format Argon2id string (<c>$argon2id$v=19$m=..,t=..,p=..$salt$hash</c>)
/// so the parameters travel with the hash and can be raised later without invalidating existing
/// passwords — the verifier reads them from the string, and a hash below current cost is transparently
/// re-hashed on the next successful sign-in.</para></summary>
public class UserCredential
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    public string PasswordHash { get; set; } = null!;
    public string Algorithm { get; set; } = "argon2id";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Account lockout (OWASP Authentication) ───────────────────────────────
    // Counter lives here rather than in a cache so a lockout survives a restart and is consistent
    // across instances — an in-process counter would reset to zero on every deploy, which is exactly
    // when an attacker mid-spray would benefit.

    public int FailedAttempts { get; set; }

    /// <summary>Set while the account is locked. Null once the window has passed; the verifier clears
    /// it lazily on the next successful attempt rather than needing a sweeper job.</summary>
    public DateTime? LockedUntil { get; set; }

    public DateTime? LastSuccessfulAt { get; set; }
    public DateTime? LastFailedAt { get; set; }
}

/// <summary>Previous password hashes, so a "change" cannot silently be a no-op reuse.
///
/// <para>Stores hashes only — reuse is detected by verifying the *candidate* password against each
/// stored hash, never by comparing hashes to each other (different salts make that meaningless).
/// Bounded to the last N per user; older rows are pruned on write so this cannot grow unbounded.</para></summary>
public class PasswordHistory
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>How many previous passwords are refused on change. NIST 800-63B does not require
    /// rotation, but it does expect a *change* to be a real change — this blocks re-setting the value
    /// the user just abandoned, which is the common case after a suspected compromise.</summary>
    public const int RetainedPerUser = 5;
}

/// <summary>A browser the user chose to trust, satisfying <b>factor 2</b> for a limited window.
///
/// <para>Critical invariant: a trusted browser skips the <b>trusted-device approval</b>, never the
/// <b>password</b>. If it skipped the password, the cookie alone would be a bearer credential for the
/// whole account, and stealing it would be full takeover — strictly worse than the flow it replaces.</para>
///
/// <para>Only <see cref="TokenHash"/> is stored: the raw token lives solely in the browser's cookie,
/// so a database disclosure yields nothing replayable, the same reasoning as refresh tokens.</para></summary>
public class TrustedBrowser
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the opaque cookie token. Never the token itself.</summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>User-editable label. Defaults to a browser/OS summary so the security screen is
    /// readable before anyone renames anything.</summary>
    public string? Label { get; set; }

    public string? Browser { get; set; }                    // "Chrome 140"
    public string? OperatingSystem { get; set; }            // "Windows 11"
    public string? UserAgent { get; set; }                  // full UA, for forensics
    public string? Ip { get; set; }
    public string? ApproxLocation { get; set; }             // coarse, city-level at most

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }               // user_revoked | password_changed | recovery
}

/// <summary>Hardened OTP record (ADR-A4). Bootstrap/recovery only — never trusted-device login.
/// Hot cooldown/replay/attempt counters live in Redis (ADR-AM14); this is the durable, audited copy.
/// Hash is HMAC-SHA256 with a server pepper (<see cref="PepperVersion"/> for rotation). Supersedes the
/// legacy <c>otp_requests</c> table, which AM1 cuts over from.</summary>
public class OtpCode
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? UserId { get; set; }                       // null during first-registration OTP
    public string Destination { get; set; } = null!;        // E.164 phone or email, by Channel
    public OtpChannel Channel { get; set; } = OtpChannel.Sms;
    public OtpPurpose Purpose { get; set; } = OtpPurpose.Registration;
    public string CodeHash { get; set; } = null!;
    public int PepperVersion { get; set; } = 1;
    public string? RequestIp { get; set; }
    public int VerifyAttempts { get; set; }
    public bool Consumed { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Transactional outbox (ADR-AM16). Security-critical events are written in the same
/// transaction as the state change and dispatched at-least-once by a background job (AM8), keyed by
/// <see cref="IdempotencyKey"/> for dedup.</summary>
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Type { get; set; } = null!;               // login.approved | device.revoked | ...
    public string PayloadJson { get; set; } = null!;        // jsonb
    public string IdempotencyKey { get; set; } = null!;
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DispatchedAt { get; set; }
}

/// <summary>An asymmetric JWT signing key and its lifecycle (AM10, D-099).
///
/// <para>Access tokens move from HS256 (one shared secret every service must hold, and which any
/// holder can forge with) to <b>ES256</b>: only this service holds the private half, and anything
/// verifying a token needs the public half only. That is what makes a published JWKS safe and lets
/// other services verify without being trusted to mint.</para>
///
/// <para>The <see cref="SigningKeyState"/> FSM is what makes rotation zero-downtime. Exactly one key
/// is <c>Active</c> and signs; <c>Retiring</c> keys no longer sign but still <b>validate</b>, so
/// tokens minted before a rotation keep working until they expire naturally. A key is only
/// <c>Retired</c> once no live token can still bear it.</para></summary>
public class SigningKey
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The JWT `kid` header. Validation selects the key by this, so a token always names
    /// the key that signed it and rotation never depends on trial-and-error verification.</summary>
    public string KeyId { get; set; } = null!;

    public string Algorithm { get; set; } = "ES256";

    /// <summary>Base64 SubjectPublicKeyInfo — published in JWKS, safe to disclose.</summary>
    public string PublicKeySpki { get; set; } = null!;

    /// <summary>Base64 PKCS#8 private key. Secret. Null once the key is retired, so an expired key
    /// stops being a liability the moment it stops being useful.
    ///
    /// <para>Held in the database today, protected by disk/TDE encryption (D-101 default 6). The
    /// production path is an <c>ISigningKeyProtector</c> wrapping this with AWS KMS so the plaintext
    /// private key never sits in Postgres at all — deliberately deferred rather than faked, because
    /// a KMS integration that has never talked to KMS proves nothing.</para></summary>
    public string? PrivateKeyPkcs8 { get; set; }

    public SigningKeyState State { get; set; } = SigningKeyState.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAt { get; set; }

    /// <summary>When the key stopped signing. Tokens signed before this remain valid until they
    /// expire, which is what bounds the grace period.</summary>
    public DateTime? RetiringAt { get; set; }
    public DateTime? RetiredAt { get; set; }

    /// <summary>Set when a key is pulled for compromise rather than age. A compromised key is
    /// removed from JWKS <b>immediately</b> — no grace period — which deliberately invalidates every
    /// token it signed. That is the point: the alternative is honouring an attacker's forgeries.</summary>
    public DateTime? CompromisedAt { get; set; }
    public string? CompromiseReason { get; set; }
}
