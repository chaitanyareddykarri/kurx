# Kurx trusted-device auth — hardware validation guide

Production acceptance procedure for the mobile authentication platform (AM2–AM9, D-091/D-092/D-093/D-094).

**Why this document exists.** Everything in `mobile/test/` runs on the host VM against a fake key
service, and everything in `mobile/integration_test/` has so far only run on an emulator with **no
credential enrolled**. That means the platform's central security claim — *the private key lives in
hardware and only a present human can make it sign* — is **not yet evidenced**. Nothing in this
guide can be run by CI on the current infrastructure; it is a human procedure on physical devices.

---

## 1. Evidence classification

Every result recorded against this guide must be classified. Use these exact labels — they are the
same ones used in `docs/DECISIONS.md`.

| Label | Meaning |
|---|---|
| **VERIFIED** | Compiled/analysed successfully. No runtime claim. |
| **VERIFIED BY TESTS** | Automated tests pass on the host VM (fakes, no real keystore). |
| **VERIFIED AT RUNTIME** | Observed on a real OS process — emulator/simulator included. |
| **PENDING HARDWARE VALIDATION** | Requires physical secure hardware; not yet run. |
| **UNVERIFIED** | Written but never compiled or executed. |

> **Rule:** an emulator or simulator result is **VERIFIED AT RUNTIME**, never
> **PENDING HARDWARE VALIDATION → satisfied**. Neither has real secure hardware (§7), so they can
> disprove a bug but can never *prove* hardware backing.

---

## 2. Current status before this guide is run

| Claim | Status |
|---|---|
| Kotlin plugin compiles; APK builds | VERIFIED |
| MethodChannel reachable; posture reported | VERIFIED AT RUNTIME (emulator, API 37) |
| Fail-closed when no credential enrolled | VERIFIED AT RUNTIME (emulator) |
| Signing with no key → `not_enrolled` | VERIFIED AT RUNTIME (emulator) |
| Dart ceremony + retry + screen logic | VERIFIED BY TESTS (110 tests) |
| **Key generated inside secure hardware** | **PENDING HARDWARE VALIDATION** |
| **Biometric-gated signing** | **PENDING HARDWARE VALIDATION** |
| **Key invalidation on biometric enrollment change** | **PENDING HARDWARE VALIDATION** |
| **StrongBox path** | **PENDING HARDWARE VALIDATION** |
| iOS `DeviceKeyPlugin.swift` — anything at all | **UNVERIFIED** (never compiled; no macOS available) |

Recorded emulator baseline (Pixel_7 AVD, API 37):
`{strongBoxSupported: false, biometricStatus: 11, apiLevel: 37, hasKey: false}` —
`biometricStatus 11` = `BIOMETRIC_ERROR_NONE_ENROLLED`.

---

## 3. Device matrix

Minimum set for production sign-off. **A1 and A2 are mandatory**; the rest raise confidence.

### Android

| ID | Device | Why it is in the matrix |
|---|---|---|
| **A1** | Pixel 6 or later (Titan M2) | The **StrongBox** path. Nothing else exercises `setIsStrongBoxBacked(true)` succeeding. |
| **A2** | Samsung Galaxy S-series (Knox/TEE) | Largest India install base; Samsung's Keymaster differs from AOSP and is where firmware-specific StrongBox refusals show up. |
| **A3** | Mid-range non-StrongBox (e.g. Redmi/Realme, API 29–33) | The **TEE fallback** path, and the realistic majority device for this market. |
| **A4** | Android emulator, API 33+ | Regression only. **Cannot** validate hardware backing (§7). |
| **A5** | Device with **PIN only, no biometric** | The `DEVICE_CREDENTIAL` fallback — must work, or users without biometrics are locked out. |

### iOS

