# Provider Architecture

How Kurx talks to everything outside itself. This is the canonical reference for the provider
boundary: what exists, how it is selected, and what adding a production implementation involves.

**Vendor-neutral by policy.** Interfaces, configuration values and documentation name *capabilities*,
not suppliers. Where a vendor appears below it is because the repository has already adopted it
(AWS SNS, Firebase) — never as a recommendation for the boundaries still open.

---

## 1. The principle

> Business logic depends on a provider **interface**. Never on an SDK, a client library, or a
> concrete implementation.

Everything external — object storage, email, SMS, WhatsApp, push, payments, payouts, KYC, malware
scanning, secrets, key protection, document rendering — sits behind an interface in
`Kurx.Application.Abstractions`. Implementations live in `Kurx.Infrastructure`. Nothing in
`Kurx.Domain` touches any of it.

The payoff is concrete: adding a production provider is **three steps that never touch a call site** —
write the adapter, add a registration case, document the variable.

---

## 2. Layering

```
Kurx.Domain            entities only, framework-free, no provider references
Kurx.Application       the interfaces (Abstractions/Providers.cs and friends)
Kurx.Infrastructure    every implementation + DependencyInjection.cs
Kurx.Api               endpoints and hubs; consumes interfaces, registers nothing
```

A consumer asks for `IStorage`. It cannot observe whether it received a local-disk implementation or
a cloud one, and that is the entire point.

---

## 3. Provider inventory

Development implementations are what ship today. **Production status is "not implemented" for every
boundary except push and SMS**, and the container refuses to start if configured otherwise.

| Interface | Development implementation | Config key | Default | Consumed by | Production status |
|---|---|---|---|---|---|
| `IStorage` | `LocalDiskStorage` | `STORAGE_PROVIDER` | `localdisk` | media, chat attachments, certificates, verification documents | **Not implemented** |
| `IFileScanner` | `NoOpFileScanner` | `FILE_SCANNER` | `none` (dev) — rejected in Production (D-338) | all 8 upload-confirm paths: chat attachments and event media scan directly; the other 6 (org verification, membership claims, org assets, event authorization, auth documents) route through `UploadScanGate` | **Available** (`clamav`, D-298/D-338) |
| `IEmailSender` | `ConsoleEmailSender` | `EMAIL_PROVIDER` | `console` | notifications, invitations, certificates | **Not implemented** |
| `IWhatsAppSender` | `ConsoleWhatsAppSender` | `WHATSAPP_PROVIDER` | `console` | notifications, tickets, announcements | **Not implemented** |
| `ISmsProvider` | `ConsoleSmsProvider` | `SMS_PROVIDER` | `console` | OTP delivery | **Available** (`sns`, dormant until credentialed) |
| `IPushSender` | `ConsolePushSender` | `PUSH_PROVIDER` | `console` | notifications, chat, login approval | **Available** (`firebase`) |
| `IPaymentGateway` | `MockPaymentGateway` | `PAYMENT_PROVIDER` | `mock` | orders, refunds, capture webhook | **Not implemented** |
| `IRouteClient` | `MockRouteClient` | `PAYMENT_PROVIDER` | `mock` | payouts, org linked accounts | **Not implemented** |
| `IKycProvider` | `MockKycProvider` | `KYC_PROVIDER` | `mock` | identity verification, org bank verification | **Not implemented** |
| `ISecretProvider` | `ConfigurationSecretProvider` | `SECRETS_PROVIDER` | `configuration` | startup secret resolution | **Available** (`aws`) — see note |
| `ISigningKeyProtector` | `NullSigningKeyProtector` | `SIGNING_KEY_PROTECTION` | `none` (dev) / `kms` (prod) | JWT signing-key wrapping | **Available** (`kms`) |
| `IDocumentRasterizer` | `DocumentRasterizerStub` | `DOCUMENT_RASTERIZER` | `stub` | certificate PNG previews | **Not implemented** |
| `ICertificateRenderer` | `CertificateRenderer` | — | — | certificates | **Complete** — in-process, no external service |
| `IQrCodeGenerator` | `QrCodeGenerator` | — | — | tickets, gate check-in | **Complete** — in-process, no external service |

