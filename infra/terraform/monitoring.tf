# ── CloudWatch alarms ─────────────────────────────────────────────────────────
# Alarms are chosen for what actually pages someone. Anything that fires routinely gets ignored, and
# an ignored alarm is worse than none — so this is a short list of genuinely actionable conditions.

resource "aws_sns_topic" "alerts" {
  name              = "${local.name}-alerts"
  kms_master_key_id = aws_kms_key.data.id

  tags = { Name = "${local.name}-alerts" }
}

resource "aws_cloudwatch_metric_alarm" "api_unhealthy_hosts" {
  alarm_name          = "${local.name}-api-unhealthy-hosts"
  alarm_description   = "API tasks are failing health checks. Authentication may be degraded or down."
  namespace           = "AWS/ApplicationELB"
  metric_name         = "UnHealthyHostCount"
  statistic           = "Maximum"
  period              = 60
  evaluation_periods  = 2
  threshold           = 0
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"

  dimensions = {
    LoadBalancer = aws_lb.main.arn_suffix
    TargetGroup  = aws_lb_target_group.api.arn_suffix
  }

  alarm_actions = [aws_sns_topic.alerts.arn]
  ok_actions    = [aws_sns_topic.alerts.arn]
}

resource "aws_cloudwatch_metric_alarm" "api_5xx" {
  alarm_name          = "${local.name}-api-5xx"
  alarm_description   = "Sustained 5xx from the API."
  namespace           = "AWS/ApplicationELB"
  metric_name         = "HTTPCode_Target_5XX_Count"
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 2
  threshold           = 25
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"

  dimensions    = { LoadBalancer = aws_lb.main.arn_suffix }
  alarm_actions = [aws_sns_topic.alerts.arn]
}

resource "aws_cloudwatch_metric_alarm" "api_latency" {
  alarm_name          = "${local.name}-api-latency-p99"
  alarm_description   = "p99 latency above 2s. Usually a slow query or KMS latency on the signing path."
  namespace           = "AWS/ApplicationELB"
  metric_name         = "TargetResponseTime"
  extended_statistic  = "p99"
  period              = 300
  evaluation_periods  = 3
  threshold           = 2
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"

  dimensions    = { LoadBalancer = aws_lb.main.arn_suffix }
  alarm_actions = [aws_sns_topic.alerts.arn]
}

resource "aws_cloudwatch_metric_alarm" "database_cpu" {
  alarm_name          = "${local.name}-database-cpu"
  alarm_description   = "Database CPU sustained high."
  namespace           = "AWS/RDS"
  metric_name         = "CPUUtilization"
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 3
  threshold           = 80
  comparison_operator = "GreaterThanThreshold"

  dimensions    = { DBInstanceIdentifier = aws_db_instance.main.identifier }
  alarm_actions = [aws_sns_topic.alerts.arn]
}

resource "aws_cloudwatch_metric_alarm" "database_storage" {
  alarm_name          = "${local.name}-database-storage"
  alarm_description   = "Database free storage below 10GB."
  namespace           = "AWS/RDS"
  metric_name         = "FreeStorageSpace"
  statistic           = "Minimum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 10737418240
  comparison_operator = "LessThanThreshold"

  dimensions    = { DBInstanceIdentifier = aws_db_instance.main.identifier }
  alarm_actions = [aws_sns_topic.alerts.arn]
}

# ── Security alarms on application telemetry (D-100) ──────────────────────────
# These read the custom metrics the auth platform emits. They are the ones that indicate an attack
# rather than a malfunction.

resource "aws_cloudwatch_metric_alarm" "auth_denied_spike" {
  alarm_name          = "${local.name}-auth-denied-spike"
  alarm_description   = <<-EOT
    Spike in risk-engine denials or decoy responses. Indicates credential stuffing or an account
    enumeration sweep in progress. The decoy rate is otherwise invisible — by design the response is
    identical to a real one (D-100).
  EOT
  namespace           = "Kurx.Auth"
  metric_name         = "kurx.auth.attempts"
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 500
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"

  dimensions    = { outcome = "denied" }
  alarm_actions = [aws_sns_topic.alerts.arn]
}

resource "aws_cloudwatch_metric_alarm" "token_reuse_detected" {
  alarm_name          = "${local.name}-token-reuse-detected"
  alarm_description   = <<-EOT
    Refresh-token reuse detected — a theft signal (D-081). Any occurrence is worth a human look, so
    the threshold is deliberately zero rather than a rate.
  EOT
  namespace           = "Kurx.Auth"
  metric_name         = "kurx.auth.security_events"
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 0
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"

  dimensions    = { type = "refresh.reuse_detected" }
  alarm_actions = [aws_sns_topic.alerts.arn]
}

resource "aws_cloudwatch_metric_alarm" "waf_blocked_spike" {
  alarm_name          = "${local.name}-waf-blocked-spike"
  alarm_description   = "WAF blocking at volume — likely an active attack on the auth endpoints."
  namespace           = "AWS/WAFV2"
  metric_name         = "BlockedRequests"
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 1000
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"

  dimensions = {
    WebACL = aws_wafv2_web_acl.main.name
    Region = var.aws_region
    Rule   = "ALL"
  }

  alarm_actions = [aws_sns_topic.alerts.arn]
}
