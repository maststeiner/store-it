#!/usr/bin/env bash
# Shared bits for stack-up.sh / stack-down.sh (SPEC-004).
# Not executable on its own — sourced by both scripts.

# Podman first, Docker as fallback (AC-14). Both are used through their compose
# subcommand, so the rest of the scripts do not care which one answered.
detect_engine() {
  if command -v podman > /dev/null 2>&1 && podman compose version > /dev/null 2>&1; then
    engine=podman
    compose=(podman compose)
  elif command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then
    engine=docker
    compose=(docker compose)
  elif command -v podman-compose > /dev/null 2>&1; then
    engine=podman
    compose=(podman-compose)
  else
    echo "error: neither 'podman compose' nor 'docker compose' is available." >&2
    echo "       install podman (preferred) or docker, then run this script again." >&2
    exit 1
  fi
}

repo_root_from_script() {
  cd "$(dirname "${BASH_SOURCE[1]}")/.." && pwd
}

# The compose project name — must match `name:` in compose.stack.yaml, because the
# teardown finds this stack's containers, volumes and images through it.
readonly STACK_PROJECT=storeit-stack
readonly STACK_FILE=compose.stack.yaml