| ID | Device | Why it is in the matrix |
|---|---|---|
| **I1** | iPhone with **Face ID** (X or later) | Primary Secure Enclave + Face ID path. |
| **I2** | iPhone **SE / Touch ID** | Touch ID differs in `LAContext.biometryType` and in the failure UX. |
| **I3** | **iPad** (Touch ID or Face ID) | Different form factor and keychain access-group behaviour. |
| **I4** | iOS Simulator | Regression only. **No Secure Enclave** — `isSecureEnclaveAvailable()` returns false by design. |

---

## 4. Prerequisites

### Global
- Backend reachable from the device. Note the base URL used; pass via
  `--dart-define=KURX_API_BASE=https://…`.
- A test account whose phone number you control (OTP bootstrap still applies).
- **Two devices** for the approval flow: one *approver* (enrolled) and one *waiter* (browser or
  second handset).
- Record for every run: device model, OS build number, app build, backend commit.

### Android
- Developer options + USB debugging enabled.
- `adb` on PATH (`…\Android\Sdk\platform-tools`).
- Install: `flutter build apk --debug && adb install -r -t build/app/outputs/flutter-apk/app-debug.apk`
- Screen lock configured as the case requires (PIN and/or biometric).

### iOS — **blocked**
> **None of the iOS cases below have ever been executed.** `ios/Runner/DeviceKeyPlugin.swift` has
> never been compiled — this project has no macOS host. Before any I-case can run, someone must
> build the app in Xcode, register the plugin in `AppDelegate.swift`, and resolve whatever compile
> errors surface. **Treat every iOS expected-result below as a design intent, not a prediction.**
- Xcode 15+, a paid developer account (Secure Enclave + passkeys need a provisioning profile with
  the right entitlements), a physical device (simulator cannot validate).

---

## 5. Capturing logs

Attach these to every result. A pass with no logs is not acceptance evidence.

**Android**
```bash
adb logcat -c                                   # clear before the run
adb logcat | tee run-A1-TC01.log                # capture during
# Useful filters:
adb logcat | grep -Ei "kurx|DeviceKey|Keystore|BiometricPrompt|strongbox"
```
Also capture the plugin's own posture dump — this is the primary artefact:
```
DEVICE SECURITY POSTURE: {strongBoxSupported: …, biometricStatus: …, apiLevel: …, hasKey: …}
KEY POSTURE AFTER CREATE: {insideSecureHardware: …, userAuthenticationRequired: …,
                           invalidatedByBiometricEnrollment: …, securityLevel: …}
```

**iOS**
- Xcode → Window → Devices and Simulators → Open Console, filter `kurx`.
- Or `log stream --predicate 'process == "Runner"' --debug`.

**Server-side, for every case**: the corresponding rows in `security_events` and `audit_log`
(`device.enroll_started`, `device.trusted`, `device.revoked`, `login.approved`, `stepup.satisfied`,
`recovery.redeemed`, `challenge.signature_invalid`).

---

## 6. Test cases

Automated harness for TC-01/02/12 (run first — it is fast and adapts to the device):
```bash
flutter test integration_test/device_key_test.dart -d <device-id>
```

---

### TC-01 · First enrollment (hardware backing)
**Applies to:** A1, A2, A3, A5, I1, I2, I3
**Prerequisites:** Fresh install. Screen lock configured. No existing Kurx device key.

**Steps**
1. Sign in with OTP.
2. Enrol this device as trusted (Security → set up this device).
3. Complete the biometric/credential prompt.
4. Capture `KEY POSTURE AFTER CREATE`.
5. Confirm the device appears as `Trusted` in Security, and server-side.

**Expected**
- Prompt appears **before** the key is usable.
- `insideSecureHardware: true` — **this is the case's whole point.**
- `userAuthenticationRequired: true`.
- `invalidatedByBiometricEnrollment: true` (Android N+).
- On **A1/A2**: `strongBoxSupported: true` and `securityLevel` indicates StrongBox (2) where the
  chip is present. On **A3**: `strongBoxSupported: false`, `insideSecureHardware` still `true`.
- Server: `device.enroll_started` then `device.trusted`; SPKI is 91 bytes.

