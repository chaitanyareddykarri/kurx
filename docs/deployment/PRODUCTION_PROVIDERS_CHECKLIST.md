# Production providers & secrets — verified checklist

Companion to `docs/AUTHENTICATION_HANDOVER.md`. That document covers the authentication workstream;
this one covers the **external providers and cloud services** a production deployment needs.

Claims below were checked against the code on 2026-07-19 and the **provider reality section was
re-verified on 2026-08-15**. Where the repository disagrees with the intended plan, the repository wins
and the gap is called out.

> ## ⚠ Read this first: five provider integrations do not exist
>
> **Six** real provider adapters are implemented, all dormant until credentialed: `SesEmailSender`
> (email, D-284), `SnsSmsProvider` (SMS), `FirebasePushSender` (push), `ClamAvFileScanner` (D-298),
> `KmsSigningKeyProtector` (D-102a) and `AwsSecretsManagerProvider` (D-101a). **Payments, payouts,
> S3 storage, KYC and outbound WhatsApp** have dev-only implementations.
>
> *(This box said "four do not exist — only two real adapters, SMS and push", and listed email among
> the missing. That was true on 2026-07-19 and wrong from D-284 onward; the table below already
> contradicted it by marking ClamAV ✅. Corrected 2026-08-15.)*
>
> For the boundaries that are still dev-only, this is not a "configure it at deploy time" gap.
> `DependencyInjection.AddProvider` **throws at startup** when a flag names anything other than the
> dev value:
>
> ```csharp
> if (value != devValue)
>     throw new NotSupportedException($"{flag}={value} is not implemented yet; only '{devValue}' is available.");
> ```
>
> So setting `PAYMENT_PROVIDER=razorpay-sandbox` or `STORAGE_PROVIDER=s3` in production **fails the
> deploy at boot**. It fails closed, which is the right behaviour — but it means these are
> *development* tasks, not configuration tasks, and they are not scheduled. Boundaries with a real
> adapter (email, SMS, push, scanning, KMS, secrets) are selected by an explicit `switch` instead and
> are genuine configuration tasks.

---

## 0. Provider reality

| Provider | Flag | Real implementation? | Consequence |
|---|---|---|---|
| **SMS** | `SMS_PROVIDER=sns` | ✅ `SnsSmsProvider` | Ready. Blocked only on SNS production access + India DLT. |
| **Push** | `PUSH_PROVIDER=firebase` | ✅ `FirebasePushSender` | Ready. Needs the service-account JSON. |
| **Email** | `EMAIL_PROVIDER=ses` | ✅ `SesEmailSender` (D-284) | Ready. Needs a verified SES identity + `SES_FROM_ADDRESS`, IAM credentials and SES production access. Left at the `console` default, **no email is delivered**. *(This row said `ses` throws at startup — untrue since D-284.)* |
| **Payments** | `PAYMENT_PROVIDER` | ❌ `MockPaymentGateway` only | No real payment can be taken. Setting `razorpay-sandbox` throws. |
| **Storage** | `STORAGE_PROVIDER` | ❌ `LocalDiskStorage` only | See §3 — this is a data-loss risk on Fargate. |
| **KYC** | `KYC_PROVIDER` | ❌ `MockKycProvider` only | Verification decisions are simulated. Because they are, `IDENTITY_VERIFICATION_BYPASS` exists (D-323) — see the row below. |
| **Identity gate** | `IDENTITY_VERIFICATION_BYPASS` | n/a — a switch, not a provider | `true` skips the govt-ID/PAN/bank proofs behind publishing a public event, organizing paid, and receiving a payout. **Production refuses to start with it set**, so this cannot reach production by accident; it is listed here so a reviewer knows the switch exists and that it must be unset in every non-dev environment. Blacklist/risk checks are unaffected. Delete it when a real KYC provider lands. |
| **WhatsApp** | `WHATSAPP_PROVIDER` | ❌ `ConsoleWhatsAppSender` only | Setting `cloudapi` throws. |
| **File scanner** | `FILE_SCANNER=clamav` | ✅ `ClamAvFileScanner` (D-298) | Ready. Needs a reachable clamd (`CLAMAV_HOST`/`CLAMAV_PORT`). **Fails closed** — if clamd is down, uploads are rejected, not accepted unscanned. Left at the `none` default, nothing is scanned. **Required in Production since D-338** — `none` is no longer legal there. |
| **Signing-key protection** | `SIGNING_KEY_PROTECTION=kms` | ✅ `KmsSigningKeyProtector` (D-102a) | **Required in Production** — the API refuses to start with `none`. Needs a KMS key + IAM permission to wrap/unwrap. |
| **Secrets** | `SECRETS_PROVIDER=aws` | ✅ `AwsSecretsManagerProvider` (D-101a) | Ready. Every secret resolves through `ISecretProvider`; `configuration` is the dev default. Missing rows for this and KMS were why the box above could claim "only two adapters". |

