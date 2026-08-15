# Security Rules

Full detail: `docs/security/overview.md`, `docs/security/secret-management.md`. This file is the enforceable checklist summary.

## Baseline (every change)

- OWASP top 10 applies: injection, broken auth, sensitive data exposure, XXE, broken access control, misconfig, XSS, insecure deserialization, vulnerable components, insufficient logging.
- No secrets in code, logs, or committed config. Production secret validation covers connection strings too — don't bypass it for convenience.
- Errors never leak internals: all error responses are RFC7807 `ProblemDetails`, no raw exception text/stack traces to clients.

## Auth-specific

- JWT + refresh rotation with reuse-revokes-all is the existing model — any auth change must preserve that property, not weaken it.
- Resource-scoped authorization (org/event roles) is checked live per request, never cached/trusted from a token claim alone.
- **Event authorization goes through `IEventAuthority` and nowhere else (D-269/D-272)** — *both* management and audience. One ordered level (`None < Participant < Staff < Manager < Admin`), one permission table. The event's **creator** reaches `Manager` with no membership (D-268); a `Representative` and an `Owner`/`Manager` seat do too; a live ticket or an accepted programme participation reaches `Participant` (D-272); `Finance` holds nothing on events. Writing a private `CanManage`/`RoleAsync` in a service is a security defect, not a style one — there were eleven such copies and their drift silently hid two live permission bugs. Documented exceptions (`ApprovalService` needs an exact role match; `Orgs/*` is org RBAC; the `Owner`-or-`Finance` financial bar is the ladder's inverse) are named in D-269/D-272 and are not precedent.
- **An organization membership is never, by itself, event access (D-272).** `db.Memberships.Any(m => m.OrgId == ev.OrgId && m.UserId == x)` as an authorization check is the bug pattern: it grants `Finance`, and it grants the event's creator *nothing* unless a self-representation row happens to exist. Resolve through `IEventAuthority` instead. Where an EF predicate genuinely cannot resolve per caller, use `EventAuthority.AudienceRoles`/`ModeratorRoles` — derived from `LevelFor`, never a hand-written role list beside it.
- **Audience permissions are not management permissions (D-272).** `Participate` (feed read, post attachment, chat room) sits at `Participant`; `ModerateAudience` (chat `Host`) at `Staff`. Gating an audience surface on `ManageContent` is a silent lockout of every ticket holder.
- **Authority and capability are separate systems.** `IEventAuthority` decides *who may act*; `ICapabilityService` decides *what an event supports*. Neither calls the other. Never gate a permission on a capability, or vice versa.
- Platform roles (SuperAdmin/Reviewer/Finance/Support) live in `platform_roles` and are resolved live per request by `PlatformRoleClaimsTransformation`, which **strips any token-supplied platform claim** before re-reading the DB (M2, D-040). Never encode platform authority in a JWT; never trust a `platform_role`/`kurx_admin` claim that came from the token.
- SignalR hubs authenticate via query-string JWT (unavoidable for WS) and re-verify membership on every group join — a connected socket must never be able to guess its way into another org's feed.
- **Every OTP in the platform — login included — goes through `IOtpService`** and is stored in `otp_codes` as `HMACSHA256(OTP_PEPPER)` (D-215). Never hash a low-entropy credential with a bare digest: a 6-digit code has a 10⁶ keyspace, so an unsalted SHA-256 is fully precomputable and the stored hash is effectively plaintext. (Unsalted `Sha256` remains correct for the 48-byte refresh/browser tokens — the distinction is entropy, not the algorithm.) The legacy `otp_requests` table is retired; do not add readers or writers.
- **Keys are per-security-domain, never shared.** `TICKET_HMAC_SECRET` (gate tickets) is distinct from `JWT_SECRET` (sessions) and Production refuses to start without it (D-216). Never reintroduce a `?? jwtSecret` style fallback: it makes a leak of either compromise both, and rotating one silently invalidates the other's artifacts. Resolve secrets through `ISecretProvider`, not `IConfiguration`, or `SECRETS_PROVIDER=aws` is silently bypassed.
- **A one-time code is an accepted second factor, and the ranking is what bounds it (D-280).** This reverses the old rule at `LoginApprovalService.cs` that OTP was never a login factor — which was correct about the risk (**password + SIM swap = takeover**) and wrong about the cost: an account with a password and no enrolled device could not log in *at all*, which on mobile is the common case. Three existing mechanisms keep the trade bounded and must not be quietly removed: device approval and passkey **outrank** a code wherever both exist, so the strong path stays the default; `IRiskEngine` may still demand device approval on an elevated-risk sign-in, so *available* is not *sufficient*; and step-up still gates recovery-code minting and the other sensitive surfaces, so an OTP-established session's blast radius stops at "signed in". **Never send a login code to an unverified address** (D-282) — an unverified address may belong to somebody else, which makes it an account-takeover primitive rather than a convenience.