**Failure modes**
| Symptom | Meaning |
|---|---|
| `insideSecureHardware: false` | **STOP — release blocker.** Key is software-backed; the platform's core claim is false on this device. |
| `not_enrolled` on a device with a lock set | `BiometricManager.canAuthenticate()` disagrees with the lock config — capture `biometricStatus`. |
| Key generated with **no** prompt | `setUserAuthenticationRequired` not applied — **release blocker.** |
| StrongBox requested and generation throws, fallback succeeds | Acceptable and expected on some firmware; record it. |

**Acceptance:** `insideSecureHardware: true` **and** `userAuthenticationRequired: true` **and** a
prompt was shown **and** the server recorded `device.trusted`.

---

### TC-02 · Signing requires a present human
**Applies to:** all A, all I
**Steps**
1. From another device/browser, start a sign-in for the same account.
2. On the enrolled device, open the approval screen.
3. Compare the match number on both screens. Approve.
4. Repeat, but **dismiss** the prompt instead.

**Expected**
- Match numbers are identical on both screens.
- Approving prompts for biometric/credential **every time** (validity window is 0 — no grace period).
- Dismissing → `user_canceled`; **no error banner** (cancelling is a choice, not a failure).
- Rejecting a request needs **no** biometric prompt.

**Failure modes:** a second approval within seconds that does *not* prompt ⇒ the auth validity
window is not 0 ⇒ **release blocker**. Match numbers differing ⇒ challenge correlation bug ⇒ blocker.

**Acceptance:** every signature preceded by a prompt; cancel is silent; reject needs no prompt;
server logs `login.approved` with the correct device id.

---

### TC-03 · Biometric enrollment change invalidates the key
**Applies to:** A1, A2, A3, I1, I2 — **the single most important case in this guide**
**Prerequisites:** TC-01 passed; device enrolled and Trusted.

**Steps**
1. Confirm approval works.
2. **Add a new fingerprint / re-enrol Face ID** in OS settings.
3. Return to Kurx and attempt to approve a sign-in.
4. Capture logs.
5. Re-enrol the device; confirm approval works again.

**Expected**
- Step 3 fails with **`key_invalidated`**.
- The app shows *"Your device security changed. Set this device up again…"* — **not** a generic error.
- Step 5 succeeds and produces a **new** SPKI (verify it differs from the original).

**Failure modes**
| Symptom | Meaning |
|---|---|
| Approval still succeeds after enrolment change | **CRITICAL release blocker.** Someone who adds their own fingerprint to a stolen unlocked phone inherits the victim's trusted device. |
| Generic "something went wrong" | UX defect; recoverable but must be fixed — user cannot know to re-enrol. |
| New SPKI identical to the old | Key was not actually regenerated. |

**Acceptance:** old key rejected with `key_invalidated`, user guided to re-enrol, new key has a
different public key.

---

### TC-04 · PIN/password removal
**Applies to:** A1, A2, A3, A5
**Steps:** enrol → remove the screen lock entirely in OS settings → attempt approval → re-add lock →
attempt again.

**Expected:** key is destroyed by the OS; approval fails with `key_invalidated` / `not_enrolled`;
re-adding the lock does **not** resurrect it — re-enrollment is required.
**Failure mode:** approval works with no device lock ⇒ **release blocker.**
**Acceptance:** device cannot approve while unlocked-by-default; re-enrollment restores service.

---

### TC-05 · Device credential fallback (no biometric)
**Applies to:** A5 (PIN only)
**Expected:** enrolment and approval both work using the PIN prompt; `biometricStatus` reports
success via `DEVICE_CREDENTIAL`; users without biometric hardware are never locked out.
**Failure mode:** `not_enrolled` on a PIN-only device ⇒ the `AUTH_DEVICE_CREDENTIAL` flag is not
taking effect ⇒ blocker for a large share of the market.
**Acceptance:** full enrol + approve cycle completes with PIN only.

---

### TC-06 · App reinstall
**Applies to:** all A, all I
**Steps:** enrol → uninstall → reinstall → sign in → inspect Security.