Per-vendor credentials, env-var-by-env-var status and webhook specifics live in
[`docs/EXTERNAL_SERVICES_AND_PROVIDERS.md`](../EXTERNAL_SERVICES_AND_PROVIDERS.md) — this document
does not repeat them.

**Note on `ISecretProvider`:** `configuration` is a *production* answer, not a stub — it covers any
deployment that injects secrets as environment variables (ECS task secrets, Kubernetes secrets).

`ICertificateRenderer` and `IQrCodeGenerator` are deliberately unflagged: they are in-process
rendering with no external dependency, so there is nothing to select between. They stay behind
interfaces for testability, not for provider substitution.

### Related boundaries

`IPresenceService` (Redis-backed, `PresenceDisabledService` otherwise) and `IRealtimeBroadcaster`
(SignalR) follow the same shape but abstract *infrastructure Kurx runs*, not a third-party service.
`IPresenceService` is the reference example of the pattern: **exactly one place decides availability,
and nothing else in the codebase checks for Redis**.

---

## 4. Registration and selection

All registration happens in `Kurx.Infrastructure/DependencyInjection.cs`. Two shapes:

**Single-implementation boundaries** use the `AddProvider<TService, TDevImpl>` helper:

```csharp
AddProvider<IStorage, LocalDiskStorage>(services, config, "STORAGE_PROVIDER", "localdisk");
```

**Multi-implementation boundaries** (push, SMS, secrets, key protection) use an explicit `switch`,
because more than one implementation genuinely exists.

Both shapes obey the same rule.

### Fail fast — never silently fall back

An unrecognised or not-yet-implemented value **throws at startup**:

```
STORAGE_PROVIDER=s3 is not implemented yet: no production implementation of IStorage exists in
this build. The only available value is 'localdisk' (LocalDiskStorage). See .env.example and
docs/architecture/providers.md.
```

Falling back to the development implementation would be far more dangerous than refusing to start: a
production deployment misconfigured as `STORAGE_PROVIDER=s3` would come up **healthy** while writing
every upload to a container filesystem that vanishes on the next redeploy. A container that will not
start is a five-minute problem; silent data loss is discovered weeks later.

Two boundaries additionally fail closed **on the correct value** in production:
`SIGNING_KEY_PROTECTION=none` is rejected outside development (an unwrapped private signing key is a
complete authentication bypass if the database leaks), and `RequiredSecrets` refuses to start when a
required secret is absent.

**One switch is not a provider but obeys the same rule.** `IDENTITY_VERIFICATION_BYPASS=true` (D-323) skips
the government-ID, PAN and bank proofs that `TrustService` composes from `IKycProvider` results, so a dev or
test account can publish a public event, organize a paid one and receive a payout. It exists precisely
*because* the `IKycProvider` row above is `MockKycProvider` — `DigilockerAsync` always approves and
penny-drop/PAN pass for anything not ending `0000`, so the gate is real logic over simulated evidence and
enforcing it outside production establishes nothing. It is scoped so the flag cannot become a hole:
`fraudClear` is outside it and stays enforced, the reported `identity_verified`/`bank_verified` facts are
never rewritten, every unset or unrecognised value means enforced, and `AddKurxInfrastructure` **throws in
Production** — same shape as `SIGNING_KEY_PROTECTION=none`. Unlike the flags in the table, it has a defined
end: **delete it when `IKycProvider` gets a real implementation**, because at that moment the gate starts
meaning something and the switch turns from a convenience into a liability.

**Webhooks fail closed too.** The WhatsApp webhook (`POST /v1/webhooks/whatsapp` + the verification handshake)
**rejects with 503 in production when `WHATSAPP_APP_SECRET`/`WHATSAPP_VERIFY_TOKEN` is unset** — verification is
skipped only in Development (so tests/local runs are unblocked). The Razorpay webhook verifies via the gateway
(`IPaymentGateway.VerifyWebhookSignature`), which the production adapter enforces. A transient processing failure in
either webhook returns a **retryable 500** (never a silent 200), safe because both handlers are idempotent — so a
provider redelivery re-confirms without duplicate processing.