- **The delivery channel of an OTP is the server's decision, in one place (D-281).** `OtpChannelPolicy.For(purpose)` — not the call site. Every call site used to name a channel and every one of them named WhatsApp, so `OtpChannel.Sms` was selected by nothing and the finished, DLT-aware `SnsSmsProvider` was unreachable: **no OTP could reach any user on any channel**, and the login rail terminated in a log line. WhatsApp keeps its provider, webhook and message log but is never selected for authentication — a WhatsApp account is portable across devices and recoverable through a takeover chain we do not control. Relatedly, `IssueAsync` now reports a failed send instead of returning `Ok` regardless; without that no caller could ever fall back.

- **Which second factors an account has is disclosed only after the password verifies (D-283).** The list is account-specific, so it is exactly the kind of thing the decoy responses on `/login/start`, `/passkeys/login/options` and `/recovery/start` exist to withhold. Gating it behind factor 1 means it tells an attacker nothing they did not already have. Unavailable methods are **absent** from the response, never present-and-disabled — a disabled entry leaks the same fact through the UI. Clients render the list and must not filter, reorder or extend it; a genuine client constraint is reported as a capability on the request so the decision stays server-side.

- **Presence is not strength.** Every name in `RequiredSecrets.Names` is run through `SecretValidation.RequireStrongProductionSecret` in Production, and every committed dev placeholder is listed in `KnownDevValues` (D-244). `OTP_PEPPER` had only a presence check and so booted Production on its committed placeholder — the worst one to miss, because a guessable pepper produces OTP hashes that verify correctly and look identical to properly peppered ones (D-115). When adding a required secret: add the placeholder to `KnownDevValues`, and keep the strength check **outside** the per-secret `try` in `ValidateAsync` — that block turns any exception into "secret unavailable", which would report a present-but-unusable secret as missing.
- **Refresh-token rotation is single-use, claimed atomically** (D-241): `UPDATE … WHERE Id = @id AND RevokedAt IS NULL`, and 0 rows means someone else won. Reading `RevokedAt` and assigning it as a tracked change let two concurrent refreshes of one token both mint a chain, which silently defeats reuse detection. The race loser is refused but its family is *not* revoked — concurrent refreshes are ordinary client behaviour, not proof of theft; real reuse (presented after rotation settles) still trips the full revoke. Clients must single-flight refreshes: mobile via `_inFlightRefresh`, web/admin via the per-token map in `middleware.ts`.
- **There is exactly one way to obtain a session: the real authentication flow.** No developer login, no seeded identity, no fixed OTP, no impersonation endpoint, no environment- or compile-gated shortcut — in any build, any configuration, any environment (D-274). The three prior attempts at a "safe" hatch are all deleted: `DEV_OTP_CODE` (runtime-gated fixed code), `ADMIN_DEV_ALLOW_ALL` (fake SuperAdmin for any authenticated user), and the `/v1/dev/*` Developer Workspace (compile-gated via `DEV_AUTH`, D-125). Every one of them shipped believing its own guard made it safe. Do not reintroduce the pattern under any framing. Locally the flow is identical to production; the console providers simply log the code the user would have received (`[sms→console]`), which is delivery, not a bypass.
- **The first SuperAdmin comes from configuration, never from code that creates one** (D-274). `SuperAdminBootstrap` grants `SuperAdmin` to an **already-registered** user named by `SUPERADMIN_BOOTSTRAP_PHONE`, only while zero live SuperAdmins exist, and writes an audit row. It must never create a user, never grant a second time, and never accept anything but an account that authenticated for itself first. Every subsequent grant goes through `POST /v1/admin/staff/grant`.