**Expected**
- The keystore entry is destroyed with the app; `hasKey: false` on first launch.
- **Refresh token does not survive** (Android: `EncryptedSharedPreferences` cleared;
  iOS: keychain items *can* outlive uninstall — see failure modes).
- The old device may still appear server-side as Trusted until revoked. **This is expected**: the
  server cannot know the key is gone. The user revokes it from Security.

**Failure modes**
| Symptom | Meaning |
|---|---|
| **iOS:** user is still signed in after reinstall | Keychain survived uninstall. Determine whether that is acceptable; if not, clear tokens on first launch after install. **Record the finding either way — this is a real iOS behaviour difference from Android.** |
| Old key still usable | Keystore not scoped to the app ⇒ blocker. |

**Acceptance:** no usable key after reinstall; token persistence behaviour documented per platform.

---

### TC-07 · Device restore / migration
**Applies to:** A1, A2, I1, I3
**Steps:** enrol on device 1 → back up → restore that backup onto device 2 (or factory-reset
device 1) → launch Kurx.

**Expected:** the key is **not** transferred (it is non-exportable, and iOS accessibility is
`WhenUnlockedThisDeviceOnly`). Device 2 must enrol afresh. The old entry remains listed server-side
until revoked.
**Failure mode:** device 2 can approve sign-ins without enrolling ⇒ **CRITICAL** — key material
escaped the original hardware.
**Acceptance:** restored device cannot sign; requires fresh enrollment.

---

### TC-08 · OS upgrade
**Applies to:** A1/A2/A3 (major Android version), I1/I2 (major iOS version)
**Steps:** enrol → perform a major OS upgrade → attempt approval without re-enrolling.

**Expected:** the key **survives**; approval works normally.
**Failure mode:** `key_invalidated` after an OS upgrade is a genuine (if uncommon) platform
behaviour. If seen, it is **not** a code defect, but the app must recover gracefully — the user is
told to re-enrol, not shown a dead end. Record the OS versions involved.
**Acceptance:** either the key survives, or the app degrades to a clear re-enrol prompt.

---

### TC-09 · Revoked device
**Applies to:** all
**Steps:** enrol two devices → from device B revoke device A → attempt approval on A → attempt to
refresh A's session.

