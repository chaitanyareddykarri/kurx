# ── KMS ───────────────────────────────────────────────────────────────────────
# The CMK that wraps private JWT signing keys (D-102a). Its key policy is the real access control:
# IAM alone is not sufficient for KMS, and a permissive key policy silently undoes a careful IAM one.

data "aws_caller_identity" "current" {}

resource "aws_kms_key" "signing" {
  description = "Envelope encryption of Kurx JWT private signing keys"
  # Rotating the CMK does not invalidate anything: existing data keys stay decryptable, and the
  # application's own key lifecycle (D-099) is separate and independent.
  enable_key_rotation     = true
  deletion_window_in_days = 30 # maximum; a deleted CMK makes every wrapped signing key unrecoverable

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        # Without this, the key becomes unmanageable if the creating principal is removed.
        Sid       = "EnableRootAccountManagement"
        Effect    = "Allow"
        Principal = { AWS = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:root" }
        Action    = "kms:*"
        Resource  = "*"
      },
      {
        # Exactly the three operations SigningKeyProtector performs — no Encrypt, no re-policy,
        # no schedule-deletion. Least privilege expressed where KMS actually enforces it.
        Sid       = "AllowApiTaskUseOnly"
        Effect    = "Allow"
        Principal = { AWS = aws_iam_role.api_task.arn }
        Action = [
          "kms:GenerateDataKey",
          "kms:Decrypt",
          "kms:DescribeKey",
        ]
        Resource = "*"
      },
    ]
  })

  tags = { Name = "${local.name}-signing" }
}

resource "aws_kms_alias" "signing" {
  name          = "alias/${local.name}-signing"
  target_key_id = aws_kms_key.signing.key_id
}

# Separate CMK for data at rest. Kept distinct from the signing key so that revoking or rotating
# access to one does not affect the other, and so CloudTrail cleanly separates "someone used a
# signing key" from ordinary storage encryption.
resource "aws_kms_key" "data" {
  description             = "Encryption at rest for RDS, ElastiCache, Secrets Manager and logs"
  enable_key_rotation     = true
  deletion_window_in_days = 30

  # Without an explicit policy this key gets the AWS default (root only), and CloudWatch Logs cannot
  # use it — CreateLogGroup fails with AccessDeniedException naming the key. Logs encrypts through
  # the service principal rather than the caller's identity, so IAM on the deployer is irrelevant;
  # the grant has to live here. The EncryptionContext condition confines the grant to log groups in
  # this account, so the key cannot be used to encrypt an attacker's log group elsewhere.
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "EnableRootAccountManagement"
        Effect    = "Allow"
        Principal = { AWS = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:root" }
        Action    = "kms:*"
        Resource  = "*"
      },
      {
        Sid       = "AllowCloudWatchLogs"
        Effect    = "Allow"
        Principal = { Service = "logs.${var.aws_region}.amazonaws.com" }
        Action = [
          "kms:Encrypt*",
          "kms:Decrypt*",
          "kms:ReEncrypt*",
          "kms:GenerateDataKey*",
          "kms:Describe*",
        ]
        Resource = "*"
        Condition = {
          ArnLike = {
            "kms:EncryptionContext:aws:logs:arn" = "arn:aws:logs:${var.aws_region}:${data.aws_caller_identity.current.account_id}:log-group:*"
          }
        }
      },
    ]
  })

  tags = { Name = "${local.name}-data" }
}

resource "aws_kms_alias" "data" {
  name          = "alias/${local.name}-data"
  target_key_id = aws_kms_key.data.key_id
}
