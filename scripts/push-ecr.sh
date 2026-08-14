#!/usr/bin/env bash
# Build the API image for Fargate and push it to the existing ECR repository.
#
# Encodes three things that are not obvious and each fail in a confusing way:
#
#   1. --platform linux/amd64. The task definition sets no runtime_platform, so Fargate defaults to
#      X86_64. A native build on an Apple Silicon machine produces arm64 and the task then dies with
#      an exec-format error at runtime rather than at push time.
#   2. --provenance=false --sbom=false. BuildKit otherwise attaches provenance/SBOM attestations,
#      which turn the push into a manifest list. ECR rejects that here with a bare
#      "403 Forbidden" on the manifest HEAD, which reads like a permissions problem and is not one.
#   3. A fresh `get-login-password` on every run. The token is evaluated against the caller's policy
#      at issue time, so a token minted before an IAM change keeps failing with 403 until re-issued.
#
# The repository is created with IMMUTABLE tags: a tag can never be overwritten, only deleted. The
# tag is therefore derived from the commit and carries an explicit marker plus a timestamp when the
# tree is dirty, so a build that is not reproducible from a commit can never masquerade as one and
# two dirty builds cannot collide.
#
# No secrets are read or written here. The ECR token is ephemeral and never touches disk.
set -euo pipefail

AWS_REGION="${AWS_REGION:-ap-south-1}"
AWS_PROFILE="${AWS_PROFILE:-kurx-deploy}"
ECR_REPO="${ECR_REPO:-kurx-api}"
export AWS_PROFILE AWS_REGION

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
REGISTRY="${ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"

SHA="$(git rev-parse --short HEAD)"
if [ -n "$(git status --porcelain)" ]; then
  # Never label a dirty build with the bare commit tag: a rollback to that tag would deploy code
  # that was never committed. The timestamp keeps successive dirty builds from colliding.
  TAG="sha-${SHA}-dirty-$(date +%Y%m%d%H%M%S)"
  echo "WARNING: working tree is dirty; tagging ${TAG}" >&2
else
  TAG="sha-${SHA}"
fi
IMAGE="${REGISTRY}/${ECR_REPO}:${TAG}"

# Fail before spending a build on a tag the immutable repository will refuse.
if aws ecr describe-images --repository-name "$ECR_REPO" --image-ids "imageTag=${TAG}" \
     >/dev/null 2>&1; then
  echo "ERROR: ${TAG} already exists and the repository is immutable. Commit your changes or delete the tag." >&2
  exit 1
fi

echo "==> building ${IMAGE}"
docker buildx build \
  --platform linux/amd64 \
  --provenance=false --sbom=false \
  -f infra/Dockerfile.api \
  -t "$IMAGE" \
  --load \
  backend/

# Prove the architecture rather than trusting the flag: a wrong answer here is a crash loop in ECS
# that costs a deploy cycle to diagnose.
ARCH="$(docker image inspect "$IMAGE" --format '{{.Os}}/{{.Architecture}}')"
[ "$ARCH" = "linux/amd64" ] || { echo "ERROR: built ${ARCH}, expected linux/amd64" >&2; exit 1; }

echo "==> pushing to ECR"
aws ecr get-login-password --region "$AWS_REGION" \
  | docker login --username AWS --password-stdin "$REGISTRY" >/dev/null

docker push "$IMAGE"

echo
echo "pushed: ${IMAGE}"
echo "set this as container_image in infra/terraform/<env>.tfvars"
