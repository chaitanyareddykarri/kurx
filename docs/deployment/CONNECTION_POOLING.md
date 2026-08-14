# Connection pooling & timeouts (DB-2)

How many database connections Kurx may demand, why those numbers, and which claims here are measured
versus still open. Configuration lives in `backend/Kurx.Infrastructure/Configuration/DatabaseOptions.cs`;
defaults are in `backend/Kurx.Api/appsettings.json` and overridable per environment via `Database__*`
environment variables with no source change.

---

## 1. The problem this solves

Before DB-2, `AddKurxInfrastructure` passed the raw connection string to `UseNpgsql` with no pool or
timeout settings, so every value was an Npgsql library default — most consequentially
**`MaxPoolSize = 100` per process**.

That number is not wrong in isolation. It is wrong against the deployment this repository declares:

| Input | Value | Source |
|---|---|---|
| API autoscaling ceiling | `api_desired_count × 5` = **10 tasks** | `infra/terraform/compute.tf:227`, `variables.tf:59` |
| Database instance | `db.t4g.medium` (4 GiB) | `infra/terraform/variables.tf:48` |
| RDS `max_connections` | `LEAST(DBInstanceClassMemory/9531392, 5000)` ≈ **439** | RDS default formula for the class |
| Demand at the old default | 10 × 100 = **1,000** | |

Exhaustion would have begun at **five tasks** — halfway through the configured autoscaling range. The
failure mode is the ugly one: it surfaces under exactly the load that triggered the scale-out, so
autoscaling causes the outage it exists to prevent, and replacement tasks fail their readiness probe and
are replaced again.

## 2. The configured ceilings

**API traffic and Hangfire hold separate pools.** Npgsql pools per distinct connection string, so the
differing `Application Name` (`kurx-api` / `kurx-jobs`) is the *mechanism*, not a label. Two consequences,
both wanted: a job holding connections cannot starve request traffic, and `pg_stat_activity.application_name`
answers "API or jobs?" directly instead of by inference.

| Setting | Production default | Why |
|---|---|---|
| `Database__MaxPoolSize` | **20** | API request pool, per task |
| `Database__JobsMaxPoolSize` | **8** | Hangfire pool, per task |
| `Database__JobWorkerCount` | **5** | Explicit. Hangfire's default is `min(vCPU × 5, 20)` = 10 on a 2-vCPU task, contending for 8 connections plus its own polling and heartbeat — a self-inflicted pool timeout during normal operation |
| `Database__MinPoolSize` | **2** | Warm, so the first request after idle does not pay TCP + TLS + auth. Small, because idle connections held per task are what starve a server when replica count is what varies |
| `Database__CommandTimeoutSeconds` | **30** | A query running longer will not succeed usefully and is holding a connection others need |
| `Database__ConnectionTimeoutSeconds` | **15** | The value that converts pool exhaustion from an indefinite hang into a clean, observable failure |
| `Database__ConnectionIdleLifetimeSeconds` | **300** | So a spike does not pin the pool at its ceiling long after the spike |

### Sizing arithmetic

```
Per task:            20 (api) +  8 (jobs)  =  28
At the ceiling:      28 × 10 tasks         = 280
RDS capacity:                              ≈ 439  (db.t4g.medium)
Reserved (superuser, rds_superuser)        ≈  10
Available                                  ≈ 429
Headroom                                   ≈ 149  (35%)
```

That headroom deliberately absorbs a migration task, an operator's `psql`, monitoring agents, and a
deploy overlapping old and new tasks. It is **not** spare capacity to be spent by raising the ceiling.

**Why not simply raise RDS `max_connections`?** Because it moves the failure rather than removing it.
Postgres allocates per-backend memory (`work_mem` and friends) per connection; a `db.t4g.medium` serving
1,000 connections would exhaust memory before it exhausted the connection counter. The application is
responsible for demanding a sane number.

