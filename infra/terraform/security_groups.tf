# ── Security groups ───────────────────────────────────────────────────────────
# Every rule is referenced by security-group id rather than CIDR. A CIDR rule says "anything in this
# subnet range"; an SG reference says "specifically the ALB" — and stays correct when subnets change.

resource "aws_security_group" "alb" {
  name        = "${local.name}-alb"
  description = "Public ingress. The only thing reachable from the internet."
  vpc_id      = aws_vpc.main.id

  tags = { Name = "${local.name}-alb" }
}

resource "aws_vpc_security_group_ingress_rule" "alb_https" {
  security_group_id = aws_security_group.alb.id
  description       = "HTTPS from the internet"
  cidr_ipv4         = "0.0.0.0/0"
  from_port         = 443
  to_port           = 443
  ip_protocol       = "tcp"
}

# Port 80 exists only to redirect to 443. Passkeys and Asset Links both require HTTPS, and Asset
# Links verification does not follow redirects (D-103a) — so http:// must never serve content.
resource "aws_vpc_security_group_ingress_rule" "alb_http_redirect" {
  security_group_id = aws_security_group.alb.id
  description       = "HTTP, redirected to HTTPS"
  cidr_ipv4         = "0.0.0.0/0"
  from_port         = 80
  to_port           = 80
  ip_protocol       = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "alb_to_api" {
  security_group_id            = aws_security_group.alb.id
  description                  = "ALB to API tasks"
  referenced_security_group_id = aws_security_group.api.id
  from_port                    = 8080
  to_port                      = 8080
  ip_protocol                  = "tcp"
}

resource "aws_security_group" "api" {
  name        = "${local.name}-api"
  description = "ECS tasks. No inbound except from the ALB."
  vpc_id      = aws_vpc.main.id

  tags = { Name = "${local.name}-api" }
}

resource "aws_vpc_security_group_ingress_rule" "api_from_alb" {
  security_group_id            = aws_security_group.api.id
  description                  = "Only the ALB may reach the API"
  referenced_security_group_id = aws_security_group.alb.id
  from_port                    = 8080
  to_port                      = 8080
  ip_protocol                  = "tcp"
}

# Outbound is unrestricted because the API legitimately calls FCM, SNS and the OTLP collector.
# KMS and Secrets Manager go via VPC endpoints rather than this route.
resource "aws_vpc_security_group_egress_rule" "api_egress" {
  security_group_id = aws_security_group.api.id
  description       = "Outbound to AWS services and third-party providers (FCM, SNS)"
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "-1"
}

resource "aws_security_group" "database" {
  name        = "${local.name}-database"
  description = "Postgres. Reachable only from API tasks; no egress at all."
  vpc_id      = aws_vpc.main.id

  tags = { Name = "${local.name}-database" }
}

resource "aws_vpc_security_group_ingress_rule" "database_from_api" {
  security_group_id            = aws_security_group.database.id
  description                  = "Postgres from API tasks only"
  referenced_security_group_id = aws_security_group.api.id
  from_port                    = 5432
  to_port                      = 5432
  ip_protocol                  = "tcp"
}
# No egress rule: the database has no reason to originate a connection, and denying it removes an
# exfiltration path if the instance is ever compromised.

resource "aws_security_group" "redis" {
  name        = "${local.name}-redis"
  description = "ElastiCache. Reachable only from API tasks."
  vpc_id      = aws_vpc.main.id

  tags = { Name = "${local.name}-redis" }
}

resource "aws_vpc_security_group_ingress_rule" "redis_from_api" {
  security_group_id            = aws_security_group.redis.id
  description                  = "Redis from API tasks only"
  referenced_security_group_id = aws_security_group.api.id
  from_port                    = 6379
  to_port                      = 6379
  ip_protocol                  = "tcp"
}

resource "aws_security_group" "vpc_endpoints" {
  name        = "${local.name}-vpc-endpoints"
  description = "Interface endpoints for KMS and Secrets Manager."
  vpc_id      = aws_vpc.main.id

  tags = { Name = "${local.name}-vpc-endpoints" }
}

resource "aws_vpc_security_group_ingress_rule" "endpoints_from_api" {
  security_group_id            = aws_security_group.vpc_endpoints.id
  description                  = "HTTPS from API tasks"
  referenced_security_group_id = aws_security_group.api.id
  from_port                    = 443
  to_port                      = 443
  ip_protocol                  = "tcp"
}
