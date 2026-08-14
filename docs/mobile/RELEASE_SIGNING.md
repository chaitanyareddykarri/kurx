# Android release signing

How the Kurx Android app is signed, and what must happen before it can be published.

> **What changed and why (D-103a).** The app previously signed release builds with
> `signingConfigs.getByName("debug")` — the Flutter template default. The Android debug keystore is
> **publicly distributed with a fixed password (`android`)**, so a release APK signed with it can be
> re-signed by anyone, is rejected by Play, and — for passkeys — would bind credentials to a key that
> is not exclusively ours. Release builds now **fail** rather than fall back to it.

---

## 1. Three different keys — do not confuse them

| Key | Who holds it | What it is for | Fingerprint used where |
|---|---|---|---|
| **Debug key** | Every developer machine (auto-generated, password `android`) | Local `flutter run` / `--debug` builds | Asset Links, for local passkey testing |
| **Upload key** | You. Kept out of the repo. | Signing the artifact you upload to Play | Play Console upload verification; Asset Links **only if not enrolled in Play App Signing** |
| **App signing key** | **Google**, when Play App Signing is enabled | The signature end users' devices actually see | **Asset Links, in production** |

The distinction matters because **passkeys verify against whatever key signed the installed APK**.
Get it wrong and passkeys work in internal testing and fail in production, with only a `dom_error`
to explain it.

- **Not enrolled in Play App Signing** → the upload key *is* the app signing key. Use its fingerprint.
- **Enrolled** (recommended, and mandatory for new apps) → Google re-signs. Use **Google's**
  fingerprint from *Play Console → Release → Setup → App signing → App signing key certificate*.

---

## 2. Current fingerprints

| Key | SHA-256 | Status |
|---|---|---|
| Debug (this machine) | `CD:DC:AC:94:81:3A:0C:58:4D:80:7A:A3:16:04:90:89:13:0C:6F:CD:7D:B2:2E:5E:DF:84:07:43:3B:D5:FD:F3` | **VERIFIED** |
| Upload | `95:CC:B4:8B:67:2D:A7:A4:83:CE:5B:83:4D:5A:6D:D0:2A:F8:FF:13:87:68:62:62:1C:52:96:DF:F3:7E:0A:C2` | **VERIFIED** |
| Play App signing | — | **PENDING DEPLOYMENT** — exists only after Play enrolment |

Both current fingerprints are already in `docs/mobile/assetlinks.json`.

> **The debug key is per-machine.** Every developer and every CI runner has a different one. Each
> must be added to `assetlinks.json`, or passkeys work for one person and not another.

---

## 3. Generating an upload keystore

```bash
keytool -genkeypair -v \
  -keystore upload-keystore.jks -storetype JKS \
  -keyalg RSA -keysize 2048 -validity 10000 \
  -alias upload
```

`-validity 10000` (~27 years) is the Play recommendation: **an expired signing key cannot be
renewed**, and without Play App Signing that would end your ability to update the app.

Then create `mobile/android/key.properties` from `key.properties.example`:

```properties
storeFile=../upload-keystore.jks
storePassword=<password>
keyAlias=upload
keyPassword=<password>
```

`storeFile` is resolved relative to `mobile/android/app/`.

---

## 4. Storing it securely

**`key.properties`, `*.jks` and `*.keystore` are gitignored (verified).** Nothing private enters the
repository.

- **Back the keystore up offline**, in at least two places. Without Play App Signing, losing it means
  you can never update the app — users must uninstall and reinstall from a new listing.
- **With Play App Signing the upload key is replaceable** (request a reset in Play Console), which is
  the main reason to enrol: it converts an unrecoverable loss into an inconvenience.
- **CI:** inject the keystore as a base64 secret and write `key.properties` at build time. Never
  commit either. Rotate if a build log ever echoes the password.

---

## 5. Fail-closed behaviour

`mobile/android/app/build.gradle.kts` throws when `key.properties` is absent:

```
Release signing is not configured. Create android/key.properties from
android/key.properties.example (see docs/mobile/RELEASE_SIGNING.md).
Release builds must never be signed with the debug key.
```

**A debug-signed release artifact cannot be produced by accident.** Debug builds are unaffected and
need no configuration.

---

## 6. Verifying which key signed a build

Without building — shows the config per variant:

```bash
cd mobile/android && ./gradlew :app:signingReport
```

Expect `Variant: release` → `Config: release` → your upload keystore. If it says `Config: debug`,
the fix has regressed.

After building, inspect the artifact itself:

```bash
"$ANDROID_HOME/build-tools/<ver>/apksigner" verify --print-certs \
  mobile/build/app/outputs/flutter-apk/app-release.apk
```

---

## 7. Play App Signing workflow

1. Build an **app bundle**: `flutter build appbundle --release` (Play requires AAB).
2. Create the app in Play Console; **enrol in Play App Signing** (default for new apps).
3. Upload — the upload key authenticates you; Google re-signs with the app signing key.
4. Copy the **app signing key** SHA-256 from *Setup → App signing*.
5. **Add it to `assetlinks.json`** and redeploy `.well-known/assetlinks.json`.
6. Re-verify with Google's checker (see `PASSKEY_DEPLOYMENT.md` §5).

> **Step 5 is the one that gets missed.** Everything works in internal testing — which uses the
> upload key — and passkeys fail for production users with only `dom_error` in the logs.

---

## 8. Known blocker (unrelated to signing)

`flutter build apk --release` currently fails to **compile**:

```
GeneratedPluginRegistrant.java: cannot find symbol
  class FilePickerPlugin (package com.mr.flutter.plugin.filepicker)
```

`file_picker` is declared in `pubspec.yaml` but its Android artifact is not resolving. This is a
**dependency problem in concurrent in-flight work, not a signing problem** — the signing
configuration itself is verified via `signingReport` (§6). It must be resolved before a release
artifact can be produced.
