# ── DB-9: database observability ──────────────────────────────────────────────
#
# Split from monitoring.tf because these read three different KINDS of signal and confusing them is how an
# operator wastes the first ten minutes of an incident:
#
#   AWS/RDS            — the database ENGINE, published by AWS.
#   Kurx.Database      — the engine facts AWS does NOT publish for RDS PostgreSQL (deadlocks, bloat),
#                        sampled from pg_stat_* by DatabaseHealthProbeJob and exported over the existing
#                        OTLP -> ADOT -> CloudWatch pipeline. No second monitoring stack.
#   Kurx.Reconciliation — BUSINESS CORRECTNESS. Says nothing about database or application health: both can
#                        be entirely well while a wallet disagrees with its ledger, which is precisely the
#                        D-240 defect that ran undetected.
#
# ⚠️ NONE OF THIS IS DEPLOYED. This Terraform has never been applied — there is no state file, no tfvars,
# and CD (.github/workflows/cd.yml) builds images to GHCR without touching AWS. Every resource here is
# CONFIGURED ONLY until an apply happens. See docs/deployment/DATABASE_RUNBOOKS.md for the deployment
# checklist that turns each item from configured into verified.

# Two topics, because "alert" and "page" are different acts. The existing alerts topic keeps its meaning;
# criticals get their own so a subscriber can route them to a phone without also being woken by a bloat
# warning. Subscriptions are deliberately not declared here — who is on call is an operational decision, not
# a repository one, and putting an address in Terraform state is how it becomes stale and unnoticed.
locals {
  # Wallet is the only CRITICAL of the three, and the distinction is not cosmetic: an inventory or
  # registration projection disagreeing with its authority is wrong data that self-heals; a wallet
  # disagreeing with its append-only ledger is money, does not self-heal by design, and fails no request
  # while it is true.
  #
  # Held in locals rather than inline ternaries because a multi-line conditional inside a heredoc is valid
  # HCL that `terraform fmt` then de-indents the rest of the resource body around — correct, and unreadable.
  reconciliation_severity = {
    wallet       = "CRITICAL"
    inventory    = "HIGH"
    registration = "HIGH"
  }

  reconciliation_impact = {
    wallet       = "a cached balance disagreeing with the append-only ledger is a money-correctness failure. Nothing fails a request while this is true, which is exactly why it needs an alarm (D-240)."
    inventory    = "a pool's consumed count disagrees with its active admissions; seat availability is wrong in a way no request will report."
    registration = "the authoritative Order/Ticket record and its registration projection disagree; admissions are wrong in a way no request will report."
  }
}

resource "aws_sns_topic" "alerts_critical" {
  name              = "${local.name}-alerts-critical"
  kms_master_key_id = aws_kms_key.data.id

  tags = { Name = "${local.name}-alerts-critical", Severity = "critical" }
}

# ── Connections (AWS/RDS — real engine metric) ─────────────────────────────────

resource "aws_cloudwatch_metric_alarm" "database_connections" {
  alarm_name        = "${local.name}-database-connections"
  alarm_description = <<-EOT
    CRITICAL. Server-side connections exceeded ${var.db_connection_alarm_threshold} for three minutes.

    WHY IT MATTERS: past this point new connections start being refused, and a refused connection is a failed
    request, not a slow one. Autoscaling makes this worse rather than better — more tasks means more pools.

    NOT a load alarm. The application cannot demand this many by configuration (see
    var.db_connection_alarm_threshold), so something outside that budget is holding connections.

    ACTION: docs/deployment/DATABASE_RUNBOOKS.md#db-connection-exhaustion
  EOT

  namespace   = "AWS/RDS"
  metric_name = "DatabaseConnections"
  statistic   = "Maximum"

  # 60s x 3 rather than one longer period: a deploy briefly doubles pools while old tasks drain, and that is
  # both normal and short. Three consecutive minutes is past any rollover.
  period             = 60
  evaluation_periods = 3

  threshold           = var.db_connection_alarm_threshold
  comparison_operator = "GreaterThanThreshold"

  # missing data is NOT breaching: the database not reporting is what the RDS/ECS health alarms are for, and
  # having two alarms fire for one cause trains people to silence both.
  treat_missing_data = "notBreaching"

  dimensions    = { DBInstanceIdentifier = aws_db_instance.main.identifier }
  alarm_actions = [aws_sns_topic.alerts_critical.arn]
  ok_actions    = [aws_sns_topic.alerts_critical.arn]

  tags = { Severity = "critical" }
}

