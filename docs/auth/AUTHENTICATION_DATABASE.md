# Kurx Authentication — Database

Every authentication table: columns, relationships, indexes, constraints, migration history.

**Conventions.**
- New auth tables use **UUID v7** (`Guid.CreateVersion7()`, ADR-AM13) for index locality on high-insert
  tables. A deliberate deviation from D-006's Guid-v4 **for new auth tables only** — `users` and
  `refresh_tokens` keep v4 and are not rewritten.
- Enums persist as **text** via the `KurxDbContext` convention, not integers — readable in `psql` and
  immune to reordering.
- JSON columns are **`jsonb`**.
- Money is `long` paise / `bigint` `_paise` (D-004) — not relevant here, listed so nobody reintroduces it.
- **There is no `psql` binary on the dev machine.** Verify schema via EF or the container, or
  `docker exec kurx-postgres psql …`.

Entities: `backend/Kurx.Domain/Entities/AuthIdentity.cs` · Config: `backend/Kurx.Infrastructure/Persistence/KurxDbContext.cs`

---

## 1. Entity relationships

```
users ─┬─1:1── user_credentials      (Cascade)   password + lockout counters
       ├─1:N── password_history      (Cascade)   last 5, pruned on write
       ├─1:N── trusted_browsers      (Cascade)   factor 2 — cookie binding
       ├─1:N── trusted_devices       (Cascade) ──1:N── device_credentials (Cascade)
       ├─1:N── auth_challenges       (Cascade)   ──N:1── trusted_devices (SetNull, ApprovedByDeviceId)
       ├─1:N── auth_sessions         (Cascade)   ──N:1── trusted_devices (SetNull)
       │         └─1:N── refresh_tokens          (SetNull on SessionId)
       ├─1:N── recovery_codes        (Cascade)
       └─0:N── security_events       (SetNull)   UserId NULLABLE — pre-auth events

standalone (no FK):  otp_codes        registration OTPs precede the user row
                     outbox_messages
                     signing_keys
```

**Why the delete behaviours differ.** `Cascade` where the row is meaningless without its user.
`SetNull` where the row is **forensic** and must survive: a `security_event` from an enumeration attempt
has no user, and an `auth_challenge` must not vanish because the approving device was deleted.

---

## 2. Tables

### `user_credentials` — factor 1 *(Phase 1)*

One row per user. **Separate from `users` by design** (ADR-AM11): `users` is projected by dozens of
queries across orgs, events, orders and admin, and a hash on that entity would sit one careless `Select`
away from a response body.

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid v7 | PK |
| `UserId` | uuid | FK → users, **UNIQUE** |
| `PasswordHash` | text | Full PHC string `$argon2id$v=19$m=..,t=..,p=..$salt$hash` |
| `Algorithm` | text | `argon2id` — allows two formats to coexist during any future migration |
| `CreatedAt` / `UpdatedAt` | timestamptz | |
| `FailedAttempts` | int | Consecutive failures |
| `LockedUntil` | timestamptz? | Set while locked; cleared lazily on next success — no sweeper job |
| `LastSuccessfulAt` / `LastFailedAt` | timestamptz? | |

**Indexes:** `UNIQUE (UserId)`

> The uniqueness is **load-bearing, not decorative**: it is what makes a concurrent "set password" race
> safe. The loser gets a `23505`, translated to `password_already_set`, so an account can never end up
> with two credential rows and an ambiguous verifier.

Lockout counters live here rather than in a cache because an in-process counter resets on every deploy —
precisely when an attacker mid-spray benefits.

### `password_history` *(Phase 1)*

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid v7 | PK |
| `UserId` | uuid | FK → users |
| `PasswordHash` | text | PHC string |
| `CreatedAt` | timestamptz | |

**Indexes:** `(UserId, CreatedAt)` · **Cap:** `PasswordHistory.RetainedPerUser = 5`, pruned on write.

Reuse is detected by **verifying the candidate against each stored hash** — comparing hashes directly is
meaningless when each has its own salt.

### `trusted_browsers` — factor 2 *(read/written by `TrustedBrowserService`, Phase 2A)*

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid v7 | PK |
| `UserId` | uuid | FK → users |
| `TokenHash` | text | **SHA-256 of the opaque cookie token. Never the token.** |
| `Label` | text? | User-editable; defaults to a browser/OS summary |
| `Browser` / `OperatingSystem` / `UserAgent` | text? | `"Chrome 140"` / `"Windows 11"` / full UA for forensics |
| `Ip` | text? | |
| `ApproxLocation` | text? | Coarse — city level at most |
| `CreatedAt` / `LastUsedAt` / `ExpiresAt` | timestamptz | Default expiry 30 days |
| `RevokedAt` | timestamptz? | |
| `RevokeReason` | text? | `user_revoked` \| `password_changed` \| `recovery` |

**Indexes:** `UNIQUE (TokenHash)` — lookup is always "find the live browser for this cookie" ·
`(UserId, RevokedAt)`

