# Passkey deployment prerequisites (Android)

Everything that must be **configured and deployed** before Android passkey validation (TC-15) can
run. Nothing in this document is runtime verification — these are **deployment prerequisites**, and
each is classified accordingly.

> **Why this exists.** Passkeys fail closed and quietly. A missing or wrong Digital Asset Links entry
> produces `dom_error` with no indication of the cause, and an `RP_ID` of `localhost` can never work
> on a physical device. Both are configuration, not code, so no amount of testing on this machine
> surfaces them.

---

## 1. Status summary

| Item | Status |
|---|---|
| Debug SHA-256 fingerprint | **VERIFIED** — extracted from the real keystore (§3) |
| **Upload (release) SHA-256 fingerprint** | **VERIFIED** — release signing configured, D-103a |
| `assetlinks.json` with **both** fingerprints | **VERIFIED** — `docs/mobile/assetlinks.json`, parsed and asserted |
| Release builds no longer use the debug key | **VERIFIED** — `gradlew signingReport`: release → `Config: release` |
| **Play App Signing fingerprint** | **PENDING DEPLOYMENT** — exists only after Play enrolment; must be appended (see `RELEASE_SIGNING.md` §7) |
| Serving `.well-known/assetlinks.json` over HTTPS | **PENDING DEPLOYMENT CONFIGURATION** |
| `WEBAUTHN_RP_ID` on a real registrable domain | **PENDING DEPLOYMENT CONFIGURATION** |
| Passkey registration / authentication on a device | **PENDING HARDWARE VALIDATION** |

---

## 2. Release signing — RESOLVED (D-103a)

Release signing is now configured with a dedicated upload keystore, and the build **fails closed**
if `key.properties` is absent rather than silently falling back to the debug key. Verified with
`gradlew :app:signingReport`:

```
Variant: release
Config: release
Store: mobile/android/upload-keystore.jks
SHA-256: 95:CC:B4:8B:...:0A:C2
```

Full detail, key-storage guidance and the Play App Signing workflow: **`docs/mobile/RELEASE_SIGNING.md`**.

> **Still outstanding:** if you enrol in Play App Signing, Google re-signs the app and **its**
> fingerprint — not the upload key's — is what devices verify. It must be appended to
> `assetlinks.json` after enrolment, or passkeys pass internal testing and fail in production.

## 3. Fingerprints

### Debug (already extracted — VERIFIED)

```
SHA256: CD:DC:AC:94:81:3A:0C:58:4D:80:7A:A3:16:04:90:89:13:0C:6F:CD:7D:B2:2E:5E:DF:84:07:43:3B:D5:FD:F3
```

Reproduce with:

```bash
keytool -list -v \
  -keystore "$HOME/.android/debug.keystore" \
  -alias androiddebugkey -storepass android -keypass android | grep SHA256
```

> The debug keystore is **per-machine**. Every developer and every CI runner has a different one, so
> each must be added to the asset-links file, or passkeys work on one machine and not another.

### Upload / release — VERIFIED

```
SHA256: 95:CC:B4:8B:67:2D:A7:A4:83:CE:5B:83:4D:5A:6D:D0:2A:F8:FF:13:87:68:62:62:1C:52:96:DF:F3:7E:0A:C2
```

Reproduce with:

```bash
keytool -list -v -keystore mobile/android/upload-keystore.jks -alias upload | grep SHA256
```

Generation, secure storage and CI injection: `docs/mobile/RELEASE_SIGNING.md`.

### Play App Signing

If you enrol in **Play App Signing**, Google re-signs the app with *their* key, so the fingerprint
that matters at runtime is **Google's**, not your upload key. Take it from
**Play Console → Release → Setup → App signing → App signing key certificate**. Using the upload
key's fingerprint here is a common and confusing failure: internal testing works, production does not.

---

## 4. `WEBAUTHN_RP_ID` and origins

| Environment | `WEBAUTHN_RP_ID` | `WEBAUTHN_ORIGINS` |
|---|---|---|
| Local (web only) | `localhost` | `http://localhost:3000` |
| Staging | `staging.kurx.in` | `https://staging.kurx.in` |
| Production | `kurx.in` | `https://kurx.in,https://www.kurx.in` |

Rules that bite:

- **`localhost` cannot work for Android passkeys.** Asset Links is fetched over HTTPS from a
  registrable domain; there is nothing to fetch for `localhost`. Android passkey validation therefore
  requires a staging backend on a real host.