# The application's own view, which the RDS metric cannot give: WHICH ceiling was hit. DB-2's kurx.db.failures
# classifies that, and the two reasons below have opposite remedies — pool_exhausted means raise our ceiling,
# server_connection_limit means lower it. Without this distinction the natural reaction to both is "raise the
# pool size", which is correct for one and actively harmful for the other.
resource "aws_cloudwatch_metric_alarm" "database_pool_failures" {
  alarm_name        = "${local.name}-database-pool-failures"
  alarm_description = <<-EOT
    HIGH. Requests are failing to obtain a database connection.

    WHY IT MATTERS: these are user-visible failures already happening, not a leading indicator.

    ACTION: read the `reason` dimension FIRST — `pool_exhausted` is our per-task ceiling (raise
    Database__MaxPoolSize or find what holds connections); `server_connection_limit` is PostgreSQL refusing
    (53300) and raising ours makes it worse. docs/deployment/DATABASE_RUNBOOKS.md#db-connection-exhaustion
  EOT

  namespace   = "Kurx.Auth" # kurx.db.failures is emitted on the existing application meter (DB-2)
  metric_name = "kurx.db.failures"
  statistic   = "Sum"

  period             = 300
  evaluation_periods = 1
  threshold          = 10 # a handful during a failover is recoverable; a sustained rate is not

  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"

  alarm_actions = [aws_sns_topic.alerts.arn]

  tags = { Severity = "high" }
}

# ── Deadlocks (Kurx.Database — no AWS source exists for RDS PostgreSQL) ────────

resource "aws_cloudwatch_metric_alarm" "database_deadlocks" {
  alarm_name        = "${local.name}-database-deadlocks"
  alarm_description = <<-EOT
    HIGH. More than 5 deadlocks in 15 minutes.

    WHY IT MATTERS: PostgreSQL resolves a deadlock by aborting one transaction, so a rare one is survivable
    and often invisible. A sustained rate means two code paths are taking locks in opposite orders under real
    traffic — and on the money path an aborted transaction is a failed payment or a lost seat.

    THRESHOLD REASONING: deliberately NOT zero. Occasional deadlocks under contention are normal and paging
    on one would train operators to ignore this alarm, which is the failure mode that matters most here.

    ACTION: docs/deployment/DATABASE_RUNBOOKS.md#deadlock
  EOT

  namespace   = "Kurx.Database"
  metric_name = "kurx.db.deadlocks"
  statistic   = "Sum"

  # 900s so the 5-minute probe contributes three samples per period — a single unlucky probe cannot breach it.
  period             = 900
  evaluation_periods = 1
  threshold          = 5

  comparison_operator = "GreaterThanThreshold"

  # notBreaching, and this one matters: the metric is only emitted when a delta is non-zero, so a healthy
  # database publishes NOTHING. Any other setting would page continuously on a perfectly healthy system.
  treat_missing_data = "notBreaching"

  alarm_actions = [aws_sns_topic.alerts.arn]

  tags = { Severity = "high" }
}

# ── Bloat (Kurx.Database — not an AWS concept at all) ──────────────────────────

