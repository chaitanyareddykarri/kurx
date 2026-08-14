# Kurx Authentication — UI

Every authentication screen across Web, Admin and Flutter, **as built** (client Features 1–5 complete).

**Legend.** ✅ shipped · N/A not applicable to this surface by design

**Updated:** 2026-07-22 — client Features 1–5 (Login, Registration, Password, Security Center, Step-up)
implemented and verified across Web, Flutter, Admin against the frozen backend contracts. See
[`AUTHENTICATION_ROADMAP.md`](AUTHENTICATION_ROADMAP.md) for status.

**Universal rules.**
1. Client-side password validation calls the **same** NIST 800-63B rules as the server — length is the
   only hard gate (12–128), no composition rules. Mirrored in `web/lib/password.ts`,
   `admin/lib/password.ts` and `mobile/.../password_policy.dart`; the server stays authoritative for the
   breach deny-list, identifier checks and reuse history (returned as error codes). Never reimplement the
   policy differently.
2. Never reveal whether an account exists. Identifier steps show the same next screen either way.
3. Never show which factor failed. "Incorrect phone/email/username or password" — not "wrong password".
4. A lockout shows a **wait**, not a retry button (`423`).
5. The **match number is displayed only on the surface that starts the login/step-up** — never pushed to
   the approving device (anti-phishing, D-181).

---

## 1. Web

**Routing.** Sign-in is a **panel on the public landing page** (`otp-panel.tsx`), password-first.

| Component | Mounted at | Status |
|---|---|---|
| `components/auth/otp-panel.tsx` (password-first + OTP bootstrap + passkey) | landing `#login` | ✅ |
| `components/auth/login-waiting.tsx` (device approval, match number) | inside `otp-panel` | ✅ |
| `components/auth/passkey-sign-in.tsx` | inside `otp-panel` | ✅ |
| `components/auth/registration-flow.tsx` | `app/register/page.tsx` | ✅ |
| `components/auth/onboarding-form.tsx` (profile step, reused by the wizard) | inside `registration-flow` | ✅ |
| `components/auth/password-manager.tsx` + `password-field.tsx` | inside `security-center` | ✅ |
| `components/auth/reset-password-panel.tsx` | `app/reset/page.tsx` | ✅ |
| `components/auth/recovery-panel.tsx` | `app/recover/page.tsx` | ✅ |
| `components/auth/security-center.tsx` | `app/(app)/settings/security/page.tsx` | ✅ |
| `components/auth/step-up-required.tsx` | inside `security-center` | ✅ |
| `lib/auth-api.ts`, `lib/password.ts`, `lib/security-activity.ts`, `lib/webauthn.ts` | — | ✅ |

`app/onboarding/page.tsx` now **redirects to `/register`** (the registration ceremony owns profile
completion). `security-manager.tsx` was replaced by `security-center.tsx`.

### Screens

| # | Screen | Status | Notes |
|---|---|---|---|
| W1 | Sign-in — identifier + **password** | ✅ | One field for username/email/E.164; password always required (INV-A). |
| W2 | Sign-in — waiting + match number | ✅ | Device-approval branch; digits large, countdown, poll + SignalR. |
| W3 | Passkey sign-in | ✅ | Phishing-resistant same-device. |
| W4 | OTP bootstrap ("first time? sign in with a code") | ✅ | Retained per D-089 for accounts with no password yet. |
| W5 | Registration ceremony | ✅ | `/register`: signup OTP → email verify (skippable) → profile → success, driven by `/registration/status`. |
| W6 | Email verification | ✅ | Inside the wizard. |
| W7 | Create / Change Password | ✅ | In the Security Center; change requires the current password. |
| W8 | Forgot / Reset Password | ✅ | `/reset`: OTP **+ a second factor — recovery code _or_ device step-up** (INV-B) → new password → signed in. The recovery field is optional; the server picks whichever factor is present. This row read "OTP **+ recovery code**", and all three clients enforced that literally — a rule `PasswordResetService` never had, which refused every account that had never minted codes. |
| W9 | Security Center | ✅ | Overview, password, passkeys, devices, **trusted browsers**, sessions (+ sign-out-everywhere), recovery codes (copy/download), **security activity**, account recovery link. |
| W10 | Step-up required | ✅ | On recovery-code generation `403`; cross-device wait-and-retry (see §4). |
| W11 | Account recovery | ✅ | `/recover` (recovery codes → regain access). Distinct from password reset. |

### Flow

```
landing #login ─► W1 password ─┬─ trusted browser ─────────────► app
                               └─ device approval ─► W2 waiting ─► app
W1 "first time? code" ─► W4 OTP ─► /register
W1 "forgot password?" ─► W8 /reset
W1 "create an account" ─► W5 /register ─► (email) ─► (profile) ─► success ─► app
/settings/security ─► W7 · W9 · W10
```

---

## 2. Admin

**Routing.** `admin/app/login/page.tsx` (password-first + OTP bootstrap) + server actions in
`admin/lib/*`. Admin carries the `KurxAdmin` policy — the highest-value target — and keeps the session
**token server-side** (server actions + `router.refresh()`), never exposing it to client JS.

| # | Screen | Status | Notes |
|---|---|---|---|
| A1 | Login — identifier + password | ✅ | Server-action cookie-proxy carries `kurx_tb`; confirms `isStaff` on success. |
| A2 | Login — waiting + match number | ✅ | Device-approval branch. |
| A3 | OTP bootstrap | ✅ | "First time? Sign in with a code". |
| A4 | Change / Create Password | ✅ | `app/(console)/account/page.tsx`. |
| A5 | Forgot / Reset Password | ✅ | `app/reset/page.tsx`; confirms staff on completion. |
| A6 | Security Center | ✅ | `app/(console)/security/page.tsx`: overview, devices, browsers, sessions (+ sign-out-everywhere), recovery codes, activity. Reads server-side. |
| A7 | Step-up required | ✅ | On recovery-code generation `403`; cross-device wait-and-retry. |