### Startup visibility

On boot the API logs one line per boundary, then a single warning naming every development
implementation still active:

```
Provider IStorage -> localdisk (LocalDiskStorage)
Provider IFileScanner -> none (NoOpFileScanner)
...
warn: Development providers active: storage (local disk — not durable); malware scanning (no-op —
      NO protection); payments (mock — signatures are not verified); ...
```

"Which providers is this instance actually running?" is the first question asked during an incident,
and the answer is otherwise spread across a dozen environment variables.

---

## 5. Configuration

Every provider is selected by a single environment variable, documented in `.env.example` alongside
the credentials that value would require. There are **no provider keys in `appsettings*.json`** —
provider selection is deployment configuration, not application configuration, and keeping it in one
place means one thing to audit.

Naming: `<CAPABILITY>_PROVIDER`, with three deliberate exceptions that name the *decision* rather than
a supplier slot — `FILE_SCANNER`, `SIGNING_KEY_PROTECTION`, `DOCUMENT_RASTERIZER`.

Development defaults are chosen so a fresh clone runs with **zero credentials**.

---

## 6. Development implementations

Each one exists so the platform is fully exercisable offline. All of them are for **local
development, automated tests and CI** — none is suitable for a deployed environment.

| Implementation | What it does | Limitations |
|---|---|---|
| `LocalDiskStorage` | Writes objects under a local root; HMAC-signed presigned PUT/GET against an API route | Single-node only. Lost on container replacement. No durability, replication, lifecycle policy or CDN origin. Presigned URLs point back at the API, so they do not work across hosts |
| `NoOpFileScanner` | Returns `Clean` for everything | **Provides no malware protection whatsoever.** The default, and development-only. Exists so the pipeline always has a scanner to call; that call site is what made enabling real scanning a config change |
| `ConsoleEmailSender` | Logs the message | Nothing is delivered. No templates, bounces, or suppression list |
| `ConsoleWhatsAppSender` | Logs the message | Nothing is delivered. No template approval or session-window handling |
| `ConsoleSmsProvider` | Logs the message | Nothing is delivered. No India DLT compliance path (the SNS implementation carries that) |
| `ConsolePushSender` | Logs the notification | Nothing is delivered. No token invalidation feedback |
| `MockPaymentGateway` | Returns synthetic order and refund ids | **`VerifyWebhookSignature` returns `true` unconditionally.** Any caller reaching the webhook endpoint can confirm a payment. Never expose an instance running this to an untrusted network |
| `MockRouteClient` | Returns synthetic linked-account and transfer ids | No money moves. Payout state advances against fictional identifiers |
| `MockKycProvider` | Approves every check | Identity, PAN and penny-drop verification all pass. Any KYC-gated capability is effectively ungated |
| `ConfigurationSecretProvider` | Reads secrets from configuration | Not a stub — appropriate wherever the platform injects secrets as environment variables |
| `NullSigningKeyProtector` | Stores signing keys unwrapped | Rejected in production. A leaked database yields usable signing keys |
| `DocumentRasterizerStub` | Returns a blank 1×1 PNG | Certificate PNG previews are silently empty. PDF generation is unaffected |

---

## 7. Future production implementations

Vendor-neutral by intent — these name the capability required, not a supplier.

