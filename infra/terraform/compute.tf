# ── ALB + ECS Fargate ─────────────────────────────────────────────────────────
# Fargate over EKS: one stateless HTTP service does not justify a Kubernetes control plane, its
# upgrade cadence, or the operational knowledge it demands. Revisit if the platform grows to many
# services with complex scheduling needs.

resource "aws_lb" "main" {
  name               = local.name
  load_balancer_type = "application"
  subnets            = aws_subnet.public[*].id
  security_groups    = [aws_security_group.alb.id]

  enable_deletion_protection = var.environment == "production"
  drop_invalid_header_fields = true # header smuggling defence
  idle_timeout               = 120  # SignalR long-polling holds connections open (D-087)

  tags = { Name = local.name }
}

resource "aws_lb_target_group" "api" {
  name        = "${local.name}-api"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = aws_vpc.main.id
  target_type = "ip"

  health_check {
    path                = "/health"
    healthy_threshold   = 2
    unhealthy_threshold = 3
    timeout             = 5
    interval            = 15
    matcher             = "200"
  }

  # Give in-flight requests time to finish on deploy and scale-in. SignalR connections and a slow
  # KMS-backed first request both benefit.
  deregistration_delay = 60

  # SignalR long-polling wants the same client back on the same task when no Redis backplane is in
  # play; harmless when there is one.
  stickiness {
    type            = "lb_cookie"
    cookie_duration = 3600
    enabled         = true
  }

  tags = { Name = "${local.name}-api" }
}

locals {
  # Without HTTPS there is no domain, so the ALB's own hostname is the only address the API has.
  # Everything origin-shaped derives from these, so the scheme can never disagree with the listener
  # that is actually running.
  public_host   = var.enable_https ? var.domain_name : aws_lb.main.dns_name
  public_origin = var.enable_https ? "https://${var.domain_name}" : "http://${aws_lb.main.dns_name}"
}

resource "aws_lb_listener" "https" {
  count = var.enable_https ? 1 : 0

  load_balancer_arn = aws_lb.main.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06" # TLS 1.2 floor, 1.3 preferred
  certificate_arn   = var.certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}

# Redirect only, never serve content over http. Asset Links verification does not follow redirects
# (D-103a), so it must be pointed at https directly.
resource "aws_lb_listener" "http_redirect" {
  count = var.enable_https ? 1 : 0

  load_balancer_arn = aws_lb.main.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type = "redirect"
    redirect {
      port        = "443"
      protocol    = "HTTPS"
      status_code = "HTTP_301"
    }
  }
}

# The enable_https = false path: port 80 serves the API directly, because there is no 443 to send it
# to. Reachable only where the operator has explicitly accepted the clear-text posture.
resource "aws_lb_listener" "http_forward" {
  count = var.enable_https ? 0 : 1

  load_balancer_arn = aws_lb.main.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}

resource "aws_ecs_cluster" "main" {
  name = local.name

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  tags = { Name = local.name }
}

resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/${local.name}-api"
  retention_in_days = var.log_retention_days
  kms_key_id        = aws_kms_key.data.arn

  tags = { Name = "${local.name}-api" }
}