### Development and test

| Environment | api / jobs / workers | Why it differs |
|---|---|---|
| **Production** (`appsettings.json`) | 20 / 8 / 5 | The arithmetic above |
| **docker-compose** | 12 / 6 / 4 | The postgres container ships `max_connections = 100`, and a dev machine commonly also runs a test suite, a `psql`, or a second stack against the same server |
| **Test suite** (`KurxApiFactory`) | 8 / 4 / 2, `MinPoolSize = 0` | The suite has the **opposite shape** to production: every test class owns its own database, and Npgsql pools per connection string, so the suite holds one pool **per class**, not per process. `MinPoolSize = 0` is load-bearing — a warm minimum would have every pool ever created hold connections open whether or not its class is still running |

## 3. Timeout policy

Failures are classified, not lumped together — three of the four look identical in a log and have
different, in one case **opposite**, remedies. Classification lives in
`Kurx.Api/ExceptionHandling/DatabaseFailureClassifier.cs`.

| Kind | Detected by | Client sees | Telemetry `reason` | Operator remedy |
|---|---|---|---|---|
| Pool exhausted | Npgsql message fragment | `503 database_busy` | `pool_exhausted` | **Raise** `Database__MaxPoolSize`, or find what holds connections |
| Server connection limit | SQLSTATE `53300` | `503 database_busy` | `server_connection_limit` | **Lower** the per-task ceiling, or add a pooler. Raising it makes this worse |
| Command timeout | SQLSTATE `57014`, or `TimeoutException` under `NpgsqlException` | `503 database_timeout` | `command_timeout` | Find the slow query |
| Lock timeout | SQLSTATE `55P03` | `503 resource_busy` | `lock_timeout` | Contention; usually transient |
| Unavailable | `NpgsqlException.IsTransient`, `57P01/02/03` | `503 database_unavailable` | `unavailable` | Server reachability |

Pool and server exhaustion share **one client-facing code** on purpose: the caller's action is identical
(retry), and telling an anonymous caller where saturation begins hands them the shape of a cheap denial of
service. The operator distinction lives in the telemetry tag.

**503 + `Retry-After: 1`, and nothing is retried server-side.** These failures happen either before a
transaction opens (pool exhaustion, connection refused) or inside one that then rolls back (command/lock
timeout), so no partial write survives and a client retry is safe. An automatic retry *inside* the server
would be a second attempt at a payment capture or a withdrawal whose first attempt's fate is unknown —
which is also why `EnableRetryOnFailure` remains **off** (it additionally requires every explicit
`BeginTransactionAsync` site to run inside an `IExecutionStrategy`; that is DB-4).

**Withdrawal lock timeout.** `WalletService.InitiateWithdrawalAsync` holds the only `SELECT … FOR UPDATE`
on a request path. It now runs `SET LOCAL lock_timeout = '5s'` first. Without it that lock waits forever,
so one stuck transaction blocks every subsequent withdrawal for that organisation while each blocked caller
pins a pooled connection — a slow drain of the pool rather than a visible error. Nothing is written before
the lock is taken, so the resulting `resource_busy` is safe to retry and cannot double-withdraw.

## 4. Observability

Extends the existing OpenTelemetry pipeline (`Api/Observability/TelemetryRegistration.cs`); no second
telemetry framework was introduced.

- **`AddMeter("Npgsql")`** — Npgsql's built-in pool instrumentation: connection usage (used vs idle),
  **pending requests waiting for a connection**, pool timeouts, command duration. Pending requests is the
  leading indicator: it rises before anything fails, which is the difference between resizing the pool on a
  graph and resizing it during an incident.
- **`kurx.db.failures{reason}`** — the classification above. Npgsql reports *that* a failure occurred; it
  cannot report whether the ceiling hit was ours or PostgreSQL's, and those have opposite remedies.

