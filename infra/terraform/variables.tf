variable "aws_region" {
  description = "AWS region. ap-south-1 (Mumbai) keeps data and latency in-country for an India-focused platform."
  type        = string
  default     = "ap-south-1"
}

variable "environment" {
  description = "Environment name; used in every resource name so one account can host several."
  type        = string

  validation {
    condition     = contains(["staging", "production"], var.environment)
    error_message = "environment must be staging or production."
  }
}

variable "enable_https" {
  description = <<-EOT
    Terminate TLS at the ALB using an ACM certificate. Leave this true for anything real.

    false serves the API as plain HTTP on the ALB's own hostname, for a throwaway environment that
    has no domain yet. It is not a supported production posture: WebAuthn requires a secure context,
    so no passkey can be registered or asserted (D-097), and Asset Links has no HTTPS origin to be
    served from (D-103a). Credentials and tokens cross the internet in clear text.
  EOT
  type        = bool
  default     = true
}

variable "domain_name" {
  description = <<-EOT
    The registrable domain. This is the WebAuthn RP ID (D-097): passkeys are bound to it, and
    changing it later invalidates every existing passkey. Asset Links is served from
    https://<domain>/.well-known/assetlinks.json.

    Required unless enable_https = false, in which case the ALB hostname takes its place.
  EOT
  type        = string
  default     = ""

  validation {
    condition     = !var.enable_https || var.domain_name != ""
    error_message = "domain_name is required when enable_https is true."
  }
}

variable "certificate_arn" {
  description = "ACM certificate for the ALB. Required unless enable_https = false."
  type        = string
  default     = ""

  validation {
    condition     = !var.enable_https || var.certificate_arn != ""
    error_message = "certificate_arn is required when enable_https is true."
  }
}

variable "container_image" {
  description = "ECR image URI for the API, including tag. Never :latest — a rollback must be addressable."
  type        = string

  validation {
    condition     = !endswith(var.container_image, ":latest")
    error_message = "Pin an immutable tag; :latest makes rollbacks and audits impossible."
  }
}

variable "vpc_cidr" {
  type    = string
  default = "10.0.0.0/16"
}

variable "db_instance_class" {
  type    = string
  default = "db.t4g.medium"
}

variable "db_connection_alarm_threshold" {
  description = <<-EOT
    Server-side connection count that pages (DB-9). A variable rather than a literal because it is derived
    from the instance class, and resizing the database silently invalidates any number written into an alarm.

    Arithmetic for the db.t4g.medium default. RDS sets max_connections to
    LEAST({DBInstanceClassMemory/9531392}, 5000); 4 GiB gives roughly 450. The application's own ceiling at
    full scale-out is api_desired_count * 5 = 10 tasks x (MaxPoolSize 20 + JobsMaxPoolSize 8) = 280
    (docs/deployment/CONNECTION_POOLING.md, DB-2), plus Hangfire's polling and heartbeat connections.

    340 sits above everything the application is configured to be able to demand and at ~75% of the server
    ceiling. Breaching it therefore means connections are being held by something outside that budget — a
    leak, a stuck migration, a forgotten psql session — rather than by ordinary traffic, which is what makes
    it actionable instead of merely correlated with load. RAISE THIS when resizing the instance class.
  EOT
  type        = number
  default     = 340
}

variable "redis_node_type" {
  type    = string
  default = "cache.t4g.micro"
}

variable "api_desired_count" {
  description = "Baseline task count. Minimum 2: a single task means a deploy or an AZ failure is an outage."
  type        = number
  default     = 2

  validation {
    condition     = var.api_desired_count >= 2
    error_message = "Run at least 2 tasks so the service survives one task or AZ failing."
  }
}

variable "api_cpu" {
  type    = number
  default = 512
}

variable "api_memory" {
  type    = number
  default = 1024
}

variable "log_retention_days" {
  description = "CloudWatch retention. Auth logs are security evidence; 90 days is the practical floor for incident review."
  type        = number
  default     = 90
}