Only the hash is stored, so a database disclosure yields nothing replayable — the same reasoning as
refresh tokens. `RevokeReason` already anticipates the Phase 3 cascade revocation.

### `trusted_devices` — factor 3 *(AM0)*

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid v7 | PK |
| `UserId` | uuid | FK → users |
| `Name` | text? | `"Pixel 8"` |
| `Platform` | text | `android` \| `ios` \| `web` |
| `LifecycleState` | text | FSM — see below |
| `AttestationJson` | jsonb? | Play Integrity / DeviceCheck payload — **stored, not validated** |
| `CreatedAt` / `LastSeenAt` / `StateChangedAt` / `DeletedAt` | timestamptz? | Soft delete |

**Indexes:** `(UserId, LifecycleState)`

**FSM (ADR-AM12):** `PendingRegistration → PendingVerification → Trusted → Suspended → Revoked →
Compromised → Deleted`. Every transition writes a `security_event` **and** an `audit_log` row.

### `device_credentials` *(AM0)*

Rotatable public keys. Covers both rails.

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid v7 | PK |
| `TrustedDeviceId` | uuid | FK → trusted_devices, Cascade |
| `CredentialType` | text | `DeviceKey` \| WebAuthn |
| `PublicKeySpki` | text | base64 SubjectPublicKeyInfo |
| `Alg` | text | `ES256` |
| `WebAuthnCredentialId` | text? | Only for WebAuthn rows |
| `SignatureCounter` | bigint | WebAuthn clone detection |
| `CreatedAt` / `RevokedAt` | timestamptz? | |

**Indexes:** `(TrustedDeviceId)` · `ix_device_credentials_webauthn` **UNIQUE** on `WebAuthnCredentialId`
**filtered** `WHERE "WebAuthnCredentialId" IS NOT NULL` — a WebAuthn credential id is globally unique,
while device-key rows leave it null and must not collide on it.

**The server stores only public keys.** The private key is non-exportable on the device.

### `auth_challenges` *(AM0, + `MatchAttempts` in Phase 1)*

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid v7 | PK |
| `UserId` | uuid | FK → users, Cascade |
| `Purpose` | text | `Login` \| `StepUp` \| `DeviceEnroll` \| `PasskeyRegister` \| `PasskeyLogin` |
| `Nonce` | text | CSPRNG, 32 bytes base64url — **what the device signs** |
| `ContextJson` | jsonb? | `{ip, geo, ua, ts}` shown to the approver |
| `MatchNumber` | int? | Two-digit anti-fatigue number (10–99) |
| `MatchAttempts` | int | **Wrong digit entries. Capped at `MaxMatchAttempts = 3`.** |
| `Status` | text | `Pending` \| `Approved` \| `Rejected` \| `Expired` \| `Consumed` |
| `ApprovedByDeviceId` | uuid? | FK → trusted_devices, **SetNull** |
| `ExpiresAt` / `ConsumedAt` / `CreatedAt` | timestamptz | Default TTL 120 s (`AUTH_CHALLENGE_TTL_SECONDS`) |

**Indexes:** `UNIQUE (Nonce)` · `(UserId, Status, ExpiresAt)`

The `Status` column is the concurrency-control point: Phase 3's first-approval-wins claim is a
conditional `UPDATE … WHERE Status='Pending'`, relying on Postgres row serialisation.

### `auth_sessions` *(AM0)*

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid v7 | PK |
| `UserId` | uuid | FK → users, Cascade |
| `TrustedDeviceId` | uuid? | FK → trusted_devices, SetNull |
| `FamilyId` | uuid v7 | **The revoke unit** |
| `CreatedAt` / `LastRotatedAt` / `RevokedAt` | timestamptz? | |
| `RevokeReason` | text? | `logout` \| `reuse_detected` \| `user_revoked` \| … |

**Indexes:** `(UserId, FamilyId)` · `(TrustedDeviceId)`

Refresh reuse revokes the **family**, not every session (D-081) — narrower than D-014's revoke-everything,
so one stolen token does not sign you out of every device.

### `refresh_tokens` *(pre-existing, extended in AM5)*

Extended with `SessionId` (FK → auth_sessions, **SetNull**) and `PoPKeyThumbprint`. Tokens are stored
hashed. PoP binding means a stolen refresh string alone cannot rotate.

### `security_events` *(AM0)*

Append-only auth forensics feed. Consumed by the risk engine and operators.

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid v7 | PK |
| `UserId` | uuid? | **NULLABLE** — some events precede a known user |
| `Type` | text | Open taxonomy: `password.created`, `password.changed`, `password.locked_out`, `challenge.signature_invalid`, `challenge.match_number_invalid`, `challenge.match_attempts_exhausted`, `challenge.approved`, `stepup.satisfied`, … |
| `Severity` | text | `info` \| `warning` \| `critical` |
| `ContextJson` | jsonb? | |
| `CreatedAt` | timestamptz | |