- **RP ID must be the registrable domain**, not a full URL and not a path — `kurx.in`, never
  `https://kurx.in/`.
- **A credential is bound to its RP ID.** Registering against `staging.kurx.in` and asserting against
  `kurx.in` fails: they are different relying parties. Staging and production passkeys are separate.
- Changing the RP ID **invalidates every existing passkey**. Choose it once.

---

## 5. Hosting `.well-known/assetlinks.json`

Serve `docs/mobile/assetlinks.json` at:

```
https://<WEBAUTHN_RP_ID>/.well-known/assetlinks.json
```

Requirements — all of them are load-bearing:

- **HTTPS with a valid certificate.** No self-signed, no expired.
- **`Content-Type: application/json`.**
- **HTTP 200 directly** — Google's verifier does not follow redirects. A `http → https` or
  `kurx.in → www.kurx.in` redirect breaks it, which is easy to miss because a browser follows it fine.
- **No authentication, no WAF challenge, no geo-block.** The fetch is server-to-server from Google's
  infrastructure, not from the handset — a WAF rule that challenges non-browser user agents will
  silently break passkeys (see §7).
- Must list **every** signing fingerprint that will run: each developer's debug key, CI's key, and
  the release/Play-signing key.

Verify once served:

```bash
curl -sS -D - https://<RP_ID>/.well-known/assetlinks.json | head -20   # expect 200, application/json
```

```
https://digitalassetlinks.googleapis.com/v1/statements:list\
?source.web.site=https://<RP_ID>\
&relation=delegate_permission/common.get_login_creds
```

The second is Google's own verifier and is authoritative — if it does not list your package, the
device will not either, regardless of what `curl` shows.

---

## 6. Multi-fingerprint file

```json
[
  {
    "relation": [
      "delegate_permission/common.handle_all_urls",
      "delegate_permission/common.get_login_creds"
    ],
    "target": {
      "namespace": "android_app",
      "package_name": "in.kurx.kurx_mobile",
      "sha256_cert_fingerprints": [
        "CD:DC:...:F3",   // developer debug key
        "<CI debug key>",
        "<release or Play App Signing key>"
      ]
    }
  }
]
```

`get_login_creds` is the relation passkeys require. `handle_all_urls` is for App Links — harmless to
include, and needed if deep links are used.

---

## 7. Failure signatures

| Symptom | Almost certainly | Check |
|---|---|---|
| `dom_error` on registration | Asset Links wrong or unreachable | Google verifier (§5); fingerprint matches the **installed** build |
| `dom_error` only in release | Release fingerprint missing from the file — or Play re-signed with a different key | Play Console signing certificate |
| `no_provider` | No screen lock, or no Google Password Manager | Set a device lock |
| `no_credential` on sign-in | No passkey on this device for this RP | Register first; confirm RP ID matches |
| Registers, then sign-in fails | RP ID / origin mismatch between the two ceremonies | `WEBAUTHN_ORIGINS` |
| `already_registered` | Working as designed — server's `excludeCredentials` refusing a duplicate | Not an error |
| Works on wifi, fails on mobile data | Asset Links fetch blocked by WAF/geo rules | Allow Google's fetchers |
| Works for one dev, not another | Only one debug fingerprint listed | Add every debug key |

Asset Links results are **cached by Play Services**. After fixing the file:

```bash
adb shell pm clear com.google.android.gms   # then retry
```

Otherwise a correct fix looks like it changed nothing.

---

## 8. Checklist before TC-15

1. [x] Release signing configured (§2) — **COMPLETE** (D-103a)
2. [x] Upload fingerprint extracted and in `assetlinks.json` — **COMPLETE**
2b. [ ] Play App Signing fingerprint appended **after enrolment** — **PENDING DEPLOYMENT**
3. [ ] Staging domain with valid HTTPS
4. [ ] `assetlinks.json` served, 200, `application/json`, no redirect
5. [ ] Google verifier lists the package (§5)
6. [ ] `WEBAUTHN_RP_ID` / `WEBAUTHN_ORIGINS` set to that domain, backend redeployed
7. [ ] App built with `--dart-define=KURX_API_BASE=https://<staging>`
8. [ ] Device: screen lock + biometric enrolled, Play Services current
9. [ ] `google-services.json` in place (D-096) for FCM
10. [ ] `scripts/android-validation.sh preflight` passes on a **physical** device

Only then does TC-15 in `HARDWARE_VALIDATION.md` produce a meaningful result.