**Impact on authentication specifically:** auth depends on SMS, push and email — **all three now have
real adapters**, so this is a credentialing task, not a development one. Left at the `console`
defaults, the OTP email channel and email-based security alerts print to the console instead of
sending; phone-based auth is unaffected either way. *(This paragraph said "the missing email adapter";
it landed at D-284.)*

---

## 1. AWS

All of this is defined in `infra/terraform/` (65 resources) and has **never been applied** — no AWS
account existed during development. `terraform validate` passes against the real provider schema;
that proves the configuration is well-formed, not that the infrastructure works.

### KMS
Two customer-managed keys: one for signing-key envelope encryption, one for data. The key policy
grants the task role exactly `GenerateDataKey`, `Decrypt`, `DescribeKey` — deliberately **not**
`kms:Encrypt`, which envelope encryption never calls.

**The KMS health check in the task logs is the gate for ES256.** It is the only thing that proves the
IAM identity holds *both* `GenerateDataKey` and `Decrypt` — the pair most often half-granted, and a
half-grant fails only at first unwrap, i.e. at the first login after cut-over.

### Secrets Manager
`SECRETS_PROVIDER=aws`, prefix `kurx/<env>/`. Terraform creates the **containers only** — a value set
in Terraform lands in state, which is exactly what Secrets Manager exists to avoid.

Secrets Terraform provisions (`infra/terraform/secrets.tf`):

| Secret | Notes |
|---|---|
| `JWT_SECRET` | Required until the ES256 migration completes. |
| `TICKET_HMAC_SECRET` | Required — Production **refuses to start** without it, or if it is under 32 chars or still the committed placeholder (D-216). Must be **distinct from `JWT_SECRET`**: it signs gate-entry tickets, and the two are separate security domains. |
| `OTP_PEPPER` | Required — Production **refuses to start** without it (D-115). Since D-215 it peppers **every** OTP, login included. |
| `FCM_SERVICE_ACCOUNT_JSON` | |
| `RAZORPAY_KEY_SECRET` | Container exists; nothing consumes it yet (§0). |
| `RAZORPAY_WEBHOOK_SECRET` | Same. |

**Not secrets you provision:**
- **Database password** — RDS `manage_master_user_password` generates and rotates it; it never enters
  Terraform state. Read the ARN from the `database_secret_arn` output.
- **Redis password** — **does not exist.** ElastiCache is configured with at-rest and in-transit
  encryption but **no `auth_token`**. If a password is wanted, `auth_token` must be added to
  `data_stores.tf` first; until then there is nothing to store.
- **SMTP credentials** — no email adapter exists (§0).
- **AWS credentials** — IAM task roles, never static keys.

### CloudWatch
Alarms are already defined in `monitoring.tf`, including two on the application's own telemetry:
`outcome=denied` spikes (credential stuffing / enumeration, otherwise invisible because the decoy
response is identical by design) and **any** `refresh.reuse_detected` occurrence (threshold zero —
token theft is worth a human look every time).

Subscribe an on-call address to the `alerts_topic_arn` output, or the alarms fire into nothing.

