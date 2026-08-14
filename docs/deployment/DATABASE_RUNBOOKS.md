# Database runbooks (DB-9)

What to do when a database alarm fires. One section per alarm; each names the metric that fired, what it
means, and the first three things to look at.

> ⚠️ **Deployment status — read before trusting any of this.** The Terraform in `infra/terraform/` has
> **never been applied**. There is no state file, no `.tfvars`, and `.github/workflows/cd.yml` builds images
> to GHCR without touching AWS. Every alarm referenced here is **CONFIGURED ONLY**. None has ever fired, and
> none has been runtime-verified. The commands below are correct for the infrastructure this configuration
> *declares*; they are not evidence it exists. See [§ Deployment checklist](#deployment-checklist).

Resource names assume `local.name` = `kurx-<environment>` (`infra/terraform/network.tf`). Substitute your
environment throughout: `export ENV=staging; export DB=kurx-$ENV`.

## The three kinds of signal

Knowing which kind fired is the first triage step, and getting it wrong wastes the first ten minutes.

| Namespace | What it measures | A red light means |
|---|---|---|
| `AWS/RDS` | The database engine, published by AWS | The database itself is stressed |
| `Kurx.Database` | Engine facts AWS does **not** publish for RDS PostgreSQL — deadlocks, bloat. Sampled from `pg_stat_*` every 5 min by `DatabaseHealthProbeJob` | The database is healthy but *something using it* is behaving badly |
| `Kurx.Reconciliation` | Financial and inventory correctness | **The database and application may both be perfectly healthy and the data still wrong.** This is the D-240 case |

---

## DB connection exhaustion

**Alarms:** `kurx-<env>-database-connections` (CRITICAL, `AWS/RDS` `DatabaseConnections` > 340 for 3 min) ·
`kurx-<env>-database-pool-failures` (HIGH, `kurx.db.failures` > 10 in 5 min)

**Why 340:** the application cannot demand more than ~280 by configuration (10 tasks × (20 API + 8 jobs) —
[`CONNECTION_POOLING.md`](CONNECTION_POOLING.md), DB-2) against ~450 available on `db.t4g.medium`. Breaching
340 means connections are held by something outside that budget. **Raise
`var.db_connection_alarm_threshold` when resizing the instance class** — it is derived from instance memory.

1. **Read the `reason` dimension on `kurx.db.failures` before anything else.** It decides the fix, and the
   two common values have *opposite* remedies:
   - `pool_exhausted` — **our** per-task ceiling. Raise `Database__MaxPoolSize`, or find what holds
     connections. The database is fine.
   - `server_connection_limit` — **PostgreSQL** refused (SQLSTATE 53300). Every replica together exceeds the
     server. **Raising our ceiling makes this worse.** Lower it, or reduce task count.
   - `command_timeout` — a slow query, not a capacity problem. Go to [slow queries](#slow-queries).

2. Who is actually connected:
   ```sql
   SELECT application_name, state, count(*)
     FROM pg_stat_activity GROUP BY 1, 2 ORDER BY 3 DESC;
   ```
   `application_name` is load-bearing here — DB-2 stamps `kurx-api` and `kurx-jobs` deliberately so this
   query answers "API or background jobs?" directly instead of by inference.

3. Long-running and idle-in-transaction sessions, which are the usual holders:
   ```sql
   SELECT pid, application_name, state, now() - xact_start AS xact_age, left(query, 120)
     FROM pg_stat_activity
    WHERE state <> 'idle' OR xact_start < now() - interval '5 minutes'
    ORDER BY xact_age DESC NULLS LAST LIMIT 20;
   ```

4. Did scaling cause it?
   ```bash
   aws ecs describe-services --cluster kurx-$ENV --services kurx-$ENV-api \
     --query 'services[0].{desired:desiredCount,running:runningCount}'
   ```
   Task count × 28 is the application's connection budget. If that exceeds the ceiling, the pool sizing is
   wrong for the current scale — not the database.

5. If mitigation is needed before root cause: reduce `desiredCount`. **Do not raise `MaxPoolSize` as a
   reflex** — see step 1.

6. Root cause. A leak shows as connections that never return to `idle`; a stuck migration shows as one
   `kurx-api` session holding a transaction open.

---

## Deadlock

**Alarm:** `kurx-<env>-database-deadlocks` (HIGH, `Kurx.Database` `kurx.db.deadlocks` > 5 in 15 min)

**Why not zero:** PostgreSQL resolves a deadlock by aborting one transaction and the caller retries.
Occasional deadlocks under contention are normal; paging on one would train everyone to ignore this alarm.

1. The deadlock detail is in the **PostgreSQL log export**, not in the metric. The metric says how many; the
   log says which statements:
   ```bash
   aws logs filter-log-events --log-group-name /aws/rds/instance/$DB/postgresql \
     --filter-pattern deadlock --start-time $(( ($(date +%s) - 3600) * 1000 ))
   ```
   PostgreSQL logs both statements and the process ids involved.

2. Lock waits are the leading indicator, logged because `log_lock_waits` is on:
   ```bash
   aws logs filter-log-events --log-group-name /aws/rds/instance/$DB/postgresql \
     --filter-pattern '"still waiting for"' --start-time $(( ($(date +%s) - 3600) * 1000 ))
   ```

3. Anything blocked right now:
   ```sql
   SELECT pid, pg_blocking_pids(pid) AS blocked_by, left(query, 120)
     FROM pg_stat_activity WHERE cardinality(pg_blocking_pids(pid)) > 0;
   ```

4. Correlate with application traces. The log line carries a timestamp; the OTLP trace for the same window
   names the endpoint. `kurx.correlation_id` on the span is the same id in the structured logs and in the
   `ProblemDetails` response the user saw.

5. Determine whether a code path takes locks in an order another path takes in reverse. **Kurx already fixes
   this by convention — inventory pools are locked in ascending pool-id order (V3 §17.1).** A deadlock
   involving `inventory_pools` most likely means a new path that does not follow it.

---

## Backup failure

**Alarm:** `kurx-<env>-database-events` — an **RDS event subscription** (categories `backup`, `failure`,
`availability`, `maintenance`) → the critical SNS topic. Not a metric alarm: AWS publishes no
"backup succeeded" metric whose absence could be alarmed on.

1. Current backup configuration and the last restorable moment:
   ```bash
   aws rds describe-db-instances --db-instance-identifier $DB \
     --query 'DBInstances[0].{status:DBInstanceStatus,retention:BackupRetentionPeriod,
                              window:PreferredBackupWindow,latest:LatestRestorableTime}'
   ```
   `LatestRestorableTime` **is** the recovery point. It should be within ~5 minutes of now.

2. Recent automated snapshots:
   ```bash
   aws rds describe-db-snapshots --db-instance-identifier $DB --snapshot-type automated \
     --query 'reverse(sort_by(DBSnapshots,&SnapshotCreateTime))[:5].{id:DBSnapshotIdentifier,
              created:SnapshotCreateTime,status:Status}'
   ```

3. The events themselves:
   ```bash
   aws rds describe-events --source-identifier $DB --source-type db-instance --duration 1440
   ```

4. **Assess RPO impact.** Retention is 30 days in production, 7 elsewhere (`data_stores.tf`). If
   `LatestRestorableTime` is outside policy, that is the incident — not the failed job.

5. Escalate if the recovery point is outside policy. A database whose backups are failing is one incident
   away from data loss, and it does not resolve itself.

---

## Table bloat

**Alarms:** `kurx-<env>-database-bloat-{inventory_pools,ticket_types,organization_wallet}` (MEDIUM —
investigate in hours, do not page. `kurx.db.dead_tuple_ratio` > 20% for 30 min)

**This is not a size alarm.** A large table that is entirely live data is healthy. Bloat is dead storage
relative to useful storage.

1. **Read `kurx.db.hours_since_vacuum` for the same table first.** It splits the diagnosis in two:
   - Recent vacuum + high ratio → the table churns faster than autovacuum's threshold. A **tuning** question.
   - No recent vacuum + high ratio → autovacuum is not running on it. A **different** problem.

2. Ground truth:
   ```sql
   SELECT relname, n_live_tup, n_dead_tup,
          round(n_dead_tup * 100.0 / NULLIF(n_live_tup, 0), 2) AS dead_pct,
          last_vacuum, last_autovacuum, autovacuum_count,
          pg_size_pretty(pg_total_relation_size(relid)) AS total
     FROM pg_stat_user_tables
    WHERE relname IN ('inventory_pools', 'ticket_types', 'organization_wallet')
    ORDER BY dead_pct DESC NULLS LAST;
   ```

3. Is autovacuum blocked? A long-lived transaction holds the xmin horizon and stops it reclaiming anything
   newer, platform-wide:
   ```sql
   SELECT pid, application_name, state, now() - xact_start AS age
     FROM pg_stat_activity WHERE xact_start IS NOT NULL ORDER BY age DESC LIMIT 5;
   ```
   This is the most common cause of "autovacuum runs but reclaims nothing", and the fix is ending that
   transaction, not touching autovacuum.

4. **DB-9 does not tune autovacuum, by design.** Per-table `autovacuum_vacuum_scale_factor` on a hot table
   is a reasonable eventual change; it needs the measurements this phase produces first, and its own
   decision record.

---

## Reconciliation drift

**Alarms:** `kurx-<env>-reconciliation-drift-wallet` (**CRITICAL**) ·
`-inventory` / `-registration` (HIGH). Threshold zero — the metric only exists when `outcome=drift`.

**Why this is the most important alarm here:** nothing fails a request while a wallet disagrees with its
ledger. Every dashboard stays green. That is exactly how D-240 could have run in production unnoticed — six
concurrent payments credited one, ₹500 of ₹600 missing, and no error anywhere.

1. Identify the entity. The structured log line carries it, at `Error`, from the job:
   ```bash
   aws logs filter-log-events --log-group-name /ecs/kurx-$ENV-api \
     --filter-pattern '"Wallet drift detected"' --start-time $(( ($(date +%s) - 86400) * 1000 ))
   ```
   It names each `org`, its `cached`, its `ledger` and the `delta` in paise. Organization ids and amounts
   only — no bank details, no payment references.

2. Inspect state. **The ledger is authoritative (D-103); the wallet is a cache of it.**
   ```sql
   SELECT state, sum("AmountPaise") FROM ledger_entries WHERE "OrgId" = '<org>' GROUP BY 1;
   SELECT * FROM organization_wallet WHERE "OrgId" = '<org>';
   ```

3. Find what wrote the divergence — payment captures, refunds and the settlement job are the three writers:
   ```sql
   SELECT "RefType", "State", count(*), sum("AmountPaise")
     FROM ledger_entries WHERE "OrgId" = '<org>' AND "CreatedAt" > now() - interval '7 days'
    GROUP BY 1, 2 ORDER BY 3 DESC;
   ```

4. **DO NOT manually edit balances or ledger rows.** Not with SQL, and not "just to make the alarm stop".
   The ledger is append-only and is the evidence for what happened; a hand-edited balance destroys the only
   record that can explain it.

5. Repair is `IWalletService.RepairAsync` — **deliberately human-triggered**. The wallet job does not
   self-heal, unlike inventory: a pool can be reset from its admissions with no consequence beyond seat
   counts, a wallet balance is money. Run it only after step 3 explains the delta.

6. Record the incident, including the delta and the explanation. A repair with no root cause is a recurrence.

---

## Reconciliation job failure

**Alarm:** `kurx-<env>-reconciliation-failure-{wallet,inventory,registration}` (HIGH, > 1 in 15 min)

**This is not "no drift". It is "we do not know."** An unverified invariant looks identical to a healthy one
on any dashboard that only watches for drift, and stays that way until someone fixes the job.

**Threshold is > 1, not > 0:** Hangfire retries three times, and a single transient failure it clears is not
worth waking anyone.

1. The exception:
   ```bash
   aws logs filter-log-events --log-group-name /ecs/kurx-$ENV-api \
     --filter-pattern '"reconciliation failed to complete"' \
     --start-time $(( ($(date +%s) - 3600) * 1000 ))
   ```
2. Check the Hangfire dashboard's failed-jobs list for the retry history.
3. A timeout points at [connection exhaustion](#db-connection-exhaustion) or a slow query, not at the
   reconciliation logic.
4. **Until this is fixed, treat the invariant as unverified.** Do not close the incident on "no drift alarm
   fired" — the detector that would raise it is the thing that is broken.

---

## Slow queries

Not an alarm — an investigation path. Three sources, and they answer different questions.

| Source | Answers | Where |
|---|---|---|
| `log_min_duration_statement = 1000` | *Which individual statements* took over a second | CloudWatch Logs `/aws/rds/instance/$DB/postgresql` |
| `pg_stat_statements` | *Which query shape* costs the most in aggregate — including the fast query run ten million times, which slow-query logs can never show | The database |
| OTLP/Npgsql spans | *Which request* the query belongs to | Traces, joined by `kurx.correlation_id` |

**1000ms is deliberate and was reviewed in DB-9.** Lowering it to 100ms would write a log line per ordinary
indexed read during an on-sale, burying the genuinely slow statements and costing ingest. Use
`pg_stat_statements` for the sub-second distribution — it aggregates rather than logs.

```sql
SELECT calls, round(mean_exec_time::numeric, 2) AS mean_ms,
       round(total_exec_time::numeric) AS total_ms, rows,
       shared_blks_read, shared_blks_hit, left(query, 120)
  FROM pg_stat_statements ORDER BY total_exec_time DESC LIMIT 20;
```

Sort by `total_exec_time`, not `mean_exec_time`: the query worth fixing is usually a fast one called
constantly, not the slow one called twice a day.

---

## Deployment checklist

Nothing below has been done. Each item moves a capability from **CONFIGURED ONLY** to **DEPLOYED**, and the
verification step is what moves it to **RUNTIME VERIFIED**. Do not report a capability as working before its
verification line has actually been run.

- [ ] `terraform apply` for the environment. Until this happens, every alarm in this document does not exist.
- [ ] **Reboot the RDS instance.** `shared_preload_libraries` and `pg_stat_statements.max` are static
      parameters — they take effect at postmaster start, *not* on apply. `terraform apply` alone leaves
      `pg_stat_statements` unavailable while the parameter group claims otherwise.
- [ ] Create the extension. Terraform cannot: it manages the parameter group, not objects inside the
      database, and the AWS provider has no `CREATE EXTENSION`. This is the same post-apply pattern as
      seeding secret values (`infra/terraform/README.md`):
      ```sql
      CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
      ```
      Verify: `SELECT count(*) FROM pg_stat_statements;` returns a row count, not an error.
- [ ] Subscribe a real destination to **both** SNS topics — `kurx-<env>-alerts` and
      `kurx-<env>-alerts-critical`. Subscriptions are intentionally not in Terraform (who is on call is not a
      repository decision). **Until this is done every alarm fires into a topic with no subscribers, which
      is indistinguishable from no alarm at all.**
- [ ] Confirm the OTLP pipeline actually exports the two new meters. `Kurx.Database` and
      `Kurx.Reconciliation` are registered in `TelemetryRegistration`, but they only reach CloudWatch when
      `OTEL_EXPORTER_OTLP_ENDPOINT` is set and the ADOT sidecar is running. Verify by finding the namespaces
      in the CloudWatch console — **not** by reading the C#.
- [ ] Verify `DatabaseHealthProbeJob` is running: Hangfire dashboard shows `database-health-probe` with a
      recent successful execution, and `kurx.db.connections.used` has datapoints.
- [ ] Test the alarm path end to end on **staging**, once, per alarm. An alarm nobody has ever seen fire is
      an assumption. `aws cloudwatch set-alarm-state --alarm-name <name> --state-value ALARM
      --state-reason "runbook test"` proves the notification path; it does not prove the metric or threshold.
- [ ] Re-verify the connection threshold if the instance class ever changes.

## What is deliberately not monitored

**Replication lag.** Kurx has no read replica — `aws_db_instance.main` is the only database resource and
nothing sets `replicate_source_db`. A `ReplicaLag` alarm would reference a dimension with no data, sit
permanently in `INSUFFICIENT_DATA`, and appear on a dashboard as a monitored capability. A false claim of
coverage is worse than an acknowledged gap. The design when a replica is added is recorded at the foot of
`infra/terraform/monitoring_database.tf`; thresholds must come from whatever actually reads from it, which
today is nothing.