- **Rate limits are counted in Redis, not per process** (D-255). The in-memory `FixedWindow`/`SlidingWindow` primitives keep their counter in process memory, so behind N replicas every configured limit silently becomes N× — including the per-IP shield in front of OTP issuance, whose entire job is bounding how fast one host can enumerate phone numbers. Horizontal scaling relaxed it in proportion to capacity. `RedisFixedWindowRateLimiter` does the increment and its expiry in **one Lua script**: a bare `INCR` then `EXPIRE` leaks a permanent key if the process dies between them, and that key rejects its partition forever. Partition keys must stay namespaced (`otp:`, `resume:`) — in-process each policy owned a separate limiter, so a bare IP key was safe; sharing one Redis keyspace removes that isolation and two policies on the same address would increment one counter.
  - It **fails open**: an unreachable Redis allows the request and logs a warning. Rejecting would turn a cache blip into a total outage. Treat the edge limiter as a throughput shield and never as the authoritative control.
  - The fail-open trade depends on the durable Postgres limits in `OtpService`/`AuthService` (D-005) actually holding when Redis is gone. They now do: `OtpService.VerifyAsync` claims both the attempt and the consumption with conditional `ExecuteUpdateAsync` (D-261). It previously read-compared-then-incremented, which let N parallel guesses share one read and made the 5-attempt cap over a **6-digit** keyspace defeatable by concurrency alone — the exact case the edge limiter is *not* there to catch.
- **A rate limit or attempt cap is claimed, never counted.** Read-compare-then-increment is the recurring shape of this bug: it has appeared in refresh rotation (D-240), OTP verification (D-261) and the challenge match-number cap. If a counter enforces a security boundary, the comparison and the increment must be one conditional `UPDATE … WHERE <still under the cap>`, and 0 rows affected is the refusal. `ChallengeService.VerifyMatchNumberAsync` is the reference implementation.
  - The `heavy` policy stays **local on purpose**: it is a concurrency limiter, and a shared concurrency counter leaks permits forever whenever an instance dies mid-request.
- **There is deliberately no elevated admin tier in the global throttle** (D-252). The limiter runs between `UseAuthentication` and `UseAuthorization`, so `PlatformRoleClaimsTransformation` has not run and `kurx_admin` cannot be present — the branch that tested for it was unreachable. Making it real would mean resolving platform roles from the database *before* the throttle, i.e. an unthrottled query per request gated on nothing, through the exact component that exists to prevent that.
- **Containers run unprivileged** (D-256) — see `deployment.md` for the writable-path caveat.
- **A client never enforces a stricter auth rule than the server** (D-329). `PasswordResetService` accepts a recovery code **or** a satisfied step-up as factor 2; web, admin and Flutter each required the code to submit at all, and since codes are minted only by one step-up-gated call that nothing invokes at registration, the screen for a locked-out user refused nearly every account before a request was sent. A client-side gate stricter than the server is invisible to every check the platform has — the contract gate compares the spec to the **server**, so a client inventing a requirement produces no diff, no failing test and no drift alert. Render the server's rule, submit, and let the refusal name the real state; a control that never enables tells the user nothing. This is the auth-path form of the capability engine's rule that clients render capabilities and never decide them.

## User-safety invariants (D-262 · D-263 · D-264)

These protect users from each other rather than from an attacker on the wire, and each one is a
promise the UI makes that the server has to keep.

- **A security notification can never be suppressed.** `NotificationCategories.IsAlwaysDelivered` is
  checked on the write path *and* again inside `NotifyAsync`, so a preference row written straight to
  the database still cannot silence a new-device sign-in or a phone change. Consent to be kept quiet
  about your own account being taken over is not consent we accept.
- **A block is symmetric and enforced on live read paths.** The `user_blocks` row is one-way; if
  *either* direction exists, neither party sees the other's posts or comments and neither can open or
  deliver a DM. A block that only hid one direction would leave the blocked person still reading and
  still able to reach out — which is not what the word means to the person who asked for it.
- **Blocks and DM request state are re-checked per request, never cached.** The D-015 rule for
  resource roles applies here for the same reason: a block has to bite on the next message, not the
  next login.
- **A DM from a non-ally notifies nobody until accepted** (D-264). Without that gate an open DM
  endpoint is an abuse surface from the hour it ships.
- **Mention notifications are gated on the recipient's own visibility** (D-262). A mention is the one
  notification whose recipient the author picks freely; ungated, `@victim` inside an `only_me` post is
  a notification channel to anyone on the platform.