### X-Ray / OpenTelemetry
ADOT sidecar in the task definition. Set `OTEL_EXPORTER_OTLP_ENDPOINT` to it. There is deliberately
**no default endpoint** — a silently mis-targeted exporter looks identical to a working one until an
incident, when the data isn't there.

### ECS
Fargate, autoscaled 2→10, ALB idle timeout 120s for SignalR. Hangfire and SignalR run **in the API
process**, not as separate services — there is no separate worker deployment to provision.

### RDS PostgreSQL
Multi-AZ, automated backups and PITR are set in `data_stores.tf` (Multi-AZ is disabled when
`environment = "staging"` to control cost).

### ElastiCache Redis
**`REDIS_CONNECTION` is mandatory in Production ([D-217](../DECISIONS.md), implementing ADR-AM14).**
The API now refuses to start without it. Previously it fell back to an in-process cache in every
environment, which left three subsystems silently wrong while the app reported healthy: the SignalR
backplane lost cross-instance fan-out, presence disabled itself, and the `ChatHub` limiter plus the
distributed cache became per-process. Provision ElastiCache before the first Production deploy.

**Correction: Redis does not back rate limiting.** It is the SignalR backplane. Per D-116, all durable
auth state (challenges, OTP attempt caps, replay guards) lives in **Postgres**, which is the right
home for it — Redis is eviction-capable, and losing single-use enforcement on a challenge would be an
authentication bypass, not merely a degraded defence.

The HTTP rate limiter is **in-process**, so across N tasks the effective limit is N× the configured
value. Partially mitigated by the WAF's global per-IP rule on `/v1/auth/*`. Making it Redis-backed is
scoped future work (D-116), best measured after staging rather than assumed. D-217 does not change
this — it removes the *silent* case where Redis was absent entirely, not the in-process limiter.

---

## 2. Firebase

Needed: a Firebase project, the **Android** app registration, and a service-account JSON.

**A Firebase *web* app is not needed.** The web client uses no Firebase and registers no service-worker
push handler — verified. Web login-approval status arrives over SignalR (`/hubs/login`) with HTTP
polling as the guaranteed fallback, so the browser never needs FCM.

---

## 3. Storage — unscheduled production blocker

`LocalDiskStorage` is the only implementation, writing under `LOCALDISK_ROOT`. On Fargate that is the
container's **ephemeral** filesystem: every uploaded image, verification document and chat attachment
is destroyed when a task restarts or scales in, and two tasks cannot see each other's files.

This is outside the authentication workstream, but it will cause visible data loss on day one of
production. It needs an `IStorage` S3 adapter before any deployment that accepts uploads.

---

## 4. Android — one blocker gates the whole chain

The dependency runs in exactly one direction, and the original checklist has two steps inverted:

```
file_picker fixed  →  release APK builds  →  Play App Signing enrolment
                                                      │
                                        SHA-256 certificate fingerprint
                                                      │
                                          assetlinks.json (contains it)
                                                      │
                                         passkeys work on a real device
                                                      │
                                            hardware validation runs
```

- **`assetlinks.json` must come *after* Play App Signing**, not before — the file's whole content is
  the fingerprint that Play App Signing issues. The original order (host assetlinks at 10, enrol at
  11) cannot be executed.
- **`file_picker ^11.0.2` currently blocks everything above.** `FilePickerPlugin` does not resolve, so
  **no release APK can be built at all**. Owned by the concurrent chat session.
- `assetlinks.json` must return **200 directly with `application/json`, no redirect** — Google's
  verifier does not follow redirects.
- Hardware validation requires a **physical device, API 28+**. `scripts/android-validation.sh`
  refuses to run on an emulator by design; emulators lack real StrongBox/TEE and real Credential
  Manager behaviour, so a pass there would be evidence of nothing.

---

## 5. Domain

One registrable HTTPS domain. `WEBAUTHN_RP_ID` is set to it.

**Choose it once.** Changing the RP ID invalidates every existing passkey — this is the least
reversible decision in the deployment, and it cannot be undone by a redeploy.

---

## 6. SMS

