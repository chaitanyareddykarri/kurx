# Observability

How the running system reports its own state. Complements `.claude/memory/logging.md` (what happened) with health, metrics, and tracing (is it healthy, how much, where's the time going).

## Health checks (built, D-017)

`/health` reflects **real dependency probes**, never a static 200:

- **Postgres** — always probed.
- **Redis** — probed only when `REDIS_CONNECTION` is configured.
- **Local-disk storage** — a writability probe, only when `STORAGE_PROVIDER=localdisk`.
- Deliberately **not** probed: email/SMS — only console/mock dev senders exist, so there is no real external dependency to check yet. Add a probe when (and only when) a real provider lands (Phase 8).

Rule: a health check must fail when the thing it names is actually down. Don't add a check that always returns healthy — that's worse than no check. When a real provider is added, add its probe in the same change.

## Correlation & tracing

- `X-Correlation-Id` threads through logs, `ProblemDetails` responses and spans (`logging.md`). One id joins all three.
- **OpenTelemetry IS wired** (D-100, `Kurx.Api/Observability/TelemetryRegistration.cs`): traces (ASP.NET Core + HttpClient + **Npgsql**, so a slow request resolves to the slow query) and metrics, over one shared resource. Serilog also enriches with the W3C trace/span id.
- **Exporter selection is explicit and never guessed.** With `OTEL_EXPORTER_OTLP_ENDPOINT` set, telemetry goes there (ADOT collector → CloudWatch/X-Ray in production); without it, **nothing is exported**. There is deliberately no default endpoint — a silently mis-targeted exporter is indistinguishable from a working one until an incident. So "the code emits it" never means "CloudWatch has it": check the namespace in the console.

## Metrics

Three meters, and **the separation is load-bearing** — the exporter maps a meter to a CloudWatch namespace, so folding one into another silently moves every metric out from under its alarm.

| Meter | Kind | Carries |
|---|---|---|
| `Kurx.Auth` | application + security | `kurx.auth.attempts` (tag `outcome`), `kurx.auth.security_events`, `kurx.auth.operation.duration`, `kurx.db.failures` (tag `reason`, DB-2) |
| `Kurx.Database` | **database engine** (DB-9) | `kurx.db.deadlocks`, `kurx.db.connections.{used,max,utilization}`, `kurx.db.dead_tuple_ratio`, `kurx.db.hours_since_vacuum`, `kurx.db.table_bytes` |
| `Kurx.Reconciliation` | **business correctness** (DB-9) | `kurx.reconciliation.runs` (tags `type`, `outcome`), `kurx.reconciliation.drifting_entities` |

Plus the `Npgsql` meter (DB-2) for pool gauges — *this process's* pool, never the server's.

Rules that cost real incidents to learn:

- **The application reads PostgreSQL catalogs only for what AWS does not publish.** RDS gives `DatabaseConnections`, `CPUUtilization`, `FreeStorageSpace` — do not duplicate them. It does **not** publish deadlocks (that CloudWatch metric is *Aurora*-only) or bloat (not an AWS concept), so `DatabaseHealthProbeJob` samples `pg_stat_database`/`pg_stat_user_tables` every 5 min. Never invent an AWS metric name; check whether it exists for *RDS* PostgreSQL specifically.
- **A cumulative catalog counter must be exported as a delta.** `pg_stat_database.deadlocks` only rises, so exporting it raw makes every alarm ask "has this database *ever* deadlocked".
- **Anything sampling a shared counter belongs in a Hangfire recurring job**, not a hosted service — `DisableConcurrentExecution` is what stops N replicas multiplying one number by N.
- **Business correctness is not health.** The database and application can both be perfectly well while a wallet disagrees with its ledger — that is D-240, and it failed no request. A reconciliation metric's three outcomes are not interchangeable: `clean` must never page, `drift` pages, and `failed` pages *differently* because an unchecked invariant is **unverified, not held**. Recording a failure as zero drift turns a broken detector into a green dashboard.
- **Never put PII, secrets or financial detail in a tag or span attribute** — telemetry leaves the platform and is readable by anyone with dashboard access. Ids and counts only; amounts stay in the structured log. Tags must also stay low-cardinality (a per-table gauge is capped at 15 tables for exactly this reason).
- Don't stub a metric speculatively (`coding-standards` — no future-proofing). Add it with the alarm that reads it.

## Alarms and runbooks

CloudWatch alarms live in `infra/terraform/monitoring.tf` (application/security) and `monitoring_database.tf` (database + reconciliation). **This Terraform has never been applied** — treat every alarm as CONFIGURED ONLY until the checklist in `docs/deployment/DATABASE_RUNBOOKS.md` has been worked through. Distinguish **configured / deployed / runtime-verified** in every report; "the Terraform validates" is not "the alarm works".

Every alarm must answer: what failed, why care, how severe, what to do. An alarm that fires routinely is worse than none — a threshold of zero is right only when the metric is emitted *solely* on the bad outcome (e.g. reconciliation `drift`), and wrong everywhere else. Each critical/high alarm gets a runbook section in `DATABASE_RUNBOOKS.md`.

## SignalR

`ScanHub`/`SalesHub` connections authenticate and re-verify membership on every group join (D-017, `api-conventions`). Connection/join failures should be observable via logs with the enriched `UserId`/`OrgId` context — a socket silently failing a membership check is a security signal worth a `Warning`.

## When adding infra

Anything that changes what "healthy" means (a new required dependency, a new external provider) must update `/health` **and** `docs/deployment/` in the same change. A new dependency with no health probe is an incomplete change — see `.claude/checklists/release.md`.
