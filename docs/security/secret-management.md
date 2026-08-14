# Secret management

## Policy

1. **No real secret is ever committed.** Only inert, clearly-labeled dev placeholders
   appear in git (`.env.example`, `backend/Kurx.Api/appsettings.Development.json`,
   `docker-compose.yml`). `.env` itself is gitignored.
2. **Every secret is read through `ISecretProvider`, never from `IConfiguration` directly.**
   Which backing store that resolves to is a deployment choice (`SECRETS_PROVIDER`, below) —
   environment variables locally and for env-var-injecting platforms, AWS Secrets Manager
   elsewhere. No call site knows which. Reading `IConfiguration` for a secret at a new call
   site is a bug, not a shortcut: it silently bypasses Secrets Manager for that one value.
3. **Production fails closed.** When `ASPNETCORE_ENVIRONMENT=Production`, the API
   refuses to start if a required secret is missing, unreadable, too short, or still set to the
   committed dev placeholder. See `SecretValidation` (`Kurx.Infrastructure/Configuration/`),
   `RequiredSecrets.ValidateAsync` (`Kurx.Infrastructure/Secrets/`) and `JwtOptions.FromSecret`
   (`Kurx.Infrastructure/Auth/TokenService.cs`).

## Where secrets come from — `SECRETS_PROVIDER`

`SecretProviderFactory.Create` (`Kurx.Infrastructure/Secrets/SecretProviders.cs`) selects one
implementation of `ISecretProvider` (AM10, D-101a). An unrecognised value **throws at startup**
rather than falling back — never silently read secrets from somewhere unintended.

| Value | Implementation | Use it when |
|---|---|---|
| `configuration` *(default)* | `ConfigurationSecretProvider` — reads `IConfiguration`, which already layers appsettings, user-secrets **and environment variables** | Local dev, and any deployment that injects secrets as env vars (ECS task secrets, Kubernetes secrets). This is a production-correct answer, not a stub. |
| `aws` | `AwsSecretsManagerProvider` — AWS Secrets Manager behind a short TTL cache | Secrets live in Secrets Manager and are rotated there. |

**AWS specifics.** `AWS_SECRETS_PREFIX` (optional) namespaces one account across environments
(`kurx/production/JWT_SECRET`). `AWS_SECRETS_CACHE_MINUTES` (default `5`) sets the TTL — caching is
a requirement, not an optimisation, because Secrets Manager is billed per call and rate-limited.
Only **successful** reads are cached, so a transient AWS error cannot pin the app into a broken
state until the TTL expires. `ISecretProvider.InvalidateAsync` exists for rotation: a caller that
sees an auth failure from a downstream dependency should invalidate and retry **once** before
concluding the credential is genuinely wrong.

> ⚠️ `AwsSecretsManagerProvider` has **never been executed against real AWS** from this repo
> (D-101a). It is reviewed, compiled and unit-tested against a fake `ISecretsManagerClient`; the
> live API interaction is pending deployment configuration. Treat the first `SECRETS_PROVIDER=aws`
> boot as an integration test.

**Bootstrap ordering.** `JWT_SECRET` and `TICKET_HMAC_SECRET` are needed while configuring bearer
authentication — *before* the DI container exists. `Program.cs` therefore builds a provider through
the same `SecretProviderFactory` rather than reading `IConfiguration`, so the selection logic cannot
drift between the bootstrap path and the DI registration.

## What's validated today

`RequiredSecrets.Names` is deliberately short — anything with a working default belongs in
configuration, not here.

| Secret | Where it's read | Production check |
|---|---|---|
| `JWT_SECRET` | `ISecretProvider` → `JwtOptions.FromSecret` | must be set, ≥32 chars, not the committed placeholder. In `RequiredSecrets` — a missing value is fatal in every environment. |
| `TICKET_HMAC_SECRET` | `ISecretProvider` → `JwtOptions.FromSecret` | must be set, ≥32 chars, not the committed placeholder (**D-216**) |
| `OTP_PEPPER` | `ISecretProvider` → `RequiredSecrets` → `OtpService` | must be set **and strength-checked** — presence alone was not enough (D-115), see below |
| `REDIS_CONNECTION` | `AddKurxInfrastructure` | must be set in Production — startup refuses without it (**D-217**) |
| `ConnectionStrings__Default` | `AddKurxInfrastructure` | must be set (any environment); must not carry the committed dev password (`Password=kurx`) in Production |
| `SIGNING_KEY_PROTECTION` | `AddKurxInfrastructure` | `kms` \| `none`; defaults to `kms` in Production. `none` is **refused** in Production — an unwrapped private signing key makes a database dump a complete authentication bypass (**D-102a**). `kms` additionally requires `AWS_KMS_SIGNING_KEY_ID`. |

