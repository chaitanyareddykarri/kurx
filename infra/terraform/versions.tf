# Kurx production infrastructure (AM10, D-104a).
#
# Target: a single ECS Fargate service behind an ALB, with Postgres, Redis, KMS-protected signing
# keys and Secrets Manager — i.e. exactly what the authentication platform needs and nothing more.
#
# ⚠ NEVER APPLIED. Validated with `terraform validate` against the real AWS provider schema, but no
# AWS account was available. Everything here is VERIFIED (syntax/schema) or VERIFIED BY REVIEW;
# nothing is runtime-verified. See D-104a.

terraform {
  required_version = ">= 1.9"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.70"
    }
  }

  # Remote state with locking. Deliberately not committed with real values: state contains resource
  # identifiers and, for some resources, secret material. Supply via `-backend-config`.
  backend "s3" {}
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "kurx"
      Environment = var.environment
      ManagedBy   = "terraform"
    }
  }
}