# One alarm per high-churn table. for_each rather than one aggregate alarm because CloudWatch cannot alarm
# across a dimension, and because the remedy is per-table anyway: knowing "something is bloated" is not
# actionable, knowing "inventory_pools is" is.
resource "aws_cloudwatch_metric_alarm" "database_bloat" {
  for_each = toset(["inventory_pools", "ticket_types", "organization_wallet"])

  alarm_name        = "${local.name}-database-bloat-${each.value}"
  alarm_description = <<-EOT
    MEDIUM — investigate in hours, do not page. Dead tuples on ${each.value} exceeded 20% of live rows for
    30 minutes.

    WHY IT MATTERS: every one of these tables is updated in place on a hot path (pool decrement, sold-count
    mirror, wallet balance), which is the access pattern that bloats. Dead tuples are scanned like live ones,
    so the read that guards oversell gets slower as the table fills with rows nobody can see.

    THIS IS NOT A SIZE ALARM. A large table that is entirely live is healthy. The measure is dead storage
    relative to useful storage, which is what autovacuum reclaims.

    ACTION: read kurx.db.hours_since_vacuum for the same table BEFORE anything else — a high ratio with a
    recent vacuum is a churn-rate tuning question, a high ratio with no recent vacuum means autovacuum is not
    running on it, and those have different fixes. docs/deployment/DATABASE_RUNBOOKS.md#table-bloat
  EOT

  namespace   = "Kurx.Database"
  metric_name = "kurx.db.dead_tuple_ratio"
  statistic   = "Average"

  # 6 x 300s = 30 minutes. Bloat is a slow phenomenon; a short window would fire during a bulk write that
  # autovacuum clears minutes later.
  period             = 300
  evaluation_periods = 6

  threshold           = 20
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"

  dimensions    = { table = each.value }
  alarm_actions = [aws_sns_topic.alerts.arn]

  tags = { Severity = "medium" }
}

# ── Reconciliation (Kurx.Reconciliation — business correctness) ────────────────

# DRIFT. The alarm this whole phase exists for: before DB-9 a wallet disagreeing with its ledger produced a
# log line and nothing else, and no one reads a log nothing points them at.
resource "aws_cloudwatch_metric_alarm" "reconciliation_drift" {
  for_each = toset(["wallet", "inventory", "registration"])

  alarm_name        = "${local.name}-reconciliation-drift-${each.value}"
  alarm_description = <<-EOT
    ${local.reconciliation_severity[each.value]}. The ${each.value} reconciliation found the invariant does
    not hold.

    WHY IT MATTERS: ${local.reconciliation_impact[each.value]}

    THRESHOLD REASONING: zero. The metric is only emitted when outcome=drift, so a clean run publishes
    nothing on this alarm's filter and any datapoint at all is a real finding.

    ACTION: DO NOT edit balances or ledger rows. docs/deployment/DATABASE_RUNBOOKS.md#reconciliation-drift
  EOT

  namespace   = "Kurx.Reconciliation"
  metric_name = "kurx.reconciliation.runs"
  statistic   = "Sum"

  period             = 300
  evaluation_periods = 1
  threshold          = 0

  comparison_operator = "GreaterThanThreshold"

  # The load-bearing line in this file. `outcome` pins the alarm to drift only. Without it the alarm would
  # read every run including the clean ones — which are the overwhelming majority — and page every period on
  # a perfectly healthy platform.
  dimensions = {
    type    = each.value
    outcome = "drift"
  }

  treat_missing_data = "notBreaching"
  alarm_actions      = [local.reconciliation_severity[each.value] == "CRITICAL" ? aws_sns_topic.alerts_critical.arn : aws_sns_topic.alerts.arn]

  tags = { Severity = lower(local.reconciliation_severity[each.value]) }
}

