#!/usr/bin/env bash
# Regenerate the checked-in API contract at docs/api/openapi.json (D-259).
#
# Run this whenever an endpoint, request body or response type changes. CI regenerates the same file
# and fails the build if it differs from what is committed, so a contract change that skips this step
# is caught at the PR rather than by a client blowing up at runtime.
#
# Requires: a reachable Postgres (the API applies migrations at boot and refuses to start without one)
# and `jq`. Uses docker compose so it needs no local .NET SDK.
set -euo pipefail

cd "$(dirname "$0")/.."

OUT=docs/api/openapi.json
# 5080 is the ONE dev API port (D-318). This script kept the pre-D-318 5001 and so polled a port
# nothing binds for 180 seconds before failing — on the one script a deferred contract regen depends on.
PORT="${KURX_API_PORT:-5080}"

echo "==> starting postgres + backend"
docker compose up -d --build backend

echo "==> waiting for the API to answer"
for _ in $(seq 1 90); do
  if curl -fsS "http://localhost:${PORT}/health" >/dev/null 2>&1; then break; fi
  sleep 2
done

command -v jq >/dev/null || { echo "error: jq is required and not installed." >&2; exit 1; }

echo "==> fetching the spec"
# Swagger is mapped only outside Production; docker-compose defaults ASPNETCORE_ENVIRONMENT to
# Development, so this is served. A production image deliberately does not expose it.
#
# Written to a temp file and moved into place only on success. `> "$OUT"` truncates the moment the
# shell sets the pipeline up — before curl or jq has run — so any failure here used to leave the
# committed contract EMPTY, and `set -e` cannot undo a redirection that already happened.
TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT
curl -fsS "http://localhost:${PORT}/swagger/v1/swagger.json" | jq -S . > "$TMP"
[ -s "$TMP" ] || { echo "error: the API returned an empty spec." >&2; exit 1; }
mv "$TMP" "$OUT"

echo "==> wrote $OUT ($(wc -l < "$OUT") lines)"
echo "    Review the diff and commit it with the change that caused it."
