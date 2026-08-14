# ── IAM ───────────────────────────────────────────────────────────────────────
# Two roles with deliberately different scopes:
#   execution role — used by ECS itself to START the task (pull image, fetch secrets, write logs)
#   task role      — assumed by the APPLICATION at runtime
#
# Keeping them separate matters: the execution role can read secrets in order to inject them, but the
# running application does not inherit that. A compromised application process cannot enumerate
# Secrets Manager just because the platform needed those values at boot.

data "aws_iam_policy_document" "ecs_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ecs-tasks.amazonaws.com"]
    }
    # Confused-deputy protection: restricts this trust to tasks in THIS account.
    condition {
      test     = "StringEquals"
      variable = "aws:SourceAccount"
      values   = [data.aws_caller_identity.current.account_id]
    }
  }
}

resource "aws_iam_role" "api_execution" {
  name               = "${local.name}-api-execution"
  assume_role_policy = data.aws_iam_policy_document.ecs_assume.json
}

resource "aws_iam_role_policy_attachment" "api_execution_managed" {
  role       = aws_iam_role.api_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

data "aws_iam_policy_document" "api_execution_secrets" {
  # Only the specific secrets this service uses — never secretsmanager:* on "*".
  statement {
    sid     = "ReadOwnSecrets"
    actions = ["secretsmanager:GetSecretValue"]
    resources = concat(
      [for s in aws_secretsmanager_secret.app : s.arn],
      [aws_db_instance.main.master_user_secret[0].secret_arn],
    )
  }

  statement {
    sid       = "DecryptSecrets"
    actions   = ["kms:Decrypt"]
    resources = [aws_kms_key.data.arn]
  }
}

resource "aws_iam_role_policy" "api_execution_secrets" {
  name   = "${local.name}-api-execution-secrets"
  role   = aws_iam_role.api_execution.id
  policy = data.aws_iam_policy_document.api_execution_secrets.json
}

resource "aws_iam_role" "api_task" {
  name               = "${local.name}-api-task"
  assume_role_policy = data.aws_iam_policy_document.ecs_assume.json
}

data "aws_iam_policy_document" "api_task" {
  # The three KMS operations SigningKeyProtector performs (D-102a), on the signing CMK only.
  # Note there is no kms:Encrypt: envelope encryption never calls it.
  statement {
    sid       = "SigningKeyEnvelopeOperations"
    actions   = ["kms:GenerateDataKey", "kms:Decrypt", "kms:DescribeKey"]
    resources = [aws_kms_key.signing.arn]
  }

  # Runtime secret reads (D-101a) — scoped by prefix so a new secret does not require an IAM change,
  # but a secret from another environment is still out of reach.
  statement {
    sid       = "ReadEnvironmentSecrets"
    actions   = ["secretsmanager:GetSecretValue", "secretsmanager:DescribeSecret"]
    resources = ["arn:aws:secretsmanager:${var.aws_region}:${data.aws_caller_identity.current.account_id}:secret:kurx/${var.environment}/*"]
  }

  statement {
    sid       = "PublishSms"
    actions   = ["sns:Publish"]
    resources = ["*"] # SNS SMS-to-phone-number has no resource ARN to scope to
    condition {
      test     = "StringEquals"
      variable = "sns:Protocol"
      values   = ["sms"]
    }
  }

  statement {
    sid       = "WriteOwnLogs"
    actions   = ["logs:CreateLogStream", "logs:PutLogEvents"]
    resources = ["${aws_cloudwatch_log_group.api.arn}:*"]
  }

  # X-Ray/ADOT trace ingestion (D-100). These APIs take no resource ARN.
  statement {
    sid       = "PublishTelemetry"
    actions   = ["xray:PutTraceSegments", "xray:PutTelemetryRecords", "cloudwatch:PutMetricData"]
    resources = ["*"]
  }
}

resource "aws_iam_role_policy" "api_task" {
  name   = "${local.name}-api-task"
  role   = aws_iam_role.api_task.id
  policy = data.aws_iam_policy_document.api_task.json
}