# FAILURE. Separate from drift because "the invariant is violated" and "the invariant is unverified" are
# different incidents. An unverified invariant is the more insidious one: it looks identical to a healthy
# system on every dashboard that only watches for drift.
resource "aws_cloudwatch_metric_alarm" "reconciliation_failure" {
  for_each = toset(["wallet", "inventory", "registration"])

  alarm_name        = "${local.name}-reconciliation-failure-${each.value}"
  alarm_description = <<-EOT
    HIGH. The ${each.value} reconciliation job could not complete.

    WHY IT MATTERS: this is NOT "no drift". It is "we do not know", and it stays unknown until someone fixes
    the job. Every period this is true is a period in which a correctness failure could begin undetected.

    THRESHOLD REASONING: >1 in 15 minutes rather than >0, because Hangfire retries three times and a single
    transient failure that the retry clears is not worth waking anyone. A repeated failure is.

    ACTION: docs/deployment/DATABASE_RUNBOOKS.md#reconciliation-job-failure
  EOT

  namespace   = "Kurx.Reconciliation"
  metric_name = "kurx.reconciliation.runs"
  statistic   = "Sum"

  period             = 900
  evaluation_periods = 1
  threshold          = 1

  comparison_operator = "GreaterThanThreshold"

  dimensions = {
    type    = each.value
    outcome = "failed"
  }

  treat_missing_data = "notBreaching"
  alarm_actions      = [aws_sns_topic.alerts.arn]

  tags = { Severity = "high" }
}

# ── Backups (RDS event subscription — the real AWS mechanism) ──────────────────
#
# An RDS event subscription, NOT a CloudWatch metric alarm: AWS publishes no "backup succeeded" metric to
# alarm the absence of. Backup and snapshot outcomes are RDS *events*, and the failure category is where a
# failed automated backup actually surfaces.
resource "aws_db_event_subscription" "database_events" {
  name      = "${local.name}-database-events"
  sns_topic = aws_sns_topic.alerts_critical.arn

  source_type = "db-instance"
  source_ids  = [aws_db_instance.main.identifier]

  # `backup` carries automated-backup and snapshot outcomes; `failure` carries the instance-level failures
  # that make a backup impossible in the first place (storage full, underlying hardware). `availability` and
  # `maintenance` are included because a Multi-AZ failover during the backup window is the common reason a
  # backup did not happen. Deliberately NOT `configuration change` or `notification`, which are routine and
  # would make this subscription noise.
  event_categories = ["backup", "failure", "availability", "maintenance"]

  tags = { Severity = "critical" }
}