**Intentional exclusions (by design).**
- **No self-registration** — staff are provisioned via `AdminStaffEndpoints`; a self-registered account
  is non-staff and cannot enter the console. So the registration ceremony and email verification are N/A.
- **No passkey login** — staff sign in with password/OTP.
- **Account recovery** — staff share the same user account as web; recovery codes are the mechanism, and
  the anonymous `/recovery` redeem ceremony lives on the shared public surface, not duplicated in-console.
- **No admin page reads `security_events`** as raw forensics — the Security Center shows the user's *own*
  activity via `/security-center/activity`; operator forensics remain CloudWatch-facing.

---

## 3. Flutter

**The mobile app is the approving/signing device** — it holds the device key that satisfies factor 2 and
signs step-up. Clean-architecture module under `mobile/lib/features/auth/` (`data`/`domain`/`presentation`).

| # | Screen | File | Status |
|---|---|---|---|
| F1 | Password login (primary) | `password_login_page.dart` (`Routes.login`) | ✅ |
| F2 | OTP entry (bootstrap) | `phone_entry_page.dart` (`Routes.loginOtp`) → `otp_verify_page.dart` | ✅ |
| F3 | Registration ceremony | `registration_flow_page.dart` (`Routes.onboarding`) | ✅ email verify → profile → success |
| F4 | Approve login (D-181 digit entry) | `approve_login_page.dart` | ✅ user types the 2 digits; signs `{nonce}.{NN}` |
| F5 | Create / Change Password | `password_page.dart` (`Routes.password`) | ✅ |
| F6 | Forgot / Reset Password | `password_reset_page.dart` (`Routes.resetPassword`) | ✅ |
| F7 | Recovery | `recovery_page.dart` (`Routes.recover`) | ✅ |
| F8 | Security Center | `security_page.dart` | ✅ overview, **Confirm-it's-you** step-up, password, passkeys, devices, **browsers**, sessions (+ sign-out-everywhere), recovery codes (copy), **activity**, account recovery |
| F9 | Step-up | `step_up_page.dart` | ✅ same-device biometric signature; reused by the recovery-codes gate and the standalone "Confirm it's you" |

The obsolete passwordless-initiate login (`login_wait_controller` / `login_waiting_page` /
`Routes.loginWaiting` / `startLogin` / `LoginStartDto`) was **removed** — the password-first login's
device-approval branch is handled inline in `password_login_page.dart`.

### Device contract (D-181)

The device signs **`"{nonce}.{NN}"`** — `NN` the two-digit match number zero-padded — not the bare nonce,
and sends `matchNumber` in the body (verified independently). `TrustedDeviceRepository.approveLogin` /
`stepUp` construct this exactly; the backend is `ChallengeService`.

### Flow

```
Routes.login ─► F1 password ─┬─ trusted device ───────────► app
                             └─ device approval ─► (inline waiting, match number) ─► app
F1 "first time? code" ─► F2 OTP ─► F3 registration
F1 "forgot password?" ─► F6 reset
new user ─► F3 registration (email → profile → success) ─► app

Push received ─► F4 approve (context + digit entry) ─► sign in Keystore ─► approved
F8 security ─► F5 · F9 step-up · browsers · devices · sessions · recovery · F7 recover
```

---

## 4. Step-up authentication (cross-surface)

Step-up = a **fresh same-device device-key signature** over `{nonce}.{NN}`, granting a ~5-minute
server-side window. Exactly one action is gated today: **recovery-code generation** (`StepUpGuard`);
users with no trusted device are exempt.

- **Flutter** signs same-device (`step_up_page.dart`, reused for the recovery-codes gate and the standalone
  "Confirm it's you").
- **Web / Admin** cannot produce a device signature and the backend exposes **no cross-device push** for
  step-up, so on a `403 step_up_required` they show `step-up-required` which **polls `/step-up/status`**
  and retries the action once the user confirms on the mobile app. **Known limitation:** cross-device
  step-up is not push-driven — the user must open the app and tap "Confirm it's you".
- **Passkeys do not satisfy step-up** — `/step-up/verify` takes an ES256 device signature, not a WebAuthn
  assertion.

---

## 5. Cross-surface consistency & client parity

| Concern | Rule / status |
|---|---|
| Password policy | One rule set (length-only + soft strength). Identical in web/admin/Flutter; server authoritative for breach/identifier/reuse |
| Error copy | Stable `error` code → mapped copy on every surface; raw messages never shown |
| Match number | Displayed only on the initiating surface; never pushed to the approver |
| Lockout | Every surface shows a wait, never a retry (`423`) |
| Enumeration | Every identifier step advances identically regardless of existence |
| Token storage | Web/Admin httpOnly cookies (`kurx_access`/`kurx_refresh`), refresh never exposed to client; Flutter `flutter_secure_storage`; **no tokens in localStorage** |
| Parity | Login / Password / Security Center / Step-up: full parity Web ↔ Flutter ↔ Admin. Registration + email verification: Web + Flutter (Admin N/A). Passkeys: Web + Flutter (Admin N/A). Device enrollment/signing: Flutter (device key) + Web passkeys |

## 6. Accessibility

- The match number is large, high-contrast and screen-reader-labelled — the single most
  security-critical piece of text in the product.
- Password strength is announced (`aria-live`); the meter is a hint, never a submit gate.
- Countdown timers have a text equivalent, not colour alone.