**Expected:** A's approval is refused (`device_not_trusted`); A's session is signed out
**immediately** (D-081 revokes the device's sessions and refresh tokens); A's local key is deleted
when A revokes *itself*.
**Failure mode:** A keeps refreshing after revocation ⇒ the "lost my phone" button does not do what
it claims ⇒ blocker.
**Acceptance:** A cannot approve and cannot refresh; server logs `device.revoked` with the session
count.

---

### TC-10 · Compromised-device / risk engine
**Applies to:** A1 or A2 (one device is enough)
**Steps:** trigger repeated invalid signatures and a refresh-token replay for the account, then
attempt a normal sign-in within the hour.

**Expected:** the risk engine denies the login and it is **indistinguishable from an unknown
identifier** — a decoy challenge, no server-side challenge row (D-085). Server logs `risk.denied`.
**Failure mode:** the client can tell a denial from a decoy (different shape, timing, or message) ⇒
enumeration leak ⇒ blocker.
**Acceptance:** denial is opaque client-side; `risk.denied` present server-side.

---

### TC-11 · Recovery flow
**Applies to:** A1/A2 and I1
**Steps:** generate recovery codes (requires step-up) → revoke/lose every trusted device → recover
using phone OTP **plus** one recovery code → observe device state afterwards.

**Expected:** recovery requires **both** factors; every wrong combination returns the same opaque
message; on success every session is revoked and every trusted device becomes **Suspended**; the
recovered session is a plain bearer session; the used code cannot be reused.
**Failure mode:** recovery succeeding with only a code, or a distinguishable error, ⇒ blocker.
**Acceptance:** two factors enforced; devices suspended; code single-use.

---

### TC-12 · Step-up
**Applies to:** all A, all I
**Steps:** with a trusted device present, attempt to generate recovery codes without stepping up;
then step up and retry; then wait past `AUTH_STEPUP_TTL_SECONDS` (default 300s) and retry.

**Expected:** first attempt refused with `step_up_required` (403) and an actionable message; after
step-up it succeeds; after the window expires it is demanded again. A user with **no** trusted
device is exempt (they cannot step up).
**Failure mode:** the grant never expiring ⇒ step-up is decorative ⇒ blocker.
**Acceptance:** gate enforced, satisfied, and re-demanded after expiry.

---

### TC-13 · Offline behaviour
**Applies to:** A1 or A2, I1
**Steps:**
1. Start a sign-in on the waiter; put the **waiter** into airplane mode.
2. Observe the waiting screen.
3. Restore connectivity before the challenge expires.
4. Repeat, but stay offline **past** expiry (~120s), then reconnect.
5. Separately: approve while the **approver** is offline.

**Expected**
- Step 2: *"Waiting for network"* — visibly different from *"waiting for approval"*.
- Step 3: *"Connection restored"*, then normal waiting; **the same** challenge continues.
- Step 4: the expired challenge is discarded and a **brand-new** one starts automatically, with a
  new match number and an explanation that the previous request expired.
- Step 5: approval **fails fast and visibly**. It is **never** queued or replayed later (D-093).

**Failure modes**
| Symptom | Meaning |
|---|---|
| An approval lands after connectivity returns | **CRITICAL** — replay of a real-time security assertion; the approved design forbids it. |
| Offline shown as a generic error | UX defect; user cannot tell what to fix. |
| Expired challenge silently re-polled | The dead challenge was not discarded. |

**Acceptance:** all six connection states observed; **no** approval ever replays.

---

### TC-14 · Token refresh & sender-constrained rotation
**Applies to:** A1 or A2
**Steps:** obtain a device-bound session → let the access token expire → make an authenticated
request → inspect the refresh call.

**Expected:** refresh succeeds transparently; a device-bound token refuses to rotate without the
device proof (`proof_required`); concurrent 401s share **one** refresh, never several (reuse would
trip theft detection and revoke the family).
**Failure mode:** parallel refreshes causing a session-family revocation ⇒ users randomly signed
out ⇒ blocker.
**Acceptance:** single refresh in flight; rotation succeeds; sender constraint enforced.

---

### TC-15 · Passkey registration & authentication
**Applies to:** A1, A2 (implemented) · I1, I2, I3 (**not implemented** — no iOS plugin, D-097)

**⚠ Blocking prerequisites — passkeys cannot work without both:**
1. **Digital Asset Links.** `https://<WEBAUTHN_RP_ID>/.well-known/assetlinks.json` must serve an
   entry for package `in.kurx.kurx_mobile` with relation
   `delegate_permission/common.get_login_creds` and the app's **SHA-256 signing fingerprint**.
   Get it with:
   `keytool -list -v -keystore <keystore> -alias <alias> | grep SHA256`
   **List both the debug and release fingerprints** — they differ, and a build signed with the
   unlisted one fails while the other succeeds, which is a confusing way to lose an afternoon.
2. **A real HTTPS domain.** `WEBAUTHN_RP_ID` **cannot be `localhost`** — Asset Links is fetched over
   HTTPS from a registrable domain, so this case needs a staging backend on a real host with
   matching `WEBAUTHN_ORIGINS`.

**Steps**
1. Sign in, open Security, tap **Add a passkey**.
2. Complete the platform sheet (Google Password Manager).
3. Confirm the passkey appears under **Passkeys**, and server-side as a `TrustedDevice`.
4. Sign out. Sign in again choosing the passkey.
5. Revoke the passkey from Security; attempt passkey sign-in again.
6. Cross-device: from a browser on another machine, start a passkey sign-in and scan the QR with
   this phone (hybrid transport).

**Expected**
- Registration creates a credential visible in Google Password Manager.
- A **second** registration on the same device is refused with `already_registered` — the server's
  `excludeCredentials` doing its job — and is shown as an explanation, not an error.
- Authentication signs in and mints a **device-bound** session which, per D-086, is **not**
  sender-constrained (a WebAuthn authenticator cannot sign an arbitrary refresh token).
- After revocation the passkey no longer signs in — via the **same** revocation path as any other
  trusted device (no passkey-specific code, D-097).
- The signature counter may legitimately stay **0** for platform passkeys; the server must **not**
  treat 0 as a clone.

**Failure modes**
| Symptom | Meaning |
|---|---|
| `dom_error` on registration | Almost certainly Asset Links: file missing, wrong fingerprint, or `RP_ID` not matching the serving domain. **Not** a code fault. |
| `no_provider` | No screen lock set, or no Google Password Manager on the device. |
| Registration succeeds but sign-in fails | RP ID / origin mismatch between registration and assertion — check `WEBAUTHN_ORIGINS`. |
| A revoked passkey still signs in | **Release blocker** — revocation is not reaching the credential. |

**Logs:** `adb logcat | grep -Ei "CredentialManager|Passkey|webauthn|assetlinks"`, plus server
`security_events` (`passkey.registered`, `passkey.assertion_failed`, `login.approved` with
`method: passkey`).

**Acceptance:** register → sign in → revoke → refused, on A1 **and** A2; duplicate registration
reports `already_registered`; cross-device hybrid works or is explicitly deferred.

---

## 7. What the emulator and simulator can never prove

Recording this explicitly so a green emulator run is not mistaken for hardware acceptance.

| Environment | Cannot validate |
|---|---|
| **Android emulator** | Real StrongBox (`strongBoxSupported: false` always); genuine TEE isolation — the "hardware" keystore is emulated in software; real biometric sensor behaviour. `insideSecureHardware` may report `true` for an emulated TEE and **must not** be taken as hardware evidence. |
| **iOS simulator** | The Secure Enclave does not exist. `isSecureEnclaveAvailable()` deliberately returns `false` under `targetEnvironment(simulator)` so a simulator run cannot masquerade as hardware validation. Face ID/Touch ID simulation does not exercise the real `biometryCurrentSet` invalidation. |
| **Both** | Device restore, OS upgrade, real backup/migration semantics. |

Consequently **TC-01, TC-03, TC-04, TC-07 have no valid emulator/simulator result at all** — they
are hardware-only.

---

## 8. Acceptance criteria for production sign-off

The mobile authentication platform is production-ready when:

1. **TC-01, TC-02, TC-03, TC-09** pass on **A1 and A2** and on **I1**. Non-negotiable — these are
   the hardware-backing, human-presence, key-invalidation and revocation guarantees.
2. **TC-05** passes on A5 (no-biometric users are not locked out).
3. **TC-04, TC-06, TC-07, TC-08** pass or have their platform-specific behaviour **documented and
   accepted** (notably iOS keychain surviving uninstall, TC-06).
4. **TC-10 – TC-14** pass on at least one Android and one iOS device.
5. **TC-15** either passes or is formally deferred with passkeys disabled in the shipped build.
6. Every result carries logs (§5) and an evidence label (§1).
7. **Zero** findings classified as *release blocker* remain open.

### Sign-off record

| Case | A1 | A2 | A3 | A4 | A5 | I1 | I2 | I3 | I4 |
|---|---|---|---|---|---|---|---|---|---|
| TC-01 … TC-15 | | | | | | | | | |

Tester · Date · Device model · OS build · App build · Backend commit · Result · Evidence label ·
Log file

---

## 9. Known open items feeding into this guide

- **iOS plugin is UNVERIFIED** — never compiled. All I-cases are blocked until an Xcode build exists.
- **Passkeys not implemented** — TC-15 is a placeholder.
- **FCM approval delivery not implemented** — approvals are fetched by poll/pull-to-refresh; a
  push-latency case should be added once FCM lands.
- The Android **positive** key-generation path is still PENDING: the emulator could not enrol a
  credential without locking credential-encrypted storage and preventing app launch (see D-092).
  **TC-01 on A1 closes this gap.**
