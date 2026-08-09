#!/usr/bin/env bash
# Tears the local stack down and cleans up after it (SPEC-004).
#
#   ./scripts/stack-down.sh          stop and remove containers, network and images
#   ./scripts/stack-down.sh --keep-data   … but keep the database volume
#
# It only ever touches what this stack created: containers and volumes carry the
# `storeit-stack` compose project, and the images are removed by their exact names. Your
# other images and the dev database from compose.yaml are not touched (SPEC-004 EC-08).
set -euo pipefail

# shellcheck source=scripts/stack-lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/stack-lib.sh"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

detect_engine
echo "container engine: $engine"
require_compose_spec_support

# Spelled out rather than accumulated into an array: macOS ships bash 3.2, where an empty
# array expansion under `set -u` is an error. This one is never empty, but the shape should
# not depend on that staying true.
if [[ "${1:-}" == "--keep-data" ]]; then
  echo "keeping the database volume"
  "${compose[@]}" -p "$STACK_PROJECT" -f "$STACK_FILE" down --remove-orphans
else
  "${compose[@]}" -p "$STACK_PROJECT" -f "$STACK_FILE" down --remove-orphans --volumes
fi

# Compose names built images "<project>_<service>" or "<project>-<service>" depending on
# the implementation, so try both spellings and stay quiet about the ones that are not
# there. Only these exact names — never a blanket prune, which would delete images this
# stack never built.
removed=0
for service in migrate backend web; do
  for image in "${STACK_PROJECT}_${service}" "${STACK_PROJECT}-${service}"; do
    if "$engine" image exists "$image" > /dev/null 2>&1 \
      || "$engine" image inspect "$image" > /dev/null 2>&1; then
      "$engine" image rm --force "$image" > /dev/null && removed=$((removed + 1))
      echo "removed image $image"
    fi
  done
done

if [[ $removed -eq 0 ]]; then
  echo "no stack images left to remove"
fi

echo
echo "stack is down."
echo "note: base images (dotnet, node, nginx, postgres) are kept — they are shared and"
echo "      expensive to pull again. Remove them yourself if you really want to."