**Why `OTP_PEPPER` is strength-checked and not merely present.** It is the one secret whose
misconfiguration nothing downstream ever surfaces: OTP hashes computed under a guessable pepper
behave exactly like correct ones, so the committed placeholder booted a Production host clean.
`RequiredSecrets.ValidateAsync` runs `RequireStrongProductionSecret` over every *present* secret in
Production — outside the read loop's `try`, deliberately, so a weak value reports as weak rather
than being swallowed into "unavailable".

**Validation runs before the database is touched**, so a missing secret stops the deploy instead of
surfacing as an intermittent failure on the first login afterwards. It also proves the *provider*
works: a bad AWS region or a missing IAM permission fails the boot, not an incident.

**`TICKET_HMAC_SECRET` is live and mandatory in Production (D-216).** It signs the gate-entry
ticket code (`TokenService.SignTicketCode`) and is verified at the gate by `GateEntryService`, with
further call sites in `OrderService`, `TicketTransferService`, `SeatBlockService` and
`WalkInService`. It is a **separate key from `JWT_SECRET` on purpose** — ticket signing and session
signing are distinct security domains, so reusing one key would mean a leak of either compromises
both, and rotating `JWT_SECRET` would silently invalidate every outstanding ticket. Outside
Production it still falls back to `JWT_SECRET` so a contributor who set only that can run locally.

> This section previously stated that `TICKET_HMAC_SECRET` "isn't implemented yet — nothing reads
> it, so it isn't validated". That was wrong on both counts and is exactly the sort of note that
> tells an operator it is safe to skip a live secret. Corrected 2026-08-01 (D-216).

## Generating a real production secret

```bash
openssl rand -base64 48
```

Where the result goes depends on `SECRETS_PROVIDER`:

- `configuration` — your platform's env-var secret store (ECS task secrets, Kubernetes secrets,
  the deployment platform's own vault). Never a committed file.
- `aws` — AWS Secrets Manager, under `${AWS_SECRETS_PREFIX}JWT_SECRET`. The task role needs
  `secretsmanager:GetSecretValue` and nothing more; the app can read secrets and cannot create,
  delete or rotate them (`ISecretsManagerClient` is narrowed to one method for exactly this reason).
  With `SIGNING_KEY_PROTECTION=kms`, IAM also needs `kms:GenerateDataKey`, `kms:Decrypt` and
  `kms:DescribeKey` on the CMK — nothing more.

## Local Docker Compose

`docker-compose.yml` auto-loads a root `.env` if one exists and falls back to the same dev
placeholders used elsewhere. It reads `POSTGRES_PASSWORD`, `JWT_SECRET`, `TICKET_HMAC_SECRET`,
`OTP_PEPPER`, `ALLOWED_ORIGINS`, `KURX_API_BASE`, `SUPERADMIN_BOOTSTRAP_PHONE`, `FILE_SCANNER` and
`CLAMAV_*`. (The `N8N_*` credentials left this list when the unused n8n service was deleted — D-337.)
This means:

- Zero-config `docker compose up` still works exactly as before.
- Copying this compose file toward a real deployment only requires creating a root
  `.env` with real values — no compose file edits needed.

Note that compose does **not** set `SECRETS_PROVIDER`, so a compose stack always runs the
`configuration` provider. Exercising the AWS path requires setting it explicitly.

## Extending this pattern

When a new secret-backed provider (Razorpay, SES, FCM, S3) is wired up:

1. Read it through **`ISecretProvider`** (`GetRequiredAsync` for anything the platform needs,
   `GetAsync` only for genuinely optional values). Do **not** read `IConfiguration` — that bypasses
   Secrets Manager for that one secret and the bypass is invisible until an AWS deployment.
2. If the API cannot function safely with a placeholder value in Production, call
   `SecretValidation.RequireStrongProductionSecret(name, value)` from that provider's
   registration path, gated on `IHostEnvironment.IsProduction()`. If its absence should stop the
   boot outright, add it to `RequiredSecrets.Names` instead — that gets both checks.
3. If it is a *known* committed placeholder, add the literal to `SecretValidation.KnownDevValues`
   so Production names the actual mistake ("still set to its committed development placeholder")
   rather than sending an operator off to lengthen the dev value.
4. Document the new variable in `.env.example` with a `PRODUCTION REQUIREMENT` comment
   like the ones above.
