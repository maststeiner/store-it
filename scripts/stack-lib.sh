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

# Refuse Compose v1. It predates the Compose Spec: it rejects the top-level `name:` outright
# and, worse, it does not know `depends_on: condition: service_completed_successfully`
# (added to the spec in 2021). Silently accepting it would mean the API starts before
# migrations finish — a guarantee this stack is built on (AC-05a). Failing loudly is the
# lesser evil. Note podman may delegate to whatever `docker-compose` binary it finds.
require_compose_spec_support() {
  local version_output
  version_output="$("${compose[@]}" version 2>&1 || true)"
  case "$version_output" in
    *"version 1."* | *"version v1."*)
      echo "error: your compose provider is Docker Compose v1, which cannot run this stack." >&2
      echo "       it rejects the top-level 'name:' and ignores" >&2
      echo "       'depends_on: condition: service_completed_successfully', so the API would" >&2
      echo "       start before migrations finish." >&2
      echo >&2
      echo "       provider reported: ${version_output%%$'\n'*}" >&2
      echo "       fix: install Docker Compose v2 or podman-compose, and point podman at it" >&2
      echo "            (containers.conf [engine] compose_providers, or PODMAN_COMPOSE_PROVIDER)." >&2
      echo "       check with: ${compose[*]} version" >&2
      exit 1
      ;;
  esac
}

# The compose project name — must match `name:` in compose.stack.yaml, because the
# teardown finds this stack's containers, volumes and images through it.
readonly STACK_PROJECT=storeit-stack
readonly STACK_FILE=compose.stack.yaml
