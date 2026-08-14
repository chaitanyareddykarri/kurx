# Kurx infrastructure (Terraform)

Production infrastructure for the Kurx API and authentication platform (AM10, D-104a).

> ⚠ **Never applied.** This configuration passes `terraform fmt -check` and `terraform validate`
> against the real AWS provider schema, but **no AWS account was available** during development.
> Everything here is **VERIFIED** (syntax and provider schema) or **VERIFIED BY REVIEW** — nothing is
> runtime-verified. A first apply must be to **staging**, reviewed plan by plan.

---

## Why Terraform, and why ECS Fargate

**Terraform** over CDK: the repository had no existing IaC standard. Terraform's `validate` runs
without an AWS account, which makes it verifiable in CI on day one; CDK would require Node
bootstrapping and still not validate resource semantics offline.

**ECS Fargate** over EKS: one stateless HTTP service does not justify a Kubernetes control plane,
its upgrade cadence, or the operational knowledge it demands. Revisit if the platform grows into
many services with complex scheduling.

---

## Architecture

```
                Internet
                    │
                 [ WAF ]  managed rules + per-IP rate limit on /v1/auth/*
                    │
        ┌───────────▼───────────┐   public subnets (2 AZ)
        │          ALB          │   TLS 1.2+, HTTP→HTTPS redirect only
        └───────────┬───────────┘
                    │ :8080
        ┌───────────▼───────────┐   private subnets (2 AZ), no public IP
        │  ECS Fargate service  │   api + ADOT sidecar, autoscaled 2→10
        └───┬──────────┬────────┘
            │          │
   ┌────────▼───┐  ┌───▼─────────┐  isolated subnets (2 AZ), NO internet route
   │ RDS Postgres│  │ ElastiCache │
   │  Multi-AZ   │  │    Redis    │
   └─────────────┘  └─────────────┘

   VPC endpoints → KMS, Secrets Manager   (never traverse the public internet)
```

**Three network tiers**, because blast radius differs: the ALB is the only thing reachable from the
internet; tasks have egress but no ingress; **data stores have no internet route at all**, so a
compromised task cannot exfiltrate the database outward.

---

## What the authentication platform requires, and where it is satisfied

| Requirement | Where |
|---|---|
| KMS CMK wrapping private signing keys (D-102a) | `kms.tf` — key policy grants the task role exactly `GenerateDataKey`, `Decrypt`, `DescribeKey` |
| Secrets from Secrets Manager (D-101a) | `secrets.tf` + `SECRETS_PROVIDER=aws`, prefix `kurx/<env>/` |
| App refuses unprotected keys in production | `SIGNING_KEY_PROTECTION=kms` in the task definition |
| WebAuthn RP ID on a real HTTPS domain (D-097) | `WEBAUTHN_RP_ID=var.domain_name`; ALB is HTTPS-only |
| Asset Links reachable without redirect (D-103a) | HTTPS listener serves directly; port 80 redirects only |
| JWKS publicly readable (D-099) | Served by the API through the ALB |
| OTLP telemetry (D-100) | ADOT sidecar → CloudWatch/X-Ray |
| SignalR across instances (D-087) | ElastiCache Redis backplane; ALB idle timeout 120s |
| SMS via SNS (AM1) | Task-role `sns:Publish` restricted to `Protocol = sms` |

---

## Prerequisites

1. AWS account, and an **S3 bucket + DynamoDB table** for Terraform state.
2. **ACM certificate** for the domain, in the same region.
3. **ECR repository** with an image pushed under an immutable tag.
4. A registrable domain — the WebAuthn RP ID. **Choose once**: changing it invalidates every
   existing passkey.

---

## Deploying

```bash
terraform init \
  -backend-config="bucket=kurx-tfstate" \
  -backend-config="key=staging/terraform.tfstate" \
  -backend-config="region=ap-south-1" \
  -backend-config="dynamodb_table=kurx-tflock"

terraform plan -var-file=staging.tfvars -out=tfplan   # READ THE PLAN
terraform apply tfplan
```

Example `staging.tfvars`:

```hcl
environment     = "staging"
domain_name     = "staging.kurx.in"
certificate_arn = "arn:aws:acm:ap-south-1:123456789012:certificate/..."
container_image = "123456789012.dkr.ecr.ap-south-1.amazonaws.com/kurx-api:sha-abc1234"
```

### After the first apply

**Terraform creates secret containers but never their values** — a value set here would land in
Terraform state, which is exactly what Secrets Manager exists to avoid.

```bash
aws secretsmanager put-secret-value --secret-id kurx/staging/JWT_SECRET \
  --secret-string "$(openssl rand -base64 48)"
aws secretsmanager put-secret-value --secret-id kurx/staging/TICKET_HMAC_SECRET \
  --secret-string "$(openssl rand -base64 32)"
aws secretsmanager put-secret-value --secret-id kurx/staging/OTP_PEPPER \
  --secret-string "$(openssl rand -base64 32)"
# FCM_SERVICE_ACCOUNT_JSON, RAZORPAY_* from their consoles
```

> `JWT_SECRET` is still **required** during the staged ES256 migration (D-099): it validates legacy
> HS256 tokens minted before cut-over. Remove it only when no such token can still be live.

Then:

1. Point DNS at `alb_dns_name` (Route 53 alias, using `alb_zone_id`).
2. Serve `docs/mobile/assetlinks.json` at `/.well-known/assetlinks.json` — **200 directly, no
   redirect** (D-103a).
3. Subscribe an on-call address to `alerts_topic_arn`.
4. Verify `jwks_url` returns a well-formed key set.
5. Confirm the KMS health check passed in the task logs — it proves the IAM identity holds **both**
   `GenerateDataKey` and `Decrypt`, the pair most often half-granted.

---

## Cost note

The largest line items are **two NAT gateways** (~$65/mo) and **Multi-AZ RDS**. A single NAT halves
that cost but makes one AZ failure remove egress for the tasks in the other — which defeats running
in two AZs. For staging, `environment = "staging"` already disables Multi-AZ and Redis failover.

---

## Deliberately not included

- **Route 53 / ACM** — usually managed outside the service stack, and the certificate must exist
  before the ALB.
- **ECR repository** — belongs to the build pipeline, not the runtime stack.
- **CI/CD pipeline** — separate concern.
- **WAF IP allow-lists / geo rules** — no requirement stated; adding speculative rules to an
  auth path risks locking out real users.
- **EKS** — see rationale above.
