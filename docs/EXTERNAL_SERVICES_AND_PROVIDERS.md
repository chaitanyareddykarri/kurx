# External Services & Providers

Single source of truth for every external service, third-party API, SDK, provider, webhook, and infrastructure dependency Kurx uses or has planned. Every claim in this document was verified directly against the codebase (NuGet/`.csproj` files, `pubspec.yaml`, `package.json`, `Kurx.Application/Abstractions/Providers.cs`, `Kurx.Infrastructure/Providers/`, `Program.cs`, `docker-compose.yml`, `.env.example`, `docs/DECISIONS.md`, `.github/workflows/*.yml`) as of 2026-07-12, with the **headline finding and provider table below re-verified on 2026-08-12**. Where an existing doc's claim could not be verified in code, or contradicted the code, that is called out explicitly rather than repeated. Per-vendor sections further down still carry their original 2026-07-12 verification date — treat the headline table as current and a per-vendor detail as needing a re-check before you rely on it.

> **Scope split (2026-07-19):** the provider *architecture* — the pattern, DI flow, how to add a provider, design principles and known deviations — is now [`docs/architecture/providers.md`](architecture/providers.md). This document remains the source of truth for **per-vendor detail**: env vars, which are actually read, credentials and webhook specifics.

> **What "Production Readiness" means here (2026-08-02).** Every such rating below grades **one integration in isolation** — is *this* seam finished, correct and configurable. It is never a statement about the platform. [`roadmap/README.md`](roadmap/README.md) holds the deployable-whole verdict, which is **"Not production-ready; do not deploy"**, and that is the binding one for a deploy decision. The two coexist because a seam can be complete while nothing is plugged into it — which is precisely this repo's state for storage, WhatsApp, payments, payouts and KYC. (Email left that list when `SesEmailSender` shipped under D-284.)

**Headline finding** *(re-verified against `DependencyInjection.cs` on 2026-08-12; the "two boundaries" figure that stood here was from 2026-07-19 and is now three times low)*: Kurx's provider-abstraction layer (`Kurx.Application/Abstractions/Providers.cs`) is fully designed, and **six boundaries now have real implementations**, all dormant until credentialed:

| Boundary | Real adapter | Flag |
|---|---|---|
| Email | `SesEmailSender` (D-284) | `EMAIL_PROVIDER=ses` |
| SMS | `SnsSmsProvider` (AM1) | `SMS_PROVIDER=sns` |
| Push | `FirebasePushSender` | `PUSH_PROVIDER=firebase` |
| Malware scanning | `ClamAvFileScanner` (D-298) | `FILE_SCANNER=clamav` (**required in Production** since D-338) |
| Signing-key protection | `KmsSigningKeyProtector` (D-102a) | `SIGNING_KEY_PROTECTION=kms` (**required in Production**) |
| Secrets | `AwsSecretsManagerProvider` (D-101a) | `SECRETS_PROVIDER=aws` |

Presence is also real (`RedisPresenceService`) whenever `REDIS_CONNECTION` is set, and cleanly disabled rather than faked when it is not.

**Five boundaries remain dev-only**: payments (`MockPaymentGateway`), payouts (`MockRouteClient`), KYC (`MockKycProvider`), storage (`LocalDiskStorage`) and the document rasterizer (`DocumentRasterizerStub`). These are the ones still routed through `AddProvider<TService,TDevImpl>`, whose entire purpose is to **refuse** any value but the dev one — it throws `NotSupportedException` at startup rather than silently degrading, so a production deployment misconfigured as `STORAGE_PROVIDER=s3` fails to boot instead of coming up healthy and writing uploads to a disappearing container filesystem. The six above are registered by hand precisely *because* they have a real branch to select.

The consequence is narrower than it used to be but still decisive: **no real money moves and no WhatsApp message is delivered**, and uploads are not durable. `LogProviderConfiguration` names every dev implementation still in play at boot — read that log, not this table, for what a given instance is actually running.

**Note on stale documentation found during this audit**: `docs/PROJECT_HANDBOOK.md` and `.claude/memory/deployment.md` describe an earlier snapshot of this repo (claim "18 decisions to date", "no `OrderService` implementation", ".NET 8 SDK", "Postgres 16", "mobile: empty scaffold") that is contradicted by the current code (D-038 exists, `OrderService` is fully implemented for free/guest tickets, the repo runs .NET 10 / Postgres 17 per `docker-compose.yml` and `global.json`, and `mobile/` has a working attendee app). This document relies on direct code verification, not those two files. *(Update 2026-07-19: the handbook's mobile row and `overview.md`'s `ISmsSender` line have since been corrected; the SMS boundary is `ISmsProvider`, added in AM1.)*

---

## Table of contents