resource "aws_ecs_task_definition" "api" {
  family                   = "${local.name}-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.api_cpu
  memory                   = var.api_memory
  execution_role_arn       = aws_iam_role.api_execution.arn
  task_role_arn            = aws_iam_role.api_task.arn

  container_definitions = jsonencode([
    {
      name      = "api"
      image     = var.container_image
      essential = true

      portMappings = [{ containerPort = 8080, protocol = "tcp" }]

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "ASPNETCORE_URLS", value = "http://0.0.0.0:8080" },
        # Secrets come from Secrets Manager at runtime (D-101a).
        { name = "SECRETS_PROVIDER", value = "aws" },
        { name = "AWS_SECRETS_PREFIX", value = "kurx/${var.environment}/" },
        # Private signing keys must be KMS-wrapped; the app refuses to start otherwise (D-102a).
        { name = "SIGNING_KEY_PROTECTION", value = "kms" },
        { name = "AWS_KMS_SIGNING_KEY_ID", value = aws_kms_key.signing.arn },
        # WebAuthn RP: passkeys are bound to this domain and changing it invalidates them (D-097).
        # With enable_https = false this is the ALB hostname over http, which no browser will accept
        # as a WebAuthn secure context — passkeys are inoperable in that mode by construction.
        { name = "WEBAUTHN_RP_ID", value = local.public_host },
        { name = "WEBAUTHN_ORIGINS", value = local.public_origin },
        { name = "ALLOWED_ORIGINS", value = local.public_origin },
        { name = "OTEL_EXPORTER_OTLP_ENDPOINT", value = "http://localhost:4317" },
        { name = "OTEL_SERVICE_NAME", value = "kurx-api" },
        { name = "REDIS_CONNECTION", value = "${aws_elasticache_replication_group.main.primary_endpoint_address}:6379,ssl=true" },
        { name = "SMS_PROVIDER", value = "sns" },
        { name = "PUSH_PROVIDER", value = "firebase" },
        { name = "AWS_REGION", value = var.aws_region },
      ]

      # Injected by ECS at start-up using the EXECUTION role, so the application process never needs
      # permission to read them itself.
      secrets = [
        { name = "ConnectionStrings__Default", valueFrom = "${aws_db_instance.main.master_user_secret[0].secret_arn}:connectionString::" },
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.api.name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "api"
        }
      }

      # Complements the load balancer check: this one restarts a wedged container even after the
      # load balancer has already stopped routing to it.
      healthCheck = {
        command     = ["CMD-SHELL", "curl -fsS http://localhost:8080/health || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60 # migrations run at startup
      }
    },
    {
      # ADOT sidecar: receives OTLP from the app (D-100) and forwards to CloudWatch and X-Ray.
      name      = "otel-collector"
      image     = "public.ecr.aws/aws-observability/aws-otel-collector:latest"
      essential = false # telemetry must never take the API down with it
      command   = ["--config=/etc/ecs/ecs-default-config.yaml"]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.api.name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "otel"
        }
      }
    },
  ])

  tags = { Name = "${local.name}-api" }
}

resource "aws_ecs_service" "api" {
  name            = "${local.name}-api"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.api_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.api.id]
    assign_public_ip = false # egress is via NAT; tasks are never directly reachable
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn
    container_name   = "api"
    container_port   = 8080
  }

  # Rolling deploy that keeps full capacity: old tasks only drain once new ones are healthy, so a
  # deploy never reduces authentication capacity.
  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200

  deployment_circuit_breaker {
    enable   = true
    rollback = true # a failed deploy rolls back instead of leaving a broken service
  }

  health_check_grace_period_seconds = 90 # startup migrations
  enable_execute_command            = var.environment != "production"

  # Whichever listener the enable_https choice produced must exist before tasks register as targets.
  depends_on = [aws_lb_listener.https, aws_lb_listener.http_forward]

  tags = { Name = "${local.name}-api" }
}

# ── Autoscaling ───────────────────────────────────────────────────────────────

resource "aws_appautoscaling_target" "api" {
  service_namespace  = "ecs"
  resource_id        = "service/${aws_ecs_cluster.main.name}/${aws_ecs_service.api.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  min_capacity       = var.api_desired_count
  max_capacity       = var.api_desired_count * 5
}

resource "aws_appautoscaling_policy" "api_cpu" {
  name               = "${local.name}-api-cpu"
  policy_type        = "TargetTrackingScaling"
  service_namespace  = aws_appautoscaling_target.api.service_namespace
  resource_id        = aws_appautoscaling_target.api.resource_id
  scalable_dimension = aws_appautoscaling_target.api.scalable_dimension

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
    target_value       = 65
    scale_in_cooldown  = 300 # slow to scale in: auth traffic is spiky
    scale_out_cooldown = 60  # fast to scale out
  }
}

resource "aws_appautoscaling_policy" "api_requests" {
  name               = "${local.name}-api-requests"
  policy_type        = "TargetTrackingScaling"
  service_namespace  = aws_appautoscaling_target.api.service_namespace
  resource_id        = aws_appautoscaling_target.api.resource_id
  scalable_dimension = aws_appautoscaling_target.api.scalable_dimension

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ALBRequestCountPerTarget"
      resource_label         = "${aws_lb.main.arn_suffix}/${aws_lb_target_group.api.arn_suffix}"
    }
    target_value       = 1000
    scale_in_cooldown  = 300
    scale_out_cooldown = 60
  }
}