- **Erasure does not override retention.** Account deletion anonymises the user row and soft-deletes
  authored content; orders, payments, refunds, ledger entries, tickets, certificates and audit logs
  are retained by design (D-263). An audit log the audited party can erase is not an audit log.
- **Hidden/blocked/invisible resources answer 404, never 403** — extending D-018 to posts, comments
  and DM rooms. A 403 confirms the thing exists, which is the fact being withheld.

## A cache outlives a request, never a session (D-308)

Anything written on a user's behalf and readable **without re-authenticating** belongs to that session,
and the session ending is the moment it stops being theirs. Applies to every client-side store.

Flutter's `kurx_cache` Hive box survived logout, which cleared only the tokens. Its keys are scoped to a
**room**, not a user — `chat_rooms`, `chat_msgs_{roomId}`, `chat_outbox_{roomId}`, `chat_read_{roomId}` —
so the next person to sign in on the device read exactly the same keys. Two consequences, both reachable:

- **Disclosure.** The room is seeded from cache *before* any network call, and the room/message reads fall
  back to cache when offline. User B saw user A's rooms and messages.
- **Misattribution.** Opening a room flushes `chat_outbox_{roomId}` through the shared Dio, which carries
  **whoever is signed in now** — user A's queued message delivered as user B.

Rules that follow:

- **Clear the whole box, not a list of keys.** The box is the boundary; a per-key cleanup is how the next
  cache added to it silently re-opens the hole.
- **Clear on logout, awaited, before the auth state flips** — the router redirects on that state, so
  clearing afterwards races a screen already reading what you are emptying.
- **The backstop belongs at start-up with no session, not on login.** Clearing on login races the new
  user's first fetches and can delete what they just cached. A process killed mid-logout leaves "tokens
  gone, data on disk", which on next launch is exactly the no-session branch.
- **Never block sign-out on a storage failure.** Leaving someone signed in because a disk write failed is
  strictly worse than a cache that outlives one more session.
- **Per-user cache keys are the wrong fix** for a shared device: the data stays on disk and is still
  recoverable by anyone who can sign in — the same disclosure, one indirection later.

## A cache in front of a security decision is a parse cache, never the authority (D-344)

The JWT resolver looked up `kid` in the live published key set on **every** request, then parsed that key's
SPKI into an `ECDsaSecurityKey` — allocating an `ECDsa` it never disposed (`ECDsaSecurityKey` does not take
ownership; every other `ECDsa.Create()` in the repo uses `using`, `TokenService` included). Caching the
parse is right. Caching the *lookup* would be a vulnerability: retirement and compromise are enforced by
`GetValidationKeysAsync` returning only `Active`/`Retiring` rows, so a cache consulted before that check
would keep honouring a key an attacker holds.

The ordering rule: **resolve authority first, then use the cache purely to avoid recomputation.** In
`ValidationKeyCache` the `published.FirstOrDefault(k => k.KeyId == kid)` lookup still runs on every
request; a key that is no longer published never reaches the cache, so it cannot be resurrected by one.
The cache key includes the SPKI, so re-publishing different material under an existing `kid` re-parses.

Two things this exposed, both worth copying:

- **Test the decision where it is enforced.** `A_compromised_key_is_dropped_immediately_with_no_grace_period`
  asserts at the service layer and never makes a request, so it would pass even if the HTTP resolver served
  a stale cached key. A cache bug is invisible to a test that does not go through the cache.
- **`GetOrAdd` needs `Lazy` with `ExecutionAndPublication`** when the value owns a native handle. A bare
  factory can run on several threads and discard the losers — abandoning exactly the handle the cache
  exists to stop abandoning.

## Process gate

Any change touching auth, payments, PII, or KYC/bank data runs `.claude/commands/security-review.md` before merge — not optional, not deferred to "later."

## Destructive/irreversible actions

Never run `--no-verify`, force-push, or edit `.env`/secret files without stopping to confirm with the user first — these are called out explicitly in `CLAUDE.md` as hard-to-reverse.

## Validate at the boundary; a normalizer that degrades is not a gate (D-317)

`NormalizePhone` parses with libphonenumber and, on failure, returns the input's bare digits. That fallback
is correct — inventing a country is what once sent a user's code to a stranger (D-290) — but it is **not a
rejection**, and `RequestOtpAsync` treated it as one by checking only length. `+911111111111` got a code for
a destination that cannot exist.