| Boundary | Future implementation | Notes for whoever builds it |
|---|---|---|
| `IStorage` | Production object storage provider | Also needs a bucket, lifecycle policy and CDN origin in infrastructure. Presigned URL semantics already match cloud object stores |
| ~~`IFileScanner`~~ | **Built (D-298): `ClamAvFileScanner`** | Speaks clamd INSTREAM over TCP. Fails closed — every failure maps to `ScanFailed`, never `Clean`, so a scanner that is down blocks uploads instead of passing them unscanned. `Infected` deletes the object and audits the rejection |
| `IEmailSender` | Production email provider | Needs bounce/complaint handling and a suppression list, neither of which the interface covers yet |
| `IWhatsAppSender` | Production messaging provider | Template approval and session windows are provider concerns, not caller concerns |
| `ISmsProvider` | Additional SMS providers | Interface already carries `SmsSendOptions` for regulatory identifiers |
| `IPushSender` | Additional push providers | Token invalidation feedback has no interface surface yet |
| `IPaymentGateway` | Production payment gateway | **Signature verification is the security boundary.** See the mock's limitation above |
| `IRouteClient` | Production payout provider | Settlement and reversal semantics vary widely; the interface assumes on-hold transfers |
| `IKycProvider` | Production identity verification provider | Only masked last-4 values are ever persisted — keep that invariant |
| `IDocumentRasterizer` | Production PDF rasterizer | In-process library, no external service required |

---

## 8. Adding a new provider

1. **Define the interface** in `Kurx.Application.Abstractions` — capability-shaped, vendor-neutral,
   `CancellationToken` last. Model failure in the return type where callers must react to it
   (`FileScanResult` is the example) rather than by throwing.
2. **Write a development implementation** that needs no credentials, and make sure a fresh clone
   still starts.
3. **Register it** in `DependencyInjection.cs` — `AddProvider` for a single implementation, a `switch`
   for several. Never fall back silently.
4. **Document the variable** in `.env.example`, with any credentials it would require.
5. **Add the row** to §3 and the limitation to §6 of this document.
6. **Consume the interface only.** If a consumer needs an SDK type in its signature, the interface is
   wrong.

---

## 9. Lifecycle

Providers are registered as **singletons**: they are stateless adapters holding a client or an HTTP
connection, and a per-request instance would discard connection pooling. Business services that use
them are scoped; the DI container handles the mismatch because a singleton may be injected into a
scoped service.

Implementations must therefore be **thread-safe** and must not hold per-request state.

---

## 10. Design principles

- **Dependency inversion.** High-level policy (`ChatService`, `OrderService`) depends on abstractions.
  Adapters depend on the same abstractions. Neither depends on the other's concrete type.
- **Single responsibility.** One interface, one capability. `IStorage` stores bytes; it does not scan
  them — that is `IFileScanner`, which is why either can be replaced alone.
- **Open/closed.** A new provider is a new class plus a registration line. No existing consumer is
  edited, which is what makes the substitution safe rather than merely possible.
- **Fail fast, fail closed.** Refuse to start over degrading silently.
- **Vendor neutrality.** Interfaces name capabilities. Where a vendor name has already leaked into
  persisted schema, it is recorded as debt rather than pretended away — see §11.

---

## 11. Known deviations

Honest exceptions to the rules above. All were found by audit and are documented rather than hidden.

1. **Vendor names are baked into the persisted schema.** `Order.RazorpayOrderId`,
   `RazorpayPaymentId`, `RazorpayRefundId`, `Money.RazorpayTransferId`,
   `Organization.RazorpayLinkedAccountId` — and `IOrderService` exposes `RazorpayOrderId` in its DTO.
   The *abstraction* is vendor-neutral (`IPaymentGateway`), but the columns commit the schema to one
   supplier. Renaming means a migration plus changes across order, refund and payout logic, so it is
   deliberately deferred; a second gateway would force it.
2. **`ChatHub` depends on `StackExchange.Redis` directly** for its per-room rate limit, taking an
   `IConnectionMultiplexer?` rather than an abstraction. `IPresenceService` already demonstrates the
   right shape for this; the rate limiter has not been moved behind an interface yet.
3. **`AttachmentPolicy` uses an image library directly** for decode-based validation. Borderline: it
   is an in-process library rather than an external service, and the same is true of the QR and
   certificate renderers — the difference is that those two have interfaces and this does not.
4. **`FILE_SCANNER`, `SIGNING_KEY_PROTECTION` and `DOCUMENT_RASTERIZER` break the `*_PROVIDER`
   naming convention.** Each names a decision rather than a supplier slot, which reads better in
   context; renaming would break existing deployment configuration for no functional gain.
