# Kurx Authentication — Migration, Rollout and Compatibility

How the existing system and its existing users move onto the new architecture without lockouts and
without silently reopening single-factor login.

**Status: NOT STARTED.** This document is the plan, not a record. **Roadmap Phase 6** executes it.

> **The highest-risk item in the entire programme is password adoption for existing users (§2).** It is
> where a mistake either locks out the whole user base or quietly reintroduces the vulnerability the
> programme exists to remove. It needs its own `D-NNN` decision before any code is written.

---

## 1. What is actually changing for a real user

| Before | After |
|---|---|
| Phone → OTP → signed in | Identifier → **password** → trusted browser **or** device approval with digits → signed in |
| OTP is the login factor | OTP is bootstrap and recovery only |
| Nothing to remember | A password to remember |
| Any device with the SIM | A known password **plus** a bound browser or an enrolled device |

Every existing user has **no password**. That is the migration.

---

## 2. Password adoption for existing users ⚠ HIGHEST RISK

### The problem

The target flow requires a password at step 2. Every existing user has none. Three ways to get this
wrong, all of them serious:

1. **Enforce immediately** → the entire user base is locked out. Catastrophic.
2. **Let a passwordless user through** → single-factor login survives behind a flow that claims to be
   three-factor. Silent, and the worst outcome because nobody notices.
3. **Use OTP to set the first password** → OTP becomes a password-reset vector, which is precisely the
   SIM-swap attack the architecture rejects.

### The approach

**Grace-period adoption with an explicit, auditable state — never an implicit fallback.**

```
Existing user signs in
  └─ has_password == false
       ├─ Legacy path REMAINS AVAILABLE during the grace window:
       │    OTP → session, but the session is flagged  password_pending
       ├─ Immediately after sign-in: FORCED Create Password screen (interstitial, not dismissible
       │    beyond a bounded number of skips)
       ├─ Once set → user_credentials row exists → the user is on the new flow permanently
       └─ After the grace window closes → the legacy path is REMOVED, not merely discouraged
```

**Non-negotiables.**

- The bypass is a **dated, explicit, logged capability**, not an `if (credential == null)` scattered
  through the login path. One flag, one place, one removal.
- `security_event` on every legacy-path sign-in, so adoption is **measurable** and the tail is visible.
- The interstitial appears **after** authentication, never before — a Create Password screen shown to an
  unauthenticated caller is an account-takeover primitive.
- Setting the first password **must not** be reachable through OTP alone once the grace window closes.
- The window closes on a **measured** adoption number, not a guessed date.

### Rollback

If adoption stalls or the interstitial breaks: re-open the grace window by flag. Because the legacy path
is removed by configuration rather than deleted code during the window, rollback is a config change. Once
the code is deleted (end of Phase 6), rollback requires a deploy — so **deletion is the last step, not
the first**.

---

## 3. Device contract migration ⚠ BREAKING

Phase 3 changes what the device signs:

| | Payload |
|---|---|
| Before | `nonce` |
| After | `"{nonce}.{NN}"` — match number zero-padded to two characters |

Plus `matchNumber` becomes a **required** field on `/v1/auth/login/approve` and `/v1/auth/step-up/verify`.

**There is no compatibility window that is also safe.** Accepting a bare-nonce signature as a fallback
would mean an attacker can always choose the weaker form, defeating Amendment C entirely.

### Sequencing

```
1. Backend + Flutter developed together against the same contract
2. Both verified in the container / on device
3. Released TOGETHER — not backend-first, not app-first
4. Users on an old app build: approval fails with invalid_signature
   → the app must detect this and prompt "update required", never retry silently
```

**Required before release:** a **version signal** so an old client fails *clearly* rather than
mysteriously. Without it, users see an inexplicable failure and support has no diagnosis. This is listed
as Phase 3/7 work in [`AUTHENTICATION_ROADMAP.md`](AUTHENTICATION_ROADMAP.md) and **must not be dropped**.

Because mobile release is gated on the App Store / Play review cycle **and** currently blocked by
`file_picker`, this co-ordination is on the critical path.

---

## 4. Phone / E.164 migration (Phase 6 — client cutover DONE)

**Canonical model (staged, additive, reversible — D-089).** `users.PhoneE164` / `CountryCode` /
`PhoneNational` are written alongside the legacy `Phone`. `PhoneCanonicalizer` wraps libphonenumber and
refuses a national number unless the caller states the region. Resolution is **dual-read**
(`AuthIdentifiers`: `PhoneE164 == e164 || Phone == digits`), so a half-backfilled table authenticates
everyone — the migration is zero-downtime. **New registrations already write canonical `PhoneE164`**
(`AuthService` parses the digits through `PhoneCanonicalizer`).

**Client cutover — DONE (Phase 6).** Every client now produces canonical **E.164** from a single shared
component, replacing the ad-hoc "10 digits ⇒ +91" hacks:

| Surface | Component | Notes |
|---|---|---|
| Web + Admin | `@kurx/ui` `PhoneField` (`libphonenumber-js`) + `toE164Identifier` | country picker (flag/search/dial), as-you-type national formatting, E.164 output, copy/paste of `+…` re-selects country |
| Flutter | `common/widgets/PhoneField` (`phone_numbers_parser`) + `common/util/toE164Identifier` | same behaviour, searchable country sheet |