`SMS_PROVIDER=sns` with `SNS_ENTITY_ID` / `SNS_TEMPLATE_ID` / `SNS_SENDER_ID`.

**Start both processes now — they are the long pole.** AWS SNS production access (out of sandbox) and
India DLT registration with TRAI take days to weeks. Until both land, `SMS_PROVIDER` stays `console`
and **no OTP reaches any real user**, which means no real user can complete first-time sign-in.

---

## 7. Email — not deployable

No adapter exists (§0). This is development work: an `IEmailSender` implementation, then a flag branch
in `DependencyInjection`. Until it lands, email verification, email OTP and email security alerts are
console output.

---

## 8. Payments — not deployable

No adapter exists (§0). `MockPaymentGateway` only. The Razorpay secret containers are provisioned but
unconsumed. Ticketing cannot take money until an `IPaymentGateway` implementation lands.

---

## 9. Corrected deployment order

Steps whose position changed from the original are marked.

1. Create Terraform state backend (S3 + DynamoDB) — *prerequisite, was implicit*.
2. Obtain the ACM certificate and choose the domain (fixes `WEBAUTHN_RP_ID` permanently).
3. `terraform apply` to **staging**, reviewing the plan resource by resource.
4. Populate secrets out-of-band (§1). `OTP_PEPPER` and `JWT_SECRET` are mandatory or the app will not boot.
5. Configure Firebase (Android app + service account) and store the JSON.
6. Deploy the backend to staging.
7. Configure DNS and HTTPS — *before* first real use, since the RP ID must resolve.
8. **Verify the KMS health check passed in the task logs**, plus `jwks_url` and OTel traces.
9. Subscribe on-call to `alerts_topic_arn`.
10. **Play App Signing enrolment** — *moved before assetlinks*; produces the fingerprint.
11. **Host `assetlinks.json`** with that fingerprint, 200 direct, no redirect.
12. Run the full Android hardware validation suite on a physical device.
13. Apply to production.
14. **AM23 security review.**
15. Enable ES256 issuance — only after gate §8 of the handover passes in full.
16. Remove `JWT_SECRET` once no HS256 token can still be live.

Steps 10–12 are blocked until `file_picker` is fixed. Step 6 will fail at boot if any unimplemented
provider flag is set to its production value (§0).

---

## 10. Corrected status

| Area | Status | Evidence |
|---|---|---|
| Authentication implementation | ✅ Complete | 447/447 backend tests, build 0/0 |
| Authentication tests | ✅ Complete | VERIFIED BY TESTS |
| Flutter authentication | ✅ Complete *(UI reachable as of D-117)* | 58/58 auth tests, analyzer 0 errors |
| Web authentication | ✅ Complete *(push-approval reachable as of D-117)* | typecheck + build green |
| Admin authentication | ✅ Complete — auth activity visible via `audit_log`; `security_events` is deliberately operator-only (D-117) | VERIFIED BY REVIEW |
| **SMS provider** | ⚠ Implemented, **dormant** — blocked on SNS + DLT | VERIFIED BY REVIEW |
| **Push provider** | ⚠ Implemented, needs credentials | VERIFIED BY REVIEW |
| **Email provider** | ❌ **Not implemented** | VERIFIED — `ConsoleEmailSender` only |
| **Payment provider** | ❌ **Not implemented** | VERIFIED — `MockPaymentGateway` only |
| **S3 storage** | ❌ **Not implemented** — ephemeral on Fargate | VERIFIED — `LocalDiskStorage` only |
| Infrastructure deployment | ⏳ Pending | never applied; no AWS account |
| Android hardware validation | ⏳ Pending — blocked by `file_picker` | no physical device |
| AM23 security review | ⏳ Pending | not started |
| ES256 cut-over | ⏳ Pending | off by call graph; 9 gates in handover §8 |
| Production rollout | ⏳ Pending | |

**Authentication is ready for deployment validation. The platform around it is not** — email,
payments and durable storage have no production implementation, and none of that is authentication
work. Deploying auth to staging does not require them; opening the platform to real users does.