**Indexes:** `(UserId, CreatedAt)` · `(Type, CreatedAt)` · **FK SetNull**, not Cascade

⚠ **No retention policy.** Append-only with no pruning — flagged as future work.

**No admin page reads this table by design** — it is operator/CloudWatch-facing. Auth also writes
`audit_log`, which the existing admin Audit page surfaces.

### `recovery_codes` *(AM0)*

`Id` (uuid v7) · `UserId` (FK, Cascade) · `CodeHash` · `UsedAt?` · `CreatedAt`. **Indexes:** `(UserId)`

Hashed at rest, consumed on use. Redemption requires **both** an OTP and a code.

### `otp_codes` *(AM0)*

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid v7 | PK |
| `UserId` | uuid? | **Null during first-registration OTP** |
| `Destination` | text | E.164 phone or email |
| `Channel` / `Purpose` | text | `Sms`/`WhatsApp`/`Email`; `Registration`/`AccountRecovery`/… |
| `CodeHash` | text | **HMAC-SHA256 with a server pepper**, not a bare hash |
| `PepperVersion` | int | For rotation |
| `RequestIp` | text? | |
| `VerifyAttempts` | int | |
| `Consumed` | bool | |
| `ExpiresAt` / `CreatedAt` | timestamptz | 5-minute TTL |

**Indexes:** `ix_otp_codes_dest_unexpired` on `(Destination, Purpose, ExpiresAt)` **filtered**
`WHERE "Consumed" = false`

**No FK by design** — first-registration OTPs precede the user row.

`OTP_PEPPER` is a **required secret**: resolved through `ISecretProvider.GetRequiredAsync` with no
fallback literal, and Production **refuses to boot** without it (D-115).

### `outbox_messages` *(AM0)*

`Id` · `Type` · `PayloadJson` (jsonb) · `IdempotencyKey` · `Status` · `Attempts` · `LastError` ·
`CreatedAt` · `DispatchedAt?`

**Indexes:** `UNIQUE (IdempotencyKey)` · `ix_outbox_pending` on `(Status, CreatedAt)` **filtered**
`WHERE "Status" = 'Pending'` — the dispatcher scans only undispatched rows.

Security-critical notifications are written **in the same transaction** as the state change (ADR-AM16).

### `signing_keys` *(AM10)*

`Id` · `KeyId` (the JWT `kid`) · `Algorithm` · `PublicKeySpki` · `PrivateKeyPkcs8?` · `State` ·
`CreatedAt` · `ActivatedAt?` · `RetiringAt?` · `RetiredAt?` · `CompromisedAt?` · `CompromiseReason?`

**FSM:** `Pending → Active → Retiring → Retired` (+ `Compromised`). Exactly one key is `Active` and signs;
`Retiring` keys no longer sign but still **validate**, which is what makes rotation zero-downtime.
`PrivateKeyPkcs8` is nulled once retired, so an expired key stops being a liability the moment it stops
being useful.

Private keys are wrapped with AES-GCM under a KMS CMK (D-102a), plaintext zeroed in `finally`.
⚠ **Never executed against real KMS.**

### `users` *(pre-existing, extended)*

Relevant columns: `Username`, `Email`, `Phone` (**legacy**, India-normalised by the old helper),
`PhoneE164` (**target**, D-089).

⚠ Both are written today (staged dual-write). Phase 6 cuts the code over, backfills and retires
`Phone`.

---

## 3. Migration history

| Migration | Contents |
|---|---|
| `20260718000000_AddAuthSubstrate` | 8 auth tables + `refresh_tokens.SessionId` / `PoPKeyThumbprint` + partial indexes (**hand-written**, snapshot hand-synced — the constraint that D-128 later retired) |
| `20260718100000_AddPhoneE164` | `users.PhoneE164` |
| `20260719000000_AddSigningKeys` | `signing_keys` + FSM columns |
| `20260720045954_AddPasswordAndTrustedBrowser` | `user_credentials`, `password_history`, `trusted_browsers`, `auth_challenges.MatchAttempts`. **Machine-generated** — first auth migration produced by `dotnet ef` in the container |
| `20260722041843_AddEmailVerifiedAt` | `users.EmailVerifiedAt` (nullable) — email verification, Phase 2D (D-182) |

Non-auth migrations interleave in the same folder; only the four above are authentication.

> **`dotnet ef` works in the container** (D-128), which retired this repository's long-standing
> hand-written-migration constraint. Migrations and the model snapshot are now machine-generated,
> removing an entire class of error. Do not hand-write new ones.

---

## 4. Pending schema work

| Item | Phase | Notes |
|---|---|---|
| `PhoneE164` backfill | 9 | Reconcile unparseable numbers **explicitly** — never silently drop |
| Retire `users.Phone` | 9 | Only once no reader remains |
| `security_events` retention | future | Append-only with no pruning today |
| Trusted-browser index review | 3 | Confirm `(TokenHash)` is the hot path once the service exists |

**No new tables are required** for phases 3–10. The schema was designed ahead of the logic.
