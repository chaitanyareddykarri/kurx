# ── Outputs ───────────────────────────────────────────────────────────────────
# Deliberately limited to what a deployer or another stack actually needs. Nothing secret is output:
# Terraform outputs land in state and in CI logs.

output "alb_dns_name" {
  description = "ALB hostname. Point the domain's DNS record here."
  value       = aws_lb.main.dns_name
}

output "alb_zone_id" {
  description = "ALB hosted zone, for a Route 53 alias record."
  value       = aws_lb.main.zone_id
}

output "api_url" {
  description = "Public API base URL."
  value       = local.public_origin
}

output "jwks_url" {
  description = "JWKS endpoint (D-099). Public by design — anything verifying a Kurx token reads this."
  value       = "${local.public_origin}/.well-known/jwks.json"
}

output "assetlinks_url" {
  description = <<-EOT
    Where Digital Asset Links must be served for Android passkeys (D-103a). Must return 200 directly
    with application/json — Google's verifier does not follow redirects.
  EOT
  value       = "${local.public_origin}/.well-known/assetlinks.json"
}

output "webauthn_rp_id" {
  description = "The WebAuthn relying party ID passkeys are bound to. Changing it invalidates every existing passkey."
  value       = local.public_host
}

output "signing_key_kms_arn" {
  description = "CMK wrapping private JWT signing keys. Set as AWS_KMS_SIGNING_KEY_ID (D-102a)."
  value       = aws_kms_key.signing.arn
}

output "secrets_prefix" {
  description = "Secrets Manager name prefix. Set as AWS_SECRETS_PREFIX (D-101a)."
  value       = "kurx/${var.environment}/"
}

output "secret_arns" {
  description = "Secret containers created by Terraform. Values must be populated out-of-band."
  value       = { for k, v in aws_secretsmanager_secret.app : k => v.arn }
}

output "database_endpoint" {
  description = "RDS endpoint. Reachable only from API tasks."
  value       = aws_db_instance.main.endpoint
}

output "database_secret_arn" {
  description = "AWS-managed master credential secret. The password never enters Terraform state."
  value       = aws_db_instance.main.master_user_secret[0].secret_arn
}

output "redis_endpoint" {
  description = "ElastiCache primary endpoint (TLS)."
  value       = aws_elasticache_replication_group.main.primary_endpoint_address
}

output "ecs_cluster_name" {
  description = "ECS cluster, for deploy scripts."
  value       = aws_ecs_cluster.main.name
}

output "ecs_service_name" {
  description = "ECS service, for forcing a new deployment."
  value       = aws_ecs_service.api.name
}

output "alerts_topic_arn" {
  description = "SNS topic for CloudWatch alarms. Subscribe an on-call address or PagerDuty here."
  value       = aws_sns_topic.alerts.arn
}

output "log_group_name" {
  description = "CloudWatch log group carrying structured JSON logs with trace correlation (D-100)."
  value       = aws_cloudwatch_log_group.api.name
}
