#!/usr/bin/env bash
# Brings up the full local stack: PostgreSQL, migrations, API, web (SPEC-004).
# Podman is used when available, Docker otherwise.
#
#   ./scripts/stack-up.sh            build if needed, then start
#   ./scripts/stack-up.sh --rebuild  force a rebuild of both images first
#
# Tear it down again with ./scripts/stack-down.sh
set -euo pipefail

# shellcheck source=scripts/stack-lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/stack-lib.sh"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

detect_engine
echo "container engine: $engine"

if [[ ! -f .env ]]; then
  echo "error: .env is missing." >&2
  echo "       cp .env.example .env   # then fill it in, at least POSTGRES_PASSWORD" >&2
  exit 1
fi

build_args=()
if [[ "${1:-}" == "--rebuild" ]]; then
  build_args+=(--no-cache)
  echo "rebuilding images from scratch…"
fi

# Build explicitly rather than relying on `up --build`: podman-compose does not always
# support the flag, and a separate step makes a build failure obvious.
"${compose[@]}" -f "$STACK_FILE" build "${build_args[@]}"

# `up` runs the migration service to completion first — that ordering lives in
# compose.stack.yaml, not here, so a plain `compose up` behaves identically.
"${compose[@]}" -f "$STACK_FILE" up --detach

port="$(grep -E '^STOREIT_WEB_PORT=' .env | cut -d= -f2 || true)"
port="${port:-8080}"

echo
echo "stack is up → http://localhost:${port}"
echo "  logs:  ${compose[*]} -f $STACK_FILE logs -f"
echo "  down:  ./scripts/stack-down.sh"