1. [WhatsApp Cloud API](#1-whatsapp-cloud-api) — OTP delivery, messaging
2. [AWS SES](#2-aws-ses) — Email
3. [Razorpay (Payments)](#3-razorpay-payments) — Checkout, capture, refunds
4. [Razorpay Route (Payouts)](#4-razorpay-route-payouts) — Organizer settlement
5. [KYC Provider (unnamed)](#5-kyc-provider-unnamed) — Penny-drop, PAN match, DigiLocker
6. [AWS S3](#6-aws-s3) — Media/document storage
7. [Firebase Cloud Messaging](#7-firebase-cloud-messaging) — Push notifications
8. [PostgreSQL](#8-postgresql) — Primary database
9. [Redis](#9-redis) — Cache, SignalR backplane, chat rate-limiting
10. [Hangfire](#10-hangfire) — Background jobs
11. [SignalR](#11-signalr-realtime) — Realtime (internal)
12. [QuestPDF](#12-questpdf) — Certificate PDF/PNG rendering
13. [QRCoder](#13-qrcoder) — QR code generation
14. [Docnet.Core](#14-docnetcore) — PDF rasterization
15. [Serilog](#15-serilog-logging) — Logging
16. [GitHub Actions + GHCR + SSH deploy](#16-github-actions--ghcr--ssh-deploy) — CI/CD & hosting
17. [Workflow automation](#17-workflow-automation--removed-d-337) — none; n8n removed (D-337)
18. [Missing / not-yet-selected integrations](#18-missing--not-yet-selected-integrations) — Maps, OAuth, analytics, crash reporting, search, video, voice, AI, CDN
19. [Final summary table](#19-final-summary-table)

---

## 1. WhatsApp Cloud API

**Purpose**: OTP delivery at login (D-012), and the target channel for invitation/announcement messaging (`IWhatsAppSender` doc-comment, `InvitationService`).

**Current Status**: **Partially Implemented.** Outbound sending is a **Stub**. Inbound webhook receipt/verification is **Implemented** (real code, unexercised without real Meta credentials).

**Used In**: Authentication (OTP), Invitations, Announcements.

**Backend Files**:
- `backend/Kurx.Application/Abstractions/Providers.cs:15-20` — `IWhatsAppSender` interface (`SendTextAsync`, `SendMediaAsync`).
- `backend/Kurx.Infrastructure/Providers/ConsoleProviders.cs:19-32` — `ConsoleWhatsAppSender`, the only implementation. Logs `[whatsapp→console] to=... text=...`; returns `Task.CompletedTask`. No HTTP call to Meta's Graph API.
- `backend/Kurx.Infrastructure/Auth/AuthService.cs:53` — `RequestOtpAsync` calls `whatsapp.SendTextAsync(phone, "Your Kurx login code is {code}...", ct)`.
- `backend/Kurx.Api/Endpoints/WebhookEndpoints.cs` — real, live code: `GET /v1/webhooks/whatsapp` performs the Meta verification handshake (reads `WHATSAPP_VERIFY_TOKEN`, line 20); `POST /v1/webhooks/whatsapp` verifies the `X-Hub-Signature-256` HMAC using `WHATSAPP_APP_SECRET` (lines 35-41, 73-90) before calling `IWhatsAppLogService.HandleWebhookAsync`.
- `backend/Kurx.Infrastructure/Messaging/WhatsAppLogService.cs` — persists inbound webhook delivery-status updates against `WhatsAppMessage` rows.
- `backend/Kurx.Infrastructure/DependencyInjection.cs` — `AddProvider<IWhatsAppSender, ConsoleWhatsAppSender>(services, config, "WHATSAPP_PROVIDER", "console")`.

**Mobile Files**: None — mobile only calls Kurx's own `/v1/auth/otp/*` endpoints (`mobile/lib/features/auth/data/datasources/auth_remote_data_source.dart`); it never talks to WhatsApp directly.

**Web Files**: None — same pattern, `web/lib/api.ts`'s `requestOtp`/`verifyOtp` call Kurx's own backend only.

**Environment Variables**:
| Variable | Read in code? | Purpose |
|---|---|---|
| `WHATSAPP_PROVIDER` | Yes (`DependencyInjection.cs`) | `cloudapi` \| `console` — requesting `cloudapi` currently throws `NotSupportedException` (no implementation exists) |
| `WHATSAPP_VERIFY_TOKEN` | Yes (`WebhookEndpoints.cs:20`) | Webhook handshake token |
| `WHATSAPP_APP_SECRET` | Yes (`WebhookEndpoints.cs:35`) | Webhook HMAC verification |
| `WHATSAPP_PHONE_NUMBER_ID` | **No** | Declared in `.env.example` for the unbuilt outbound sender |
| `WHATSAPP_ACCESS_TOKEN` | **No** | Declared in `.env.example` for the unbuilt outbound sender |

**Production Provider**: WhatsApp Business/Cloud API (Meta) — named in `.env.example` and `docs/PROJECT_HANDBOOK.md` as the intended provider; no alternative (Twilio, MSG91, AWS SNS) is referenced anywhere in code or docs.

**API Direction**:
- Outbound (planned, not built): `Kurx → HTTPS POST → Meta Graph API → delivery status`.
- Inbound (built): `Meta → HTTPS POST (webhook, HMAC-signed) → Kurx /v1/webhooks/whatsapp → WhatsAppMessage status update`. Also a one-time `GET` verification handshake.

**Cost Model**: Per-conversation/per-message pricing (Meta's WhatsApp Business Platform pricing, varies by country/category) — not configured or estimated anywhere in this repo.

**Production Readiness**: **Missing** (outbound). Needs: a real `IWhatsAppSender` implementation, a Meta Business/WhatsApp API account, phone number ID + access token, and `AddProvider` extended to route `cloudapi` to it. Inbound webhook code is **Needs Testing** — it exists and looks correct but has never been exercised against a real Meta webhook.

**Risks**: Vendor lock-in to Meta's platform; template-message approval process adds lead time; per-message cost at scale; number quality/ban risk if messaging policies are violated; no fallback channel configured if WhatsApp delivery fails (OTP has no SMS/email fallback in code today).

**Alternatives**: MSG91, Twilio (SMS/WhatsApp), AWS SNS (SMS) — none referenced in code; would require a new `IWhatsAppSender`/new `ISmsSender` implementation and abstraction respectively.

---

## 2. AWS SES

**Purpose**: Transactional email — ticket resend, certificate delivery (`IEmailSender` callers).

**Current Status**: **Stub.**

**Used In**: Orders (ticket resend), Certificates.

**Backend Files**:
- `backend/Kurx.Application/Abstractions/Providers.cs:8-13` — `IEmailSender` (`SendAsync(to, subject, htmlBody, attachments?, ct) -> Task<string?>`).
- `backend/Kurx.Infrastructure/Providers/ConsoleProviders.cs:8-17` — `ConsoleEmailSender`, the only implementation. Logs `[email→console] to=... subject=... attachments=...`; returns a synthetic id `console-{guid}`. No SMTP/SES call.
- `backend/Kurx.Infrastructure/Orders/OrderService.cs` — `ResendTicketAsync`/`ResendGuestOrderAsync` call `emailSender.SendAsync(...)`.
- `backend/Kurx.Infrastructure/Events/CertificateService.cs` — `GenerateForEventAsync` emails the rendered certificate PDF as an attachment.
- `backend/Kurx.Infrastructure/DependencyInjection.cs` — `AddProvider<IEmailSender, ConsoleEmailSender>(services, config, "EMAIL_PROVIDER", "console")`.

**Mobile Files**: None.

**Web Files**: None.

**Environment Variables**:
| Variable | Read in code? |
|---|---|
| `EMAIL_PROVIDER` | Yes — `ses` \| `console`; requesting `ses` throws `NotSupportedException` today |
| `AWS_REGION` | No |
| `AWS_ACCESS_KEY_ID` | No |
| `AWS_SECRET_ACCESS_KEY` | No |
| `SES_FROM_ADDRESS` | No |
| `SES_FROM_NAME` | No |

**Production Provider**: AWS SES v2 — the `AWSSDK.SimpleEmailV2` 4.0.100.2 NuGet package is already referenced in `backend/Kurx.Infrastructure/Kurx.Infrastructure.csproj`, but **zero code anywhere calls it** (confirmed by repo-wide grep for `Amazon.SimpleEmailV2`/`AmazonSimpleEmailServiceV2Client` — no hits).

**API Direction**: Planned: `Kurx → AWS SDK call (SES SendEmailAsync) → SES → SMTP delivery → recipient`. No webhook/bounce handling exists.

**Cost Model**: AWS SES is pay-per-email (roughly $0.10 per 1,000 emails in most regions, plus data transfer) — not configured/estimated in this repo.

**Production Readiness**: **Missing.** Package is present; needs a real `SesEmailSender : IEmailSender` implementation, a verified SES sending domain/identity, IAM credentials, and `AddProvider` routing for `ses`.

**Risks**: New AWS SES accounts start in sandbox mode (can only send to verified addresses) until a production-access request is approved — a real lead-time risk if not requested early; deliverability requires SPF/DKIM/DMARC DNS setup on the sending domain; no bounce/complaint handling designed yet.

**Alternatives**: SendGrid, Resend, Postmark, plain SMTP — none referenced in code; `.env.example`/`docs/PROJECT_HANDBOOK.md` name only SES.

---

## 3. Razorpay (Payments)

**Purpose**: Ticket checkout, payment capture, refunds for paid ticket types.

**Current Status**: **Stub (Mock).** Updated for M10/D-049 (2026-07-13): the paid-checkout *write-path* is now fully implemented (order → `POST /v1/webhooks/razorpay` capture → ticket issuance + Collected ledger + wallet), but it drives `MockPaymentGateway`. There is still **no real `RazorpayPaymentGateway`**, so **no real money moves** — the mock mints fake `order_mock_*`/`rfnd_mock_*` ids and its `VerifyWebhookSignature` always returns `true`.

**Used In**: Orders/Payments (blocked), Wallet (downstream of payments, also unwired).

**Backend Files**:
- `backend/Kurx.Application/Abstractions/Providers.cs:25-31` — `IPaymentGateway` (`CreateOrderAsync`, `RefundAsync`, `VerifyWebhookSignature`).
- `backend/Kurx.Infrastructure/Providers/MockProviders.cs:9-27` — `MockPaymentGateway`. Mints `order_mock_{guid}`/`rfnd_mock_{guid}` ids; `VerifyWebhookSignature` **always returns `true`** (line 26, comment: "accepts any signature so local webhook simulation scripts stay simple") — this must never ship to production as-is.
- `backend/Kurx.Infrastructure/Orders/OrderService.cs` — **updated in M10/D-049**: `CreateOrderAsync` now implements the paid path — a priced ticket type creates a `Pending` order and calls `paymentGateway.CreateOrderAsync(...)`; `ConfirmPaymentAsync` (invoked by the `POST /v1/webhooks/razorpay` capture webhook) then issues the ticket and writes the Collected ledger entry. `IPaymentGateway` **is** invoked here now — against `MockPaymentGateway`. (The method's XML doc-comment was updated to describe this paid flow.)
- `backend/Kurx.Domain/Entities/Orders.cs` — `Order.RazorpayOrderId` field exists (schema scaffolding for the future integration, unused today).
- `backend/Kurx.Infrastructure/DependencyInjection.cs` — `AddProvider<IPaymentGateway, MockPaymentGateway>(services, config, "PAYMENT_PROVIDER", "mock")`.

**Mobile Files**: None — no `razorpay_flutter` package in `mobile/pubspec.yaml` (verified: not present). `.env.example`'s `KURX_RAZORPAY_KEY_ID` comment ("razorpay_flutter sandbox key") is aspirational — the package isn't even installed yet.

**Web Files**: None — no Razorpay JS SDK in `web/package.json`.

**Environment Variables**:
| Variable | Read in code? |
|---|---|
| `PAYMENT_PROVIDER` | Yes — `razorpay-sandbox` \| `mock`; requesting `razorpay-sandbox` throws `NotSupportedException` today |
| `RAZORPAY_KEY_ID` | No |
| `RAZORPAY_KEY_SECRET` | No |
| `RAZORPAY_WEBHOOK_SECRET` | No |
| `KURX_RAZORPAY_KEY_ID` (mobile) | No — not read by any Dart code (no Razorpay package installed) |

**Production Provider**: Razorpay — the only payment gateway named anywhere in the repo (`docs/DECISIONS.md`, `docs/PROJECT_HANDBOOK.md`, `.env.example`). No Razorpay .NET SDK package is referenced in any `.csproj` — a real implementation would need to add one or call Razorpay's REST API directly via `HttpClient`.

**API Direction**: Planned: `Kurx → REST POST (create order) → Razorpay → client-side checkout → Razorpay → webhook POST (payment captured/failed) → Kurx (signature-verified) → Order status update`.

**Cost Model**: Razorpay charges a percentage transaction fee per successful payment (typically ~2% domestic cards/UPI, varies by instrument) plus GST — not configured/estimated in this repo.

**Production Readiness**: **Missing.** Needs: a real `IPaymentGateway` implementation, a Razorpay merchant account (KYC'd), API keys, real webhook signature verification (the mock's always-`true` verifier must be replaced, not just left in place behind a flag), and `AddProvider` routing for `razorpay-sandbox`/production.

**Risks**: `MockPaymentGateway.VerifyWebhookSignature` always returning `true` is a **security landmine** if ever accidentally left wired in a non-dev environment — flag this specifically in any pre-production review. Real integration needs PCI-relevant care (Kurx should never touch raw card data — Razorpay Checkout handles that client-side, which the architecture already assumes). Settlement/reconciliation complexity once wired to real money movement.

**Alternatives**: Stripe (not India-optimized for UPI/domestic rails — Razorpay is the deliberate India-first choice per `docs/PROJECT_HANDBOOK.md`), Cashfree, PayU — none referenced in code.

---

## 4. Razorpay Route (Payouts)

**Purpose**: Split/transfer settlement funds to organizers' linked bank accounts (org payout tiers T1/T2/T3, D-007).

**Current Status**: **Stub (Mock).**

**Used In**: Wallet/Payouts (Organizations).

**Backend Files**:
- `backend/Kurx.Application/Abstractions/Providers.cs:36-42` — `IRouteClient` (`CreateLinkedAccountAsync`, `CreateOnHoldTransferAsync`, `ReleaseTransferAsync`, `ReverseTransferAsync`).
- `backend/Kurx.Infrastructure/Providers/MockProviders.cs:29-46` — `MockRouteClient`. All methods synthesize `*_mock_{guid}` ids, no network call.
- `backend/Kurx.Infrastructure/Orgs/WalletService.cs` — `InitiateWithdrawalAsync` only creates a `Withdrawal` row with `Status="requested"`; requires `PayoutAccountStatus.Active`; **makes no external payout call**.
- `backend/Kurx.Domain/Entities/Orgs.cs:13-14` — `Organization.RazorpayLinkedAccountId`, `PayoutAccountStatus` fields (schema scaffolding, unused today).
- `backend/Kurx.Infrastructure/Jobs/CollectedToAvailableLedgerJob.cs` — daily job transitioning ledger state `Collected → Available` after a 7-day window; comment references "Razorpay standard settlement window" but the job itself is pure date-math over the DB, no Razorpay call.
- `backend/Kurx.Infrastructure/DependencyInjection.cs` — `AddProvider<IRouteClient, MockRouteClient>(services, config, "PAYMENT_PROVIDER", "mock")` (shares the same `PAYMENT_PROVIDER` flag as §3).

**Mobile Files**: None.

**Web Files**: None (payout management is an organizer/dashboard concern, not yet built in `web/`).

**Environment Variables**: Same as §3 (`PAYMENT_PROVIDER`, `RAZORPAY_*`) plus `RAZORPAY_ROUTE_ENABLED` (declared in `.env.example`, not read anywhere in code today) and the payout-policy override vars `PAYOUT_T1_ADVANCE_PCT`, `PAYOUT_T1_CAP_PAISE`, `PAYOUT_T1_FIRST_DELAY_DAYS`, `PAYOUT_T1_RESERVE_PCT`, `PAYOUT_T2_ADVANCE_PCT`, `PAYOUT_T2_RESERVE_PCT`, `PAYOUT_T3_ADVANCE_PCT`, `PAYOUT_T3_RESERVE_PCT`, `RESERVE_HOLD_DAYS` (all declared in `.env.example`, none found read in any `.cs` file — the payout tier defaults from D-007 appear to be hardcoded constants rather than reading these overrides; not independently confirmed which constants file holds them in this pass).

**Production Provider**: Razorpay Route (linked-account transfers) — same vendor as §3, a distinct product within it.

**API Direction**: Planned: `Kurx (Hangfire job or manual trigger) → REST POST (create/release/reverse transfer) → Razorpay Route → webhook (transfer status) → Kurx`.

**Cost Model**: Razorpay Route charges a per-transfer fee on top of the base payment gateway fee — not configured/estimated in this repo.

**Production Readiness**: **Missing.** Needs: real `IRouteClient` implementation, Razorpay Route enabled on the merchant account, organizer bank-account linking flow (KYC-gated, D-016), and real webhook handling for transfer status.

**Risks**: Direct financial/regulatory exposure — incorrect settlement math or a missed reversal is a real-money bug, not a cosmetic one; needs the most rigorous testing of any integration in this list before going live; RBI/payment-aggregator compliance considerations apply to organizer payouts in India.

**Alternatives**: None realistically — Route is Razorpay's own split-payment product; switching payment gateways would mean re-doing payouts too.

---

## 5. KYC Provider (unnamed)

**Purpose**: Verify organizer bank accounts (penny-drop) and identity (PAN match, DigiLocker) before enabling payouts (D-016).

**Current Status**: **Mock.**

**Used In**: Organizations (KYC gate before payments/payouts).

**Backend Files**:
- `backend/Kurx.Application/Abstractions/Providers.cs:57-63` — `IKycProvider` (`PennyDropAsync`, `PanMatchAsync`, `DigilockerAsync`).
- `backend/Kurx.Infrastructure/Providers/MockProviders.cs:48-63` — `MockKycProvider`. Deterministic pass/fail keyed on a `"0000"` suffix in the input; `DigilockerAsync` always returns `Approved: true`.
- `backend/Kurx.Infrastructure/Orgs/KycService.cs` — delegates to `IKycProvider`; stores only masked `account_last4`/`pan_last4` per D-016 (never full account/PAN numbers).
- `backend/Kurx.Infrastructure/DependencyInjection.cs` — `AddProvider<IKycProvider, MockKycProvider>(services, config, "KYC_PROVIDER", "mock")`.

**Mobile Files**: None.

**Web Files**: None (org KYC is an organizer-dashboard flow, not yet built in `web/`).

**Environment Variables**: `KYC_PROVIDER` (read; `.env.example` comment: "only mock implemented; real provider slots in behind `IKycProvider`" — no real-provider value has even been named yet, unlike the other flags which at least specify a target like `ses`/`s3`).

`IDENTITY_VERIFICATION_BYPASS` (D-323) is the **consequence** of this slot being mock-backed, and belongs here rather than beside the other provider flags: because `DigilockerAsync` always approves and penny-drop/PAN pass for anything not ending `0000`, the gate those calls feed establishes nothing outside Production while still costing a tester four submissions per account. Setting it `true` skips the govt-ID, PAN and bank proofs in `TrustService`, so a plain account can publish a public event, organize a paid one and receive a payout. It does **not** relax the blacklist or risk-score checks (real implementations, enforced either way) and does **not** alter the reported `identity_verified`/`bank_verified` facts. Defaults to enforced, and `AddKurxInfrastructure` **throws at startup** if it is set in Production. **This flag is the first thing to delete when a real provider lands here** — it is scaffolding for the gap this section describes, not a permanent feature.

**Production Provider**: **Not yet chosen.** No vendor (Razorpay's own KYC APIs, Setu, Signzy, Cashfree Verification, etc.) is named anywhere in the repo — this is the one provider slot in `Providers.cs` with no target vendor decided at all.

**API Direction**: Planned: `Kurx → REST call (penny-drop / PAN match / DigiLocker) → Provider → verification result → Kurx (masked storage only)`.

**Cost Model**: Typically per-verification-call pricing (₹X per penny-drop, per PAN check) — no vendor chosen, so no cost data exists.

**Production Readiness**: **Missing**, and **needs a vendor decision** before any implementation work — this is a prerequisite gap, not just a coding gap.

**Risks**: Regulatory — bank/PAN verification touches KYC/AML obligations; storing anything beyond masked last-4 would be a compliance and security problem (the mock already correctly avoids this pattern, which any real implementation must preserve).

**Alternatives**: Setu, Signzy, Cashfree Verification, Razorpay's own KYC endpoints (if staying single-vendor) — none evaluated in any doc found.

---

## 6. AWS S3

**Purpose**: Media/document storage — avatars, event media, certificate PDFs/PNGs.

**Current Status**: **Stub (local disk only).**

**Used In**: Profile (avatars), Events (media gallery), Certificates (PDF/PNG storage).

**Backend Files**:
- `backend/Kurx.Application/Abstractions/Providers.cs:46-53` — `IStorage` (`PresignPutAsync`, `PresignGetAsync`, `PutAsync`, `GetAsync`, `ExistsAsync`).
- `backend/Kurx.Infrastructure/Providers/LocalDiskStorage.cs` — the only implementation. Writes under `Path.GetFullPath(config["LOCALDISK_ROOT"] ?? ".localdata/storage")`; guards path traversal; "presigned" URLs are literally `{KURX_API_BASE}/v1/media/{key}` pointing back at the API's own media endpoint — explicitly commented "dev only".
- `backend/Kurx.Api/HealthChecks/LocalDiskStorageHealthCheck.cs` — probes writability, only registered when `STORAGE_PROVIDER=localdisk`.
- `backend/Kurx.Infrastructure/DependencyInjection.cs` — `AddProvider<IStorage, LocalDiskStorage>(services, config, "STORAGE_PROVIDER", "localdisk")`.

**Mobile Files**: None directly — mobile displays media via storage keys/URLs returned by the API, never talks to S3 (or local disk) directly.

**Web Files**: None directly — same pattern; `next.config.mjs`'s `images.remotePatterns` currently allows any `https://**` hostname, a wildcard placeholder, not a specific S3/CDN domain.

**Environment Variables**:
| Variable | Read in code? |
|---|---|
| `STORAGE_PROVIDER` | Yes — `s3` \| `localdisk`; requesting `s3` throws `NotSupportedException` today |
| `LOCALDISK_ROOT` | Yes (`LocalDiskStorage.cs`) |
| `S3_BUCKET` | No |
| `S3_REGION` | No |
| `S3_ENDPOINT` | No (declared for a MinIO-compatible custom endpoint, unused) |
| `PRESIGN_PUT_TTL` / `PRESIGN_GET_TTL` | No |

**Production Provider**: AWS S3 — the `AWSSDK.S3` 4.0.100.2 NuGet package is already referenced in `Kurx.Infrastructure.csproj`, but **zero code anywhere calls it** (confirmed by repo-wide grep for `Amazon.S3` — no hits). `.env.example`'s `S3_ENDPOINT` field suggests MinIO was considered as a self-hosted alternative but no code exists for either.

**API Direction**: Planned: `Kurx → AWS SDK (presign) → S3 → client uploads/downloads directly via presigned URL (no file bytes through Kurx's own server)`.

**Cost Model**: S3 is usage-based — per-GB storage, per-request, and per-GB egress — not configured/estimated in this repo.

**Production Readiness**: **Missing.** Needs: a real `S3Storage : IStorage` implementation, a bucket, IAM credentials scoped to that bucket, CORS configuration for direct browser/app uploads, and `AddProvider` routing for `s3`.

**Risks**: Misconfigured bucket ACLs/public access is the classic S3 risk — needs careful IAM policy design from the start; egress costs scale with certificate/media volume; presigned URL TTLs (`PRESIGN_PUT_TTL`/`PRESIGN_GET_TTL`, already scaffolded in `.env.example`) need sane defaults once wired.

**Alternatives**: Cloudflare R2 (S3-compatible, no egress fees), MinIO (self-hosted, already hinted at via `S3_ENDPOINT`), DigitalOcean Spaces — none evaluated in code/docs beyond the `S3_ENDPOINT` hint.

---

## 7. Firebase Cloud Messaging

**Purpose**: Push notifications to the mobile app.

**Current Status**: **Stub.**

**Used In**: Notifications.

**Backend Files**:
- `backend/Kurx.Application/Abstractions/Providers.cs:65-68` — `IPushSender` (`SendAsync(fcmToken, title, body, data?, ct)`).
- `backend/Kurx.Infrastructure/Providers/ConsoleProviders.cs:34-42` — `ConsolePushSender`, the only implementation. Logs `[push→console] ...`. No FCM SDK call, despite the `FirebaseAdmin` 3.5.0 NuGet package being referenced in `Kurx.Infrastructure.csproj` (confirmed unused via grep — zero `FirebaseApp`/`FirebaseMessaging` hits anywhere in `backend/`).
- `backend/Kurx.Domain/Entities/Users.cs` — `Device` entity (`FcmToken`, `Platform`, `LastSeen`).
- `backend/Kurx.Infrastructure/Events/AnnouncementService.cs:152-161` — **a genuine, live call site**: when an announcement's `Channels` include `"push"`, it queries `db.Devices` for the batch of recipient user ids and calls `push.SendAsync(token, ann.Title, bodyExcerpt, null, ct)` per device token.
- `backend/Kurx.Infrastructure/Notifications/NotificationService.cs:33-47,49-67` — `RegisterDeviceAsync` (upserts a `Device` row) and `NotifyAsync` (writes a `Notification` row, then pushes to every device token for that user) both exist and are correctly wired internally — **but confirmed dead code**: repo-wide grep found zero endpoint or other call site invoking either `NotificationService.NotifyAsync` or `RegisterDeviceAsync` anywhere in `Kurx.Api/Endpoints/`. `INotificationService` is registered in DI but nothing calls it.
- **Net effect**: the announcement→push path (`AnnouncementService`) is real and reachable via `POST` announcement-send endpoints, but since `RegisterDeviceAsync` has no API endpoint, **no client can ever get a device token into the `Devices` table** — so in practice this path can never actually deliver a push today, independent of `ConsolePushSender` being a stub. Two separate gaps, not one.
- `backend/Kurx.Infrastructure/DependencyInjection.cs` — `AddProvider<IPushSender, ConsolePushSender>(services, config, "PUSH_PROVIDER", "console")`.

**Mobile Files**: **None.** No `firebase_messaging`, `firebase_core`, or any Firebase package in `mobile/pubspec.yaml` (verified — the full dependency list has zero Firebase entries). No `firebase_options.dart`, `google-services.json`, or `GoogleService-Info.plist` found anywhere under `mobile/`. The mobile app cannot receive a push today even if the backend sent one — both ends are unbuilt.

**Web Files**: None.

**Environment Variables**: `PUSH_PROVIDER` (read; `fcm` \| `console`, `fcm` throws `NotSupportedException` today), `FCM_SERVICE_ACCOUNT_JSON_PATH` (declared in `.env.example`, not read anywhere in code).

**Production Provider**: Firebase Cloud Messaging (Google) — the only push provider named anywhere in the repo.

**API Direction**: Planned: `Kurx (event trigger) → Firebase Admin SDK → FCM → device`. Mobile side (also unbuilt): `FCM → device token registration → Kurx POST /v1/devices (or similar, not found)`.

**Cost Model**: Free — FCM has no usage-based cost for standard push delivery.

**Production Readiness**: **Missing on both ends.** Needs: a real `FcmPushSender : IPushSender` backend implementation, a Firebase project + service-account JSON, `AddProvider` routing for `fcm`; on mobile, the `firebase_messaging`/`firebase_core` packages, platform config files (`google-services.json`/`GoogleService-Info.plist`), and a device-token-registration endpoint + call site (none of which currently exist).

**Risks**: Free, low cost/vendor-lock risk relative to other providers here; iOS push requires an Apple Push Notification (APNs) certificate/key configured inside Firebase, an extra credential to manage beyond the Firebase service account.

**Alternatives**: OneSignal (cross-platform push abstraction) — not referenced anywhere; FCM is standard for Flutter and the only one planned.

---

## 8. PostgreSQL

**Purpose**: Primary system-of-record database for all application data.

**Current Status**: **Implemented** — the one fully production-grade dependency in this document.

**Used In**: Every module (Authentication, Events, Orders, Certificates, Chat, Notifications, Profile, Orgs/Wallet/KYC, Hangfire job storage).

**Backend Files**:
- `backend/Kurx.Infrastructure/Persistence/KurxDbContext.cs` — 70 `DbSet`s, the entire schema.
- `backend/Kurx.Infrastructure/Migrations/` — 19 migration files (`Initial` → `EventManagement` → … → `AddVerificationSubstrate`, `AddPlatformRoles`, `DropIsKurxAdmin`, `AddUserIdentity`, `AddOrgRegistry`, `AddOrgVerification`, `AddMembershipClaims`, `RenameKycToOrgBankVerification`, `DropRegistrationForms`, `AddFraudTables`), auto-applied at boot (`Program.cs`), host aborts on migration failure.
- `backend/Kurx.Infrastructure/Kurx.Infrastructure.csproj` — `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2.
- `backend/Kurx.Tests/KurxApiFactory.cs` — tests run against a real `kurx_test` Postgres database, never mocked persistence.

**Mobile Files**: None — mobile never connects to Postgres directly, only via Kurx's own REST API.

**Web Files**: None — same.

**Environment Variables**: `ConnectionStrings__Default` (read in `DependencyInjection.cs`/`Program.cs`), `POSTGRES_PASSWORD` (docker-compose only, kept in sync with the connection string per `.env.example`'s own comment).

**Production Provider**: Self-hosted/managed PostgreSQL 17 (per `docker-compose.yml`'s `image: postgres:17`) — no managed-DB vendor (AWS RDS, Supabase, Neon, etc.) is named anywhere; the repo assumes you bring your own Postgres 17 instance (Docker container locally, unspecified host in production per `docs/deployment/`).

**API Direction**: Direct TCP connection, `Kurx ⇄ Npgsql driver ⇄ Postgres`. No webhook/polling — synchronous queries within each request.

**Cost Model**: Depends entirely on hosting choice — free if self-hosted on owned hardware/a VPS already being paid for; usage-based (storage + compute) if using a managed provider. Not decided in this repo.

**Production Readiness**: **Production Ready** as an integration; **Needs Configuration** for an actual production host/backup strategy (none is documented in this repo beyond "bring your own Postgres 17").

**Risks**: Single point of failure with no documented backup/replication/failover strategy in this repo; connection string committed as a dev placeholder in `appsettings.Development.json` (flagged, expected, and gated by `SecretValidation.cs`'s production fail-closed check per `docs/security/secret-management.md`).

**Alternatives**: N/A — Postgres is a firm architectural choice (D-003 and throughout), not something to swap.

---

## 9. Redis

**Purpose**: SignalR realtime backplane (multi-instance fan-out), distributed cache (profile-stats TTL), and `ChatHub`'s per-room sliding-window rate limiter.

**Current Status**: **Implemented**, but fully optional — every use gracefully degrades when unconfigured.

**Used In**: Chat (rate limiting), Sales/Scan dashboards (SignalR backplane), Profile (cache).

**Backend Files**:
- `backend/Kurx.Api/Program.cs` — SignalR Redis backplane wired only `if (!string.IsNullOrWhiteSpace(redisConn))`; health check `AddRedis` added only under the same condition.
- `backend/Kurx.Infrastructure/DependencyInjection.cs` — distributed cache: `AddStackExchangeRedisCache` when `REDIS_CONNECTION` set, else `AddDistributedMemoryCache()` fallback.
- `backend/Kurx.Api/Hubs/ChatHub.cs` — Redis-backed sliding-window rate limiter (10 msgs/60s/user/room) via `IConnectionMultiplexer` sorted sets; falls back to always-allow when Redis isn't configured.
- `backend/Kurx.Infrastructure/Kurx.Infrastructure.csproj` — `StackExchange.Redis` 3.0.11, `Microsoft.Extensions.Caching.StackExchangeRedis` 10.0.0. `backend/Kurx.Api/Kurx.Api.csproj` — `Microsoft.AspNetCore.SignalR.StackExchangeRedis` 10.0.0, `AspNetCore.HealthChecks.Redis` 9.0.0.

**Mobile Files**: None — mobile connects to SignalR hubs directly (via the backend), never to Redis.

**Web Files**: `web/package.json` — `@microsoft/signalr` 8.0.7 client package (talks to Kurx's SignalR hubs, not to Redis directly).

**Environment Variables**: `REDIS_CONNECTION` (read in `Program.cs`, `DependencyInjection.cs`).

**Production Provider**: Self-hosted/managed Redis (per `docker-compose.yml`'s `image: redis:8`) — no managed vendor (AWS ElastiCache, Upstash, Redis Cloud) named anywhere.

**API Direction**: Direct TCP, `Kurx ⇄ StackExchange.Redis client ⇄ Redis`. No webhook/polling.

**Cost Model**: Same as Postgres — depends entirely on hosting choice, not decided in this repo.

**Production Readiness**: **Production Ready** as an integration for a single-instance deployment (works fine with `REDIS_CONNECTION` unset); **Needs Configuration** the moment Kurx runs more than one API instance (SignalR fan-out requires it) or wants persistent rate-limiting across restarts.

**Risks**: Silent degradation is a double-edged sword — if `REDIS_CONNECTION` is accidentally unset in a multi-instance production deploy, SignalR messages simply won't fan out across instances and chat rate-limiting silently becomes always-allow, with no error raised. Worth an explicit startup warning if `ASPNETCORE_ENVIRONMENT=Production` and Redis is unset, which does not currently exist.

**Alternatives**: N/A — Redis is the architectural choice for this role; no alternative evaluated.

---

## 10. Hangfire

**Purpose**: Scheduled/background job execution — seat-hold expiry, waitlist-offer expiry, ledger settlement.

**Current Status**: **Implemented** for the jobs that exist; all three jobs are internal DB-only operations with **no external API calls**.

**Used In**: Events (seat holds, waitlist), Wallet (ledger settlement).

**Backend Files**:
- `backend/Kurx.Infrastructure/Jobs/ExpireSeatHoldsJob.cs` — runs every minute.
- `backend/Kurx.Infrastructure/Jobs/ExpireWaitlistOffersJob.cs` — runs every 5 minutes.
- `backend/Kurx.Infrastructure/Jobs/CollectedToAvailableLedgerJob.cs` — runs daily, 7-day settlement window.
- `backend/Kurx.Api/Program.cs` — registers all **18** recurring jobs (this line said "all three", which was the D-029 figure); mounts the Hangfire dashboard at `/hangfire` (dev-only). Storage is schema `hangfire` (12 tables) inside the `kurx` database, on a separate connection pool (`Database__JobsMaxPoolSize`).
- `backend/Kurx.Infrastructure/Kurx.Infrastructure.csproj` — `Hangfire.AspNetCore` 1.8.23, `Hangfire.PostgreSql` 1.21.1 (storage backed by the same Postgres connection, not a separate service).

**Mobile Files**: None. **Web Files**: None.

**Environment Variables**: None specific to Hangfire — it reuses `ConnectionStrings__Default`.

**Production Provider**: N/A — Hangfire is a self-hosted open-source library, not a third-party SaaS; storage is your own Postgres instance.

**API Direction**: Scheduled Job — Hangfire's own scheduler triggers each job in-process on its cron/interval; no external call in or out for the jobs that exist today.

**Cost Model**: Free (open-source, MIT-licensed core; no paid tier is in use here).

**Production Readiness**: **Production Ready.**

**Risks**: Dashboard mounted only in dev today (good — a production Hangfire dashboard needs auth if ever exposed); job failures currently have no alerting wired (no email/Slack/PagerDuty on job exception — falls back to Hangfire's own retry/dashboard visibility only).

**Alternatives**: Quartz.NET, a cloud scheduler (AWS EventBridge, etc.) — none evaluated; Hangfire is a settled choice (D-029).

---

## 11. SignalR (Realtime)

**Purpose**: Live dashboards and chat — ticket sales, gate check-in scan feed, event chat.

**Current Status**: **Implemented**, fully internal (no third-party realtime vendor).

**Used In**: Chat, Events (check-in scan feed, sales dashboard).

**Backend Files**:
- `backend/Kurx.Api/Hubs/SalesHub.cs`, `ScanHub.cs`, `ChatHub.cs` — all `[Authorize]`, membership re-verified via DB on group join (D-017).
- `backend/Kurx.Api/Realtime/SignalRBroadcaster.cs` — `IRealtimeBroadcaster` implementation wrapping `IHubContext<...>`.
- `backend/Kurx.Api/Kurx.Api.csproj` — `Microsoft.AspNetCore.SignalR.StackExchangeRedis` 10.0.0 for the optional Redis backplane (§9).

**Mobile Files**: `mobile/lib/features/social/data/datasources/chat_hub_client.dart` (chat, D-108) and `mobile/lib/features/auth/data/datasources/login_status_stream.dart` (login, AM9) over `signalr_netcore`.

**Web Files**: `web/package.json` — `@microsoft/signalr` 8.0.7 (client package present; specific call sites not verified in this pass — flagged for a follow-up check if a precise usage map is needed).

**Environment Variables**: `REDIS_CONNECTION` (§9) — optional in dev, **required in Production since D-217**: startup refuses without it, because the fallback silently costs cross-instance fan-out, presence, and per-instance rate limiting.

**Production Provider**: N/A — SignalR is ASP.NET Core's own realtime framework, self-hosted; not a third-party SaaS.

**API Direction**: Realtime — WebSocket (with SSE/long-polling fallback), bidirectional; JWT passed via `?access_token=` query string on hub connections.

**Cost Model**: Free (part of ASP.NET Core; no Azure SignalR Service or other paid managed variant is in use).

**Production Readiness**: **Production Ready** for a single instance; **Needs Configuration** (Redis backplane, §9) for multi-instance deployments.

**Risks**: None specific beyond the Redis-dependency risk already noted in §9.

**Alternatives**: Azure SignalR Service (managed, paid) if horizontal scale without self-managing Redis is ever wanted — not evaluated in this repo.

---

## 12. QuestPDF

**Purpose**: Renders certificate PDFs and PNGs.

**Current Status**: **Implemented** (real, local library — not a network service, included here because it's a licensed third-party dependency worth tracking).

**Used In**: Certificates.

**Backend Files**:
- `backend/Kurx.Application/Abstractions/Providers.cs:84-89` — `ICertificateRenderer`.
- `backend/Kurx.Infrastructure/Providers/DocumentProviders.cs:53-167` — `CertificateRenderer`. Static constructor sets `QuestPDF.Settings.License = LicenseType.Community`; builds via `Document.Create(...)`, calls `document.GeneratePdf()` and `document.GenerateImages(new ImageGenerationSettings { RasterDpi = 150 })`. Entirely local rendering, no network call.
- `backend/Kurx.Infrastructure/Events/CertificateService.cs` — orchestrates render → `IStorage.PutAsync` → `IEmailSender.SendAsync`.
- `backend/Kurx.Infrastructure/Kurx.Infrastructure.csproj` — `QuestPDF` 2026.7.0.

**Mobile/Web Files**: None — certificates are generated server-side only.

**Environment Variables**: None.

**Production Provider**: N/A — self-contained library, `LicenseType.Community` (QuestPDF's free tier, revenue-capped per QuestPDF's own licensing terms — worth revisiting if Kurx's revenue crosses that threshold).

**API Direction**: N/A — local computation only.

**Cost Model**: Free under the Community license (revenue-cap conditions apply per QuestPDF's own terms — not independently verified against Kurx's actual revenue in this pass, since Kurx has no live revenue yet).

**Production Readiness**: **Production Ready.**

**Risks**: **License risk, not a technical one** — QuestPDF's Community license has an annual gross-revenue cap for the company using it; if Kurx starts generating real revenue, this needs a licensing review (upgrade to a paid QuestPDF license) before that threshold is crossed. Worth a calendar reminder tied to first real payment processing (§3), not a code fix.

**Alternatives**: iText (also has a similarly-structured commercial license), PDFsharp — not evaluated; QuestPDF is a settled choice (D-035).

---

## 13. QRCoder

**Purpose**: Generates QR codes (ticket check-in, certificate verification).

**Current Status**: **Implemented** (real, local library, despite the misleading class name).

**Used In**: Certificates, Tickets (check-in QR).

**Backend Files**:
- `backend/Kurx.Application/Abstractions/Providers.cs:91-96` — `IQrCodeGenerator`.
- `backend/Kurx.Infrastructure/Providers/DocumentProviders.cs:15-25` — `QrCodeGenerator` (renamed from the misleading `QrCodeGeneratorStub`; genuinely uses `QRCodeGenerator`/`PngByteQRCode` from the QRCoder package).
- `backend/Kurx.Infrastructure/Kurx.Infrastructure.csproj` — `QRCoder` 1.8.0.

**Mobile/Web Files**: None — QR PNGs are generated server-side and served as images/attachments.

**Environment Variables**: None.

**Production Provider**: N/A — self-contained library.

**API Direction**: N/A — local computation only.

**Cost Model**: Free (MIT-licensed).

**Production Readiness**: **Production Ready.** (Housekeeping done: the class was renamed `QrCodeGeneratorStub` → `QrCodeGenerator`, since it uses real QRCoder and is not a stub.)

**Risks**: None material.

**Alternatives**: N/A — settled choice, no reason to change.

---

## 14. Docnet.Core

**Purpose**: Intended for rasterizing custom (org-uploaded) PDF certificate templates to images.

**Current Status**: **True Stub** — package referenced, never invoked.

**Used In**: Certificates (custom-template path only — the system-template path via QuestPDF doesn't need it).

**Backend Files**:
- `backend/Kurx.Application/Abstractions/Providers.cs:98-103` — `IDocumentRasterizer`.
- `backend/Kurx.Infrastructure/Providers/DocumentProviders.cs:27-46` — `DocumentRasterizerStub`. Logs `LogWarning("DocumentRasterizer is a stub — returning empty PNG...")` and returns a hardcoded 1×1 PNG byte array. Comment: real implementation would use Docnet.Core "when certificate rendering ships" for custom templates.
- `backend/Kurx.Infrastructure/Kurx.Infrastructure.csproj` — `Docnet.Core` 2.6.0 (package present, zero actual invocations anywhere).

**Mobile/Web Files**: None.

**Environment Variables**: None.

**Production Provider**: N/A — self-contained library (not yet wired).

**API Direction**: N/A.

**Cost Model**: Free (MIT-licensed).

**Production Readiness**: **Stub.** Needed only when/if org-uploaded custom certificate templates ship (`TemplateMode.Custom`, explicitly out of scope per D-035).

**Risks**: None while unused; the returned 1×1 placeholder PNG would silently produce a broken-looking certificate image if the custom-template path were ever accidentally exercised before this is built — worth an explicit guard/feature-flag if custom templates are enabled in the UI before this ships.

**Alternatives**: PDFium bindings, ImageMagick+Ghostscript — not evaluated; Docnet.Core is the already-chosen (if unimplemented) library.

---

## 15. Serilog (Logging)

**Purpose**: Structured application logging.

**Current Status**: **Implemented**, console-sink only — **no external log aggregator wired**.

**Used In**: Every module (cross-cutting).

**Backend Files**:
- `backend/Kurx.Api/Program.cs` — `ReadFrom.Configuration(ctx.Configuration).WriteTo.Console()`.
- `backend/Kurx.Api/Middleware/CorrelationIdMiddleware.cs` — threads `X-Correlation-Id` into every log line.
- `backend/Kurx.Api/Kurx.Api.csproj` — `Serilog.AspNetCore` 10.0.0.

**Mobile/Web Files**: None — no shared logging pipeline between backend and clients.

**Environment Variables**: None specific (Serilog level config lives in `appsettings.json`).

**Production Provider**: **None** — logs go to stdout/console only. No Seq, Application Insights, Datadog, Sentry, or any log-shipping sink configured anywhere in `Program.cs` or the `.csproj` files.

**API Direction**: N/A — write-only, local process output.

**Cost Model**: Free today (no external sink); would become usage-based the moment a real log aggregator is added.

**Production Readiness**: **Needs Configuration** for production — console-only logging works for local dev and container log capture (e.g. `docker logs`), but has no searchable retention/alerting without a downstream collector (even just shipping container stdout to a hosted logging service).

**Risks**: No log retention/search today beyond whatever the hosting platform captures from stdout; no error alerting tied to logs (this overlaps with the Hangfire alerting gap in §10 — there is no monitoring/alerting integration anywhere in this codebase).

**Alternatives**: Seq (self-hosted, .NET-native), Better Stack, Datadog, Grafana Loki — none evaluated in code/docs.

---

## 16. GitHub Actions + GHCR + SSH deploy

**Purpose**: CI (build/test) and CD (build Docker images, deploy to staging/production).

**Current Status**: **Implemented.**

**Used In**: Platform-wide (build/deploy pipeline, not an app feature).

**Backend/Mobile/Web Files**: N/A (infrastructure, not application code) — `.github/workflows/ci.yml`, `.github/workflows/cd.yml`, `infra/Dockerfile.api`, `web/Dockerfile`, `admin/Dockerfile`.

**Environment Variables / Secrets** (GitHub repo secrets/variables, not app env vars): `STAGING_HOST`, `STAGING_SSH_USER`, `STAGING_SSH_KEY`, `STAGING_ENV`, `STAGING_URL`, `PRODUCTION_HOST`, `PRODUCTION_SSH_USER`, `PRODUCTION_SSH_KEY`, `PRODUCTION_ENV`, `PRODUCTION_URL`, plus the implicit `GITHUB_TOKEN` used to authenticate to GHCR.

**Production Provider**: GitHub Actions (CI runner + orchestration), GitHub Container Registry — `ghcr.io/{owner}/kurx-{api,web,admin}` (Docker image hosting), and a generic SSH-reachable host for staging/production (no named cloud provider — could be any VPS/VM; `appleboy/ssh-action` is the deploy mechanism).

**API Direction**: `ci.yml`: triggered on push/PR (webhook from GitHub itself). `cd.yml`: on push to `main` (auto-deploy staging) or on GitHub Release (manual-approval-gated production deploy via a GitHub Environment) — builds images, pushes to GHCR, then SSHes into the target host to `docker compose pull && up -d`, with a post-deploy `/health` check and rollback capture (`docker inspect ... > .rollback-*`).

**Cost Model**: GitHub Actions minutes are free for public repos / included quota for private repos on paid plans, then per-minute billed beyond quota. GHCR storage/bandwidth has its own free tier then usage-based pricing. The actual staging/production hosts (unspecified provider) have their own hosting cost, not tracked in this repo.

**Production Readiness**: **Needs Configuration** — the workflow files are real and complete, but require the secrets above to be set in the GitHub repo before `cd.yml` can deploy anything; no staging/production host currently exists per this codebase alone (host provisioning is out of scope of the repo itself).

**Risks**: SSH-key-based deploy is a real credential to protect (rotate the deploy key, restrict it to only what's needed on the target host); no infrastructure-as-code (Terraform/Ansible/etc.) for the target hosts themselves — they're assumed to be manually provisioned with Docker + this repo's `docker-compose.yml` already in place at `/opt/kurx`.

**Alternatives**: A managed platform (Fly.io, Render, Railway) could replace the manual-SSH deploy model — not evaluated; current approach is a deliberate "we own the box" choice.

---

## 17. Workflow automation — *removed (D-337)*

**There is no workflow-automation service.** An unused self-hosted **n8n** container sat in both compose
files from an early commit until 2026-08-14, when it was deleted along with its `kurx_n8ndata` volume, its
published `:5678` port, its `N8N_*` credentials and the `infra/postgres-init/01-n8n-db.sql` script that
created an empty `n8n` database on every fresh Postgres volume. Nothing in `backend/`, `web/`, `admin/` or
`mobile/` ever referenced it — verified by repo-wide grep at deletion — and no workflow was ever authored.

**Background work is Hangfire's**, and always was: 18 recurring jobs, verified live against real Postgres
(§10). They are not substitutes. Every Kurx job reads and writes the application database through EF Core
inside the app's own DI scope, transactions and `pg_advisory_xact_lock` calls; reaching that from an
external workflow runner would require exposing an HTTP endpoint per job — strictly more attack surface for
strictly less type safety. n8n's niche is glue *between* SaaS products, which Kurx has no case for today.

**If a case ever appears** (Slack alerts on job failure, ops runbooks over third-party APIs), re-adding it
is a new `D-NNN` — and it needs auth, a non-default encryption key and a port that is not published to the
host, none of which the deleted service had by default.

---

## 18. Missing / not-yet-selected integrations

These are features Kurx's own product documentation describes as needing an external provider, where **no code, no abstraction interface, and in most cases no vendor decision** exists yet — distinct from §1–7 above, which at least have an interface and a stub/mock.

| Feature | Provider needed | Verified status |
|---|---|---|
| **Maps** (venue location display) | Google Maps / Mapbox | **Not Started.** `web/app/e/[slug]/page.tsx` renders venue as plain text with a static `MapPin` icon (lucide-react glyph, not a map). No maps package in `web/package.json` or `mobile/pubspec.yaml`. `Event` entity stores lat/lng as plain columns (D-026) but nothing renders them on a map anywhere. |
| **Google/Apple Sign-In (OAuth)** | Google Identity, Sign in with Apple | **Not Started.** Repo-wide grep for OAuth/Google/Apple sign-in code found zero hits; the only auth mechanism anywhere is WhatsApp OTP → JWT. No `google_sign_in`/`sign_in_with_apple` package in `mobile/pubspec.yaml`; no `next-auth` or OAuth library in `web/package.json`. |
| **Analytics** (product/usage analytics) | e.g. PostHog, Mixpanel, GA | **Not Started.** No analytics SDK in `web/package.json` or `mobile/pubspec.yaml`. `Event.ViewCount` (D-018) is the only "analytics" that exists — a single DB counter, not a real analytics pipeline. |
| **Crash reporting** | e.g. Sentry, Firebase Crashlytics | **Not Started.** No Sentry/Crashlytics package anywhere in `web/package.json` or `mobile/pubspec.yaml`. |
| **Search** (beyond simple substring match) | e.g. Algolia, Elasticsearch, Postgres full-text | **Not Started as "search engine."** Verified: `EventService.cs`/`CategoryService.cs` use `EF.Functions.ILike(...)` (case-insensitive substring match) — there is **no** `tsvector`/`to_tsquery` full-text search despite `docs/PROJECT_HANDBOOK.md` describing "Postgres full-text search" as the starting point; that claim does not match the code. |
| **Video** (livestreaming) | e.g. YouTube Live, Mux, Agora | **Not Started.** No video SDK/embed code found anywhere; "Live Streaming" is listed as an unbuilt module in `docs/PROJECT_HANDBOOK.md`'s module catalog. |
| **Voice** (e.g. IVR, voice OTP fallback) | e.g. Twilio Voice, Exotel | **Not Started.** No code or doc mention beyond this audit's own template. |
| **AI** (any LLM/ML feature) | Not applicable to Kurx's current product scope | **Not Started.** No AI/ML SDK, API key, or feature found referenced anywhere in the codebase or product docs. |
| **CDN** (dedicated, beyond whatever the storage provider offers) | e.g. Cloudflare, CloudFront | **Not Started.** `next.config.mjs`'s `images.remotePatterns` allows any HTTPS host — a permissive placeholder, not a configured CDN. No CDN vendor named in any doc. |
| **SMS (as a channel distinct from WhatsApp)** | e.g. MSG91, Twilio, AWS SNS | **Not Started, and not even abstracted.** `docs/architecture/overview.md` lists an `ISmsSender` interface, but it **does not exist** in `backend/Kurx.Application/Abstractions/Providers.cs` (verified by direct file read and grep) — this appears to be aspirational documentation drift, not a real gap between interface-and-implementation like the other providers in this list. |

---

## 19. Final summary table

| Service | Provider | Purpose | Current Status | Production Ready | Configuration Complete | Estimated Cost Model | Criticality |
|---|---|---|---|---|---|---|---|
| WhatsApp OTP (outbound) | WhatsApp Cloud API (Meta) | OTP delivery, messaging | Stub (console) | No | No | Per-message | **Critical** — the only login mechanism |
| WhatsApp webhook (inbound) | WhatsApp Cloud API (Meta) | Delivery-status receipt | Implemented, untested live | Needs Testing | Partial (verify token/secret only) | N/A | Medium |
| Email | AWS SES | Ticket resend, certificates | Stub (console) | No | No | Per-email (~$0.10/1k) | High |
| Payments | Razorpay | Checkout, capture, refunds | Mock (write-path live D-049, adapter still mock) | No | No | % per transaction | **Critical** — blocks all real paid tickets |
| Payouts | Razorpay Route | Organizer settlement | Mock | No | No | Per-transfer fee | **Critical** — blocks all organizer payouts |
| KYC | Unnamed (no vendor chosen) | Bank/PAN/DigiLocker verification | Mock | No | No (no vendor selected) | Per-verification | High |
| Storage | AWS S3 | Media/certificate storage | Stub (local disk) | No | No | Usage-based (GB + requests) | High |
| Push notifications | Firebase Cloud Messaging | Mobile push | Stub (console); mobile side also unbuilt | No | No | Free | Medium |
| Database | PostgreSQL 17 | System of record | Implemented | Yes (needs prod host) | Partial | Hosting-dependent | **Critical** |
| Cache/Realtime backplane/Rate-limit | Redis | Cache, SignalR fan-out, chat limiter | Implemented (optional) | Yes (single instance) | Partial | Hosting-dependent | Medium (High for multi-instance) |
| Background jobs | Hangfire (self-hosted) | Seat-hold/waitlist expiry, ledger | Implemented | Yes | Yes | Free | High |
| Realtime | SignalR (self-hosted) | Chat, sales/scan live feeds | Implemented | Yes (single instance) | Yes | Free | Medium |
| Certificate rendering | QuestPDF | PDF/PNG certificate generation | Implemented | Yes | Yes | Free (license cap applies) | Medium |
| QR generation | QRCoder | Ticket/certificate QR codes | Implemented | Yes | Yes | Free | Medium |
| PDF rasterization | Docnet.Core | Custom template rendering | True stub | No | No | Free | Low (custom templates unbuilt) |
| Logging | Serilog (console only) | Structured logs | Implemented, no external sink | Needs Configuration | Partial | Free today | Medium |
| CI/CD & registry | GitHub Actions + GHCR | Build/test/deploy | Implemented | Needs Configuration (secrets, target hosts) | Partial | Usage-based (minutes/storage) | High |
| Deploy target | Generic SSH host (unspecified provider) | Staging/production hosting | Not provisioned in-repo | No | No | Hosting-dependent | **Critical** |
| Workflow automation | *none* | — | Removed 2026-08-14 (D-337); Hangfire owns all background work | N/A | N/A | N/A | None |
| Maps | Not chosen | Venue location display | Not Started | No | No | Usage-based (typically) | Medium |
| OAuth (Google/Apple) | Not chosen | Alternate login | Not Started | No | No | Free | Low (OTP already works) |
| Analytics | Not chosen | Product analytics | Not Started | No | No | Usage-based/free tier | Low |
| Crash reporting | Not chosen | Mobile/web crash visibility | Not Started | No | No | Free tier available | Medium |
| Search | Not chosen (currently `ILIKE`) | Better event/org search | Not Started | No | No | N/A | Low |
| Video | Not chosen | Livestreaming | Not Started | No | No | Usage-based | Low (post-MVP) |
| Voice | Not chosen | Voice OTP fallback | Not Started | No | No | Per-minute | Low |
| AI | Not applicable | — | Not Started | No | No | N/A | None currently |
| CDN | Not chosen | Static/media delivery | Not Started | No | No | Usage-based | Low |
| SMS | Not chosen (interface doesn't exist despite doc claim) | Alternate OTP channel | Not Started | No | No | Per-message | Low |

**Bottom line for production readiness**: every "Critical" row above (WhatsApp OTP, Payments, Payouts, Database, Deploy target) is either fully mocked/stubbed or unprovisioned. None of Kurx's revenue-critical or login-critical paths can go live without real implementations for WhatsApp Cloud API, Razorpay (both products), and a provisioned production Postgres + deploy host. This matches — and gives file-level evidence for — `docs/PROJECT_HANDBOOK.md`'s own (accurate, in this instance) summary: "No real third-party providers — Razorpay, AWS SES, WhatsApp Cloud API, FCM, and S3 are all mocked/console/localdisk."