**A function that returns a degraded value on failure has told the caller nothing. Whoever needs a verdict
must ask for one.** The guard re-parses the normalized value and refuses with the existing `invalid_phone`;
it cannot reject a good number, because every success branch returns `parsed.E164[1..]` and restoring the
'+' reconstructs exactly that. Do **not** extend this to stored values: `ToOtpDestination` stays
format-only, and D-290's rule against re-normalizing a stored bare-digit phone is unchanged.

**Fixtures made of impossible data cannot catch bugs about impossible data.** Fixing this took the suite to
283 failures, all of them tests registering users with eleven-digit "Indian" numbers that only worked
because of the fallback. When adding a phone fixture, use a number libphonenumber accepts.

## Fraud signals are measured before they are enforced (D-317)

`FraudSignalKind.DisposableContact` sat with no producer. It now has one, scoring 10 against a threshold of
100 — deliberately non-blocking.

Email is **not** an authentication factor here (phone OTP is), so a throwaway address buys an attacker
little, and refusing one at signup would hit students on temporary institutional addresses hardest. The
capability that carries risk — organising paid public events — is already gated on identity + PAN + bank +
fraud-clear (D-307). So a new signal feeds that score and lets the existing gate decide. **Do not add a
second fraud verdict, table or endpoint alongside it.**

> Outside Production the *identity + PAN + bank* half of that gate can be switched off
> (`IDENTITY_VERIFICATION_BYPASS`, D-323 — those proofs are all mock-backed). **`fraud-clear` is
> deliberately outside the bypass** and still gates the capability either way, which is exactly why this
> section's reasoning survives it: the fraud score remains the live input it was designed to be. A change
> that folded `fraudClear` into the bypass would silently delete this control in every dev and test
> environment, and is the one modification to that flag a reviewer should refuse outright. Raising a score to a blocking weight is a product
decision taken against observed volume, never at the moment the producer is written.

The producer runs *after* the verification commits and only on success — scoring a submitted address scores
typos — is de-duplicated per (subject, value) so repeating a legitimate action cannot accumulate score, and
is best-effort so a signal write can never fail the ceremony it observes.

## A phone lookup that re-normalizes can fail open (D-290)

`FraudService.IsUserClearAsync` checked the blacklist with `user.Phone`, which the lookup re-normalized —
re-homing international numbers to India, matching no blacklist row, and letting a banned account straight
through. Security checks keyed on a phone must read `u.PhoneE164 ?? u.Phone`; see
[database-conventions](database-conventions.md). The general lesson: a normalization mismatch in a
*deny* check is silent and fails in the attacker's favour, so any new phone-keyed gate needs a test with a
non-Indian number, not just the Indian fixtures the suite is full of.

## An upload is scanned where its key is CLAIMED, never where it is PUT (D-338)

`IFileScanner` shipped real in D-298 but was injected into two services — `ChatService` and `PostService`.
The other six presign paths persisted a storage key with **no scan at any point**, and four of those six are
the verification documents (govt ID, PAN, letterhead, membership proof) a `VerificationReviewer` downloads
and opens in the admin console. Coverage is now all eight, via `UploadScanGate`.

**Where a new upload path must scan.** At the point the server first *claims* the key — the confirm/attach/
submit step — never in `PUT /v1/storage/{*key}`. That endpoint looks like the one choke point every upload
passes through, and today it is, but only because `LocalDiskStorage` presigns back to our own API. An **S3
presign uploads directly to S3 and never enters this process**, so a gate there would cover everything until
the S3 adapter ships and then silently cover nothing, with no test failing. Adding a presign method without
a matching gate at its claim site re-opens this hole.

**`FILE_SCANNER=none` is refused in Production**, the same shape as `SIGNING_KEY_PROTECTION=none` and a
missing `REDIS_CONNECTION`. `NoOpFileScanner` reports Clean without reading the bytes, and `.env.example`
ships `none`, so "forgot the variable" was the default path rather than an unusual mistake.

**Testing a scanner gate needs no clamd.** Point `ClamAvFileScanner` at a dead port
(`UnreachableScannerFactory`): every scan returns `ScanFailed`, which a correctly-gated path must refuse. It
runs on any machine, unlike the EICAR tests that need a live clamd (`docker compose up -d clamav`). Assert the refusal
is *total* — no row persisted, no half-staged aggregate — not merely that an error came back.
