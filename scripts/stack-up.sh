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
require_compose_spec_support

if [[ ! -f .env ]]; then
  echo "error: .env is missing." >&2
  echo "       cp .env.example .env   # then fill it in, at least POSTGRES_PASSWORD" >&2
  exit 1
fi

# Build explicitly rather than relying on `up --build`: podman-compose does not always
# support the flag, and a separate step makes a build failure obvious.
#
# Two spelled-out branches instead of an args array: macOS ships bash 3.2, where expanding
# an EMPTY array under `set -u` fails with "unbound variable". This script has to run there.
if [[ "${1:-}" == "--rebuild" ]]; then
  echo "rebuilding images from scratch…"
  "${compose[@]}" -p "$STACK_PROJECT" -f "$STACK_FILE" build --no-cache
else
  "${compose[@]}" -p "$STACK_PROJECT" -f "$STACK_FILE" build
fi

# `up` runs the migration service to completion first — that ordering lives in
# compose.stack.yaml, not here, so a plain `compose up` behaves identically.
"${compose[@]}" -p "$STACK_PROJECT" -f "$STACK_FILE" up --detach

port="$(grep -E '^STOREIT_WEB_PORT=' .env | cut -d= -f2 || true)"
port="${port:-8080}"

# `up --detach` returns once the containers are created; the web container may still be
# coming up. Wait for it to actually answer, so "stack is up" is not a lie.
# Probe with whatever is available. Without either tool the stack may still be perfectly
# fine, so refusing to start would be wrong — but so would claiming it is up. Say which.
if command -v curl > /dev/null 2>&1; then
  probe() { curl --silent --fail --max-time 2 "$1" > /dev/null 2>&1; }
elif command -v wget > /dev/null 2>&1; then
  probe() { wget --quiet --spider --timeout=2 "$1" > /dev/null 2>&1; }
else
  echo
  echo "note: neither curl nor wget found, so readiness was not verified."
  echo "stack started → http://localhost:${port} (check it yourself)"
  echo "  logs:  ${compose[*]} -p $STACK_PROJECT -f $STACK_FILE logs -f"
  exit 0
fi

retries="${STOREIT_WAIT_RETRIES:-60}"
ready=false
echo -n "waiting for http://127.0.0.1:${port} "
for _ in $(seq 1 "$retries"); do
  if probe "http://127.0.0.1:${port}/"; then
    ready=true
    echo "— ready"
    break
  fi
  echo -n "."
  sleep 2
done

if [[ "$ready" != true ]]; then
  echo
  echo "error: the stack did not answer on http://127.0.0.1:${port} in time." >&2
  echo "       the containers may still be running but unusable — inspect them with:" >&2
  echo "         ${compose[*]} -p $STACK_PROJECT -f $STACK_FILE ps" >&2
  echo "         ${compose[*]} -p $STACK_PROJECT -f $STACK_FILE logs" >&2
  exit 1
fi

echo
echo "stack is up → http://localhost:${port}"
echo "  logs:  ${compose[*]} -p $STACK_PROJECT -f $STACK_FILE logs -f"
echo "  down:  ./scripts/stack-down.sh"