**Recommended alarms** (not yet in `infra/terraform/monitoring.tf` — see §6):
`DatabaseConnections` approaching `max_connections`; any `kurx.db.failures{reason="server_connection_limit"}`;
sustained `kurx.db.failures{reason="pool_exhausted"}`; RDS `Deadlocks`.

## 5. Validation — what is proven and what is not

> Per the phase's own rule: **never claim a production load result that was not measured.**

### ✅ PROVEN LOCALLY (`Kurx.Tests/DatabasePoolTests.cs`, real Postgres 17)

- Configured ceilings actually reach the connection — and the first implementation **did not**, because
  `NpgsqlConnectionStringBuilder.ContainsKey` returns true for every keyword it knows, so the
  "don't override the operator" guard suppressed all defaults. The test caught it; detection now uses a
  plain `DbConnectionStringBuilder`, which keeps only supplied keys.
- API and jobs resolve to genuinely different connection strings → separate pools.
- An operator-supplied value is never overridden; unset ones still default.
- Nonsense configuration (`MaxPoolSize=0`, non-numeric, `MinPoolSize > MaxPoolSize`,
  idle lifetime below the pruning interval) fails at **startup** with a message naming the setting.
- **Pool exhaustion fails cleanly, bounded by `ConnectionTimeoutSeconds`, rather than hanging.**
- A command exceeding `CommandTimeoutSeconds` is cancelled and classified as `command_timeout`.
- SQLSTATE `53300` is never reported as our pool being exhausted.
- An ordinary `23505` constraint violation is *not* dressed up as an infrastructure failure.
- No connection leak across 60 sequential scope create/dispose cycles.

### 🔶 STRUCTURALLY VERIFIED (arithmetic, not measurement)

The 280-vs-429 figure is derived from `infra/terraform` inputs and the documented RDS formula. It is sound
arithmetic and **not** an observed peak.

### ⛔ REQUIRES STAGING VALIDATION

No AWS environment exists — `infra/terraform/README.md` states the configuration has **never been applied**.
The following are therefore open, and `MaxPoolSize = 20` is a **reasoned starting value, not a measured
optimum**:

| Metric | Status |
|---|---|
| Peak concurrent connections under real traffic | ⛔ not measured |
| p95 / p99 in-flight query concurrency per task | ⛔ not measured |
| Pool wait time under sustained load | ⛔ not measured |
| Request latency / error rate at the ceiling | ⛔ not measured |
| DB CPU and memory under load | ⛔ not measured |
| Whether background jobs starve API traffic in practice | ⛔ not measured (isolated by design; unproven under load) |
| Actual `max_connections` on the provisioned instance | ⛔ formula-derived only |

**Procedure once staging exists**

1. Apply Terraform to staging; confirm actual `max_connections` (`SHOW max_connections`).
2. Load-test to ~70% of the autoscaling ceiling. Record, per task: `SELECT count(*) FROM pg_stat_activity
   WHERE application_name = 'kurx-api'`, the Npgsql pending-requests metric, p95/p99 latency, error rate.
3. Read the **pending-requests** gauge, not just the connection count. Sustained non-zero pending with
   spare server capacity means `MaxPoolSize` is too low; near-zero pending with connections idling means it
   is too high.
4. Set `MaxPoolSize` to observed p99 in-flight concurrency **× 1.5**, then re-derive the total and confirm
   ≥ 30% server headroom remains.
5. Record the measured values in this file, replacing the ⛔ rows.

## 6. Known gaps (deliberately out of DB-2 scope)

- No CloudWatch alarms for connections / deadlocks / backup failure — audit finding **O-02**.
- `pg_stat_statements` not enabled (`shared_preload_libraries` unset in the RDS parameter group).
- No RDS Proxy or PgBouncer. Not needed at a 10-task ceiling with these numbers; revisit if the ceiling
  rises materially.
- `EnableRetryOnFailure` off pending the `IExecutionStrategy` work — **DB-4**.
