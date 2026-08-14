# ── RDS PostgreSQL ────────────────────────────────────────────────────────────

resource "aws_db_subnet_group" "main" {
  name       = local.name
  subnet_ids = aws_subnet.isolated[*].id
  tags       = { Name = local.name }
}

resource "aws_db_parameter_group" "main" {
  name   = local.name
  family = "postgres17"

  # Reject any non-TLS connection. The application connects over TLS; enforcing it here means a
  # misconfigured client fails loudly instead of silently sending credentials in the clear.
  parameter {
    name  = "rds.force_ssl"
    value = "1"
  }

  # Log slow queries. Auth endpoints are latency-sensitive and a slow query is the usual cause.
  #
  # DB-9 reviewed this value and deliberately KEPT 1000ms. Lowering it is the obvious-looking change and the
  # wrong one: at 100ms an India-scale event on-sale would write a log line per ordinary indexed read, which
  # costs CloudWatch Logs ingest, buries the genuinely slow statements it exists to surface, and can itself
  # become the bottleneck. 1000ms is "no interactive request should ever take this long"; pg_stat_statements
  # below is the right tool for the sub-second distribution, because it aggregates instead of logging.
  parameter {
    name  = "log_min_duration_statement"
    value = "1000"
  }

  # ── DB-9 observability ──────────────────────────────────────────────────────
  # pg_stat_statements: the normalised per-query aggregate (calls, total/mean time, rows, shared block
  # reads/hits). Nothing else answers "which query shape is costing the most", and slow-query logs cannot —
  # they show individual slow statements, never the fast query executed ten million times.
  #
  # STATIC parameter: shared_preload_libraries is read once at postmaster start, so this takes effect on the
  # next reboot and NOT on apply. Set explicitly rather than relying on the family default, which AWS may
  # change between engine versions.
  parameter {
    name         = "shared_preload_libraries"
    value        = "pg_stat_statements"
    apply_method = "pending-reboot"
  }

  # `top` records statements issued directly by clients but not those nested inside functions. Chosen over
  # `all` because nested tracking multiplies the entry count for detail this codebase cannot act on — it has
  # no stored procedures; every query originates from EF Core.
  parameter {
    name  = "pg_stat_statements.track"
    value = "top"
  }

  # Distinct normalised statements retained. EF Core generates a bounded set of query shapes, so the 5,000
  # default is ample; pinned rather than inherited so that eviction behaviour is a decision on record.
  # Static — the store is allocated in shared memory at start.
  parameter {
    name         = "pg_stat_statements.max"
    value        = "5000"
    apply_method = "pending-reboot"
  }

  # Log a line whenever a session waits longer than deadlock_timeout (1s) for a lock. This is the input to
  # deadlock diagnosis: the deadlock itself is already logged with the statements involved, and lock waits
  # are the leading indicator that shows contention building before it becomes a deadlock. Cheap because it
  # only fires past a full second of waiting.
  parameter {
    name  = "log_lock_waits"
    value = "1"
  }

  # Log autovacuum runs lasting over a second, so "is autovacuum keeping up with inventory_pools" is
  # answerable from the log export rather than by inference from the bloat gauges alone.
  parameter {
    name  = "log_autovacuum_min_duration"
    value = "1000"
  }

  tags = { Name = local.name }
}

resource "aws_db_instance" "main" {
  identifier     = local.name
  engine         = "postgres"
  engine_version = "17"
  instance_class = var.db_instance_class

  allocated_storage     = 50
  max_allocated_storage = 500 # storage autoscaling; running out of disk is an avoidable outage
  storage_type          = "gp3"
  storage_encrypted     = true
  kms_key_id            = aws_kms_key.data.arn

  db_name  = "kurx"
  username = "kurx"
  # Password is generated and stored by AWS in Secrets Manager — it never exists in Terraform state
  # or in this repository.
  manage_master_user_password   = true
  master_user_secret_kms_key_id = aws_kms_key.data.arn

  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.database.id]
  parameter_group_name   = aws_db_parameter_group.main.name
  publicly_accessible    = false

  # Multi-AZ: the platform's entire auth state lives here. A single-AZ database makes an AZ failure
  # a total authentication outage.
  multi_az = var.environment == "production"

  backup_retention_period = var.environment == "production" ? 30 : 7
  backup_window           = "18:00-19:00" # ~23:30 IST, lowest traffic for an India-focused platform
  maintenance_window      = "sun:19:30-sun:20:30"
  copy_tags_to_snapshot   = true

  # Point-in-time recovery is implied by backup retention; deletion protection stops an accidental
  # `terraform destroy` from taking the platform with it.
  deletion_protection       = var.environment == "production"
  skip_final_snapshot       = false
  final_snapshot_identifier = "${local.name}-final-${formatdate("YYYYMMDDhhmmss", timestamp())}"

  performance_insights_enabled    = true
  performance_insights_kms_key_id = aws_kms_key.data.arn
  enabled_cloudwatch_logs_exports = ["postgresql"]
  auto_minor_version_upgrade      = true

  lifecycle {
    # timestamp() changes on every plan; without this the final snapshot name would force a diff.
    ignore_changes = [final_snapshot_identifier]
  }

  tags = { Name = local.name }
}

# ── ElastiCache Redis ─────────────────────────────────────────────────────────
# Used for the SignalR backplane (so multiple tasks can push to one client) and cache. Not a
# system of record — losing it degrades, never corrupts.

resource "aws_elasticache_subnet_group" "main" {
  name       = local.name
  subnet_ids = aws_subnet.isolated[*].id
}

resource "aws_elasticache_replication_group" "main" {
  replication_group_id = local.name
  description          = "Kurx SignalR backplane and cache"

  engine         = "redis"
  engine_version = "7.1"
  node_type      = var.redis_node_type
  port           = 6379

  # Two nodes with automatic failover in production: a single node makes SignalR fan-out an SPOF.
  num_cache_clusters         = var.environment == "production" ? 2 : 1
  automatic_failover_enabled = var.environment == "production"
  multi_az_enabled           = var.environment == "production"

  subnet_group_name  = aws_elasticache_subnet_group.main.name
  security_group_ids = [aws_security_group.redis.id]

  at_rest_encryption_enabled = true
  kms_key_id                 = aws_kms_key.data.arn
  transit_encryption_enabled = true

  snapshot_retention_limit = var.environment == "production" ? 7 : 1
  maintenance_window       = "sun:20:30-sun:21:30"
  apply_immediately        = false

  tags = { Name = local.name }
}