# ── Dashboard ─────────────────────────────────────────────────────────────────
#
# One screen, ordered the way an incident is actually triaged: is the engine healthy, is it being overloaded,
# is it degrading slowly, is the money right. Deliberately small — a dashboard nobody can read at 3am is the
# same as no dashboard. Replication lag is absent because no replica exists; see below.
resource "aws_cloudwatch_dashboard" "database" {
  dashboard_name = "${local.name}-database"

  dashboard_body = jsonencode({
    widgets = [
      {
        type   = "text"
        x      = 0
        y      = 0
        width  = 24
        height = 2
        properties = {
          markdown = join("", [
            "## Kurx database — ${var.environment}\n",
            "**AWS/RDS** = engine, published by AWS. **Kurx.Database** = engine facts AWS does not publish ",
            "(sampled every 5 min by `DatabaseHealthProbeJob`). **Kurx.Reconciliation** = financial/inventory ",
            "correctness — green here means the invariants were *checked and held*, not that the database is up. ",
            "Runbooks: `docs/deployment/DATABASE_RUNBOOKS.md`.",
          ])
        }
      },
      {
        type   = "metric"
        x      = 0
        y      = 2
        width  = 12
        height = 6
        properties = {
          title  = "Capacity — CPU, connections, free storage"
          region = var.aws_region
          view   = "timeSeries"
          stat   = "Average"
          period = 300
          metrics = [
            ["AWS/RDS", "CPUUtilization", "DBInstanceIdentifier", aws_db_instance.main.identifier],
            [".", "DatabaseConnections", ".", ".", { stat = "Maximum" }],
            [".", "FreeStorageSpace", ".", ".", { yAxis = "right" }],
          ]
          annotations = {
            horizontal = [{
              label = "connection alarm"
              value = var.db_connection_alarm_threshold
            }]
          }
        }
      },
      {
        type   = "metric"
        x      = 12
        y      = 2
        width  = 12
        height = 6
        properties = {
          title  = "Throughput & latency — IOPS, read/write latency"
          region = var.aws_region
          view   = "timeSeries"
          stat   = "Average"
          period = 300
          metrics = [
            ["AWS/RDS", "ReadIOPS", "DBInstanceIdentifier", aws_db_instance.main.identifier],
            [".", "WriteIOPS", ".", "."],
            [".", "ReadLatency", ".", ".", { yAxis = "right" }],
            [".", "WriteLatency", ".", ".", { yAxis = "right" }],
          ]
        }
      },
      {
        type   = "metric"
        x      = 0
        y      = 8
        width  = 12
        height = 6
        properties = {
          title  = "Contention — deadlocks (Kurx.Database) and connection failures by reason (DB-2)"
          region = var.aws_region
          view   = "timeSeries"
          stat   = "Sum"
          period = 300
          metrics = [
            ["Kurx.Database", "kurx.db.deadlocks"],
            ["Kurx.Auth", "kurx.db.failures", "reason", "pool_exhausted"],
            ["...", "server_connection_limit"],
            ["...", "command_timeout"],
          ]
        }
      },
      {
        type   = "metric"
        x      = 12
        y      = 8
        width  = 12
        height = 6
        properties = {
          title  = "Bloat — dead tuples as % of live, high-churn tables"
          region = var.aws_region
          view   = "timeSeries"
          stat   = "Average"
          period = 300
          metrics = [
            ["Kurx.Database", "kurx.db.dead_tuple_ratio", "table", "inventory_pools"],
            ["...", "ticket_types"],
            ["...", "organization_wallet"],
          ]
          annotations = {
            horizontal = [{ label = "bloat alarm", value = 20 }]
          }
        }
      },
      {
        type   = "metric"
        x      = 0
        y      = 14
        width  = 24
        height = 6
        properties = {
          title  = "Financial & inventory correctness — reconciliation outcomes (drift/failed must be flat at zero)"
          region = var.aws_region
          view   = "timeSeries"
          stat   = "Sum"
          period = 900
          metrics = [
            ["Kurx.Reconciliation", "kurx.reconciliation.runs", "type", "wallet", "outcome", "drift"],
            ["...", "wallet", ".", "failed"],
            ["...", "inventory", ".", "drift"],
            ["...", "inventory", ".", "failed"],
            ["...", "registration", ".", "drift"],
            ["...", "registration", ".", "failed"],
            ["Kurx.Reconciliation", "kurx.reconciliation.drifting_entities", "type", "wallet", { yAxis = "right" }],
          ]
        }
      },
    ]
  })
}

# ── Replication lag: DEFERRED, deliberately ───────────────────────────────────
#
# Kurx has NO read replica. `aws_db_instance.main` is the only database resource in this configuration and
# nothing sets `replicate_source_db`.
#
# A ReplicaLag alarm here would reference a dimension that resolves to no data, sit permanently in
# INSUFFICIENT_DATA, and — worst of all — appear on a dashboard as a monitored capability. That is a false
# claim of coverage, which is more dangerous than an acknowledged gap.
#
# WHEN A REPLICA IS ADDED, the design is: AWS/RDS `ReplicaLag` (seconds, published per replica instance),
# dimensioned on the replica's DBInstanceIdentifier. Thresholds MUST be derived from what actually reads from
# it — Kurx has no read-replica routing today, so there is no application requirement to derive them from,
# and inventing a number now would only mean shipping an arbitrary one. Revisit with the routing change that
# introduces the replica, not before.