Phone-only fields (OTP signup, registration signup, phone-change) use `PhoneField`; mixed identifier
fields (login / reset / recovery — phone **or** email **or** username) use `toE164Identifier`, which
canonicalizes phone-shaped input and leaves email/username untouched. Client validation is fail-fast;
the backend `PhoneCanonicalizer` stays authoritative.

**Data backfill — IMPLEMENTED, not yet run in production.** `PhoneE164BackfillJob` (idempotent,
resumable, never touches the legacy column) is triggered by `POST /v1/admin/phone-backfill` (KurxAdmin).
It categorizes each legacy row exactly as the migration requires:

```
For each user with Phone but no PhoneE164 (region hint PHONE_BACKFILL_LEGACY_REGION, default IN):
  ├─ AUTO-CONVERTIBLE   parses + validates → write PhoneE164/CountryCode/PhoneNational   (report.Converted)
  ├─ AMBIGUOUS/UNPARSEABLE → left exactly as-is, counted + logged with the user id       (report.Failed)
  └─ COLLISION          canonicalizes onto a number another row already holds → skipped  (report.Skipped)
```

- **Automatically convertible:** already-`+`-prefixed numbers and legacy 10-digit rows that parse under
  the region hint — the vast majority. Written canonically.
- **Ambiguous / needs confirmation:** unparseable or region-ambiguous legacy values — **never guessed,
  never dropped** (each is somebody's login identifier). Left in place; reconciled manually or prompted
  for on the user's next sign-in before the legacy column is retired.
- **Collision:** two legacy rows canonicalize to the same E.164 — skipped for human review (the filtered
  unique index on `PhoneE164` would otherwise reject the second write).

**Remaining (Phase 7 / cleanup, gated on a production backfill run):** move the remaining backend write
sites off `AuthService.NormalizePhone` (legacy `Phone` column only) and **delete** the helper (do not
deprecate — it grows new callers); then retire `users.Phone` only once **no reader remains** — verified
by grep **and** a deploy with the column renamed rather than dropped, so a missed reader fails loudly and
reversibly.

---

## 5. Trusted device migration

Existing enrolled devices keep working — the enrollment contract is unchanged. Only the **approval**
payload changes (§3).

Devices in `PendingVerification` at cutover: unaffected, since enrollment signs the bare nonce and always
will.

---

## 6. Session and token compatibility

| Item | Behaviour across the migration |
|---|---|
| Existing access tokens | Valid until expiry (1 h). No forced invalidation |
| Existing refresh tokens | Continue rotating. PoP binding unchanged |
| Existing sessions | Preserved. Password adoption does **not** sign anyone out |
| ES256 cut-over | **Separate change, separate risk.** Do not bundle it with password migration |

**Password change and reset cascade-revoke** trusted browsers and sessions (Phase 3.3 / 4.5) — but that
applies to *new* changes, not to the adoption event itself. Setting a first password must **not** sign
the user out; that would make adoption feel like a punishment and depress completion.

---

## 7. Rollout sequence

```
1. Phase 3 verified   ─► challenge hardening + trusted browser, backend only
2. Phase 4 verified   ─► full ceremony behind a flag, legacy path still default
3. Phases 5/6/7       ─► clients built against the frozen contract
4. Staging soak       ─► real devices, real OTP if unblocked, measure Argon2id under load
5. Co-ordinated release ─► backend + mobile together (§3)
6. Grace window opens ─► adoption measured via security_events
7. Grace window closes─► legacy OTP login removed by flag
8. Cleanup            ─► legacy code deleted, users.Phone retired
```

**Gate between 5 and 6:** AM23 security review must be complete.

---

## 8. Rollback plan

| Stage | Rollback |
|---|---|
| 1–2 (backend, flagged) | Flip the flag. No data change |
| 3 (clients) | Deploy previous frontend build. Mobile is store-gated — **this is the slow one** |
| 5 (co-ordinated release) | ⚠ **Hardest.** Backend rollback breaks new app builds; app rollback breaks against new backend. Mitigation: keep the previous backend deployable for the whole store review window |
| 6–7 (grace window) | Re-open by flag |
| 8 (cleanup) | ⚠ **Not rollbackable by config** — requires a deploy. Do this last, and only after adoption is measured near-total |

**Data rollback.** No migration in this programme is destructive until step 8. `PhoneE164` is additive;
`user_credentials` is additive; `trusted_browsers` is additive. Nothing needs a down-migration before the
final cleanup — which is exactly why cleanup is last.

---

## 9. Pre-flight checklist for Phase 6

- [ ] `D-NNN` written for the password-adoption strategy, reviewed before code
- [ ] Adoption metric defined and instrumented **before** the grace window opens
- [ ] Legacy-path sign-ins emit `security_event` and are dashboarded
- [ ] Client version signal implemented so old apps fail clearly
- [ ] Backfill dry-run on a production **copy**; quarantine list reviewed
- [ ] Previous backend build kept deployable through the store review window
- [ ] Rollback rehearsed, not merely documented
- [ ] Support briefed: no self-service reset exists for a user with neither device nor recovery codes
- [ ] AM23 security review complete
