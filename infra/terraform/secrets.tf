# ── Secrets Manager ───────────────────────────────────────────────────────────
# Consumed by AwsSecretsManagerProvider (D-101a) with the prefix kurx/<environment>/.
#
# Terraform creates the secret CONTAINERS but never their values. A value written here would land
# in Terraform state, which is exactly what Secrets Manager exists to avoid. Populate out-of-band:
#   aws secretsmanager put-secret-value --secret-id kurx/production/JWT_SECRET --secret-string "$(openssl rand -base64 48)"

locals {
  # JWT_SECRET remains required during the staged ES256 migration (D-099): it still validates legacy
  # HS256 tokens minted before cut-over. It can only be removed once no such token can be live.
  managed_secrets = [
    "JWT_SECRET",
    "TICKET_HMAC_SECRET",
    "OTP_PEPPER",
    "FCM_SERVICE_ACCOUNT_JSON",
    "RAZORPAY_KEY_SECRET",
    "RAZORPAY_WEBHOOK_SECRET",
  ]
}

resource "aws_secretsmanager_secret" "app" {
  for_each = toset(local.managed_secrets)

  name        = "kurx/${var.environment}/${each.value}"
  description = "Kurx ${var.environment} — ${each.value}"
  kms_key_id  = aws_kms_key.data.arn

  # Long enough to recover from an accidental delete, short enough that a rotated secret's old
  # version does not linger indefinitely.
  recovery_window_in_days = 30

  tags = { Name = "kurx-${var.environment}-${lower(each.value)}" }
}
