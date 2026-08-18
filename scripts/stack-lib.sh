#!/usr/bin/env bash
# Shared bits for stack-up.sh / stack-down.sh (SPEC-004).
# Not executable on its own — sourced by both scripts.

# The compose project name — must match `name:` in compose.stack.yaml, because the
# teardown finds this stack's containers, volumes and images through it.
readonly STACK_PROJECT=storeit-stack
readonly STACK_FILE=compose.stack.yaml

# Filled in by the engine checks below: why a candidate was rejected, and what to do about
# it. Kept in globals rather than returned, because the checks also have to set `compose`
# in the caller's shell — a $(…) capture would run them in a subshell and lose that.
reject_reason=""
reject_hint=""

# A compose subcommand is a client-side plugin: `docker compose version` answers happily
# while no daemon is running at all, and the failure then surfaces much later as a raw
# "failed to connect to the docker API" out of the middle of an image build. So ask the
# engine for a fact that only a live *server* can supply.
engine_answers() {
  case "$1" in
    docker) docker info --format '{{.ServerVersion}}' > /dev/null 2>&1 ;;
    podman) podman info --format '{{.Version.Version}}' > /dev/null 2>&1 ;;
    *) return 1 ;;
  esac
}

# Docker Desktop, Colima and Rancher Desktop each keep their socket under $HOME and rely on
# `docker context` to find it. A `default` context — or an inherited DOCKER_HOST — then
# points at /var/run/docker.sock, which on macOS often does not exist. Name the socket we
# can actually see instead of making the reader guess.
docker_socket_hint() {
  local sock
  for sock in "$HOME/.docker/run/docker.sock" "$HOME/.colima/default/docker.sock" \
    "$HOME/.rd/docker.sock"; do
    if [[ -S "$sock" ]]; then
      printf '%s\n' "a socket does exist at ${sock}, so the active docker context is not" \
        "the one serving it. Inspect and switch:" \
        "  docker context ls" \
        "  docker context use <name>" \
        "or, for this shell only:" \
        "  export DOCKER_HOST=unix://${sock}"
      return
    fi
  done
  if [[ -n "${DOCKER_HOST:-}" ]]; then
    printf '%s\n' "DOCKER_HOST is set to '${DOCKER_HOST}' — verify that it is right."
  fi
}

# Sets `engine` + `compose` and returns 0 when podman can run this stack; otherwise fills
# reject_reason/reject_hint. `podman compose` is only a shim: it delegates to whatever
# compose provider it finds, so "podman works" and "podman can compose" are two questions.
podman_usable() {
  reject_reason=""
  reject_hint=""
  if ! command -v podman > /dev/null 2>&1; then
    reject_reason="podman is not installed"
    reject_hint="macOS: brew install podman && podman machine init && podman machine start"
    return 1
  fi
  if ! engine_answers podman; then
    reject_reason="podman is installed but did not answer"
    reject_hint="$(printf '%s\n' "on macOS podman runs in a VM that has to be started:" \
      "  podman machine start          # 'podman machine init' first, if there is none" \
      "then check it with:  podman info")"
    return 1
  fi
  if podman compose version > /dev/null 2>&1; then
    engine=podman
    compose=(podman compose)
    return 0
  fi
  if command -v podman-compose > /dev/null 2>&1; then
    engine=podman
    compose=(podman-compose)
    return 0
  fi
  reject_reason="podman is running without a compose provider"
  reject_hint="$(printf '%s\n' \
    "'podman compose' delegates to an external provider and finds none. Install one:" \
    "  brew install docker-compose     # the v2 binary; podman picks it up automatically" \
    "  pipx install podman-compose     # or this, if you prefer it" \
    "then check it with:  podman compose version")"
  return 1
}

# Same contract as podman_usable, for docker.
docker_usable() {
  reject_reason=""
  reject_hint=""
  if ! command -v docker > /dev/null 2>&1; then
    reject_reason="docker is not installed"
    reject_hint="install Docker Desktop, or use podman"
    return 1
  fi
  if ! engine_answers docker; then
    reject_reason="docker is installed but its daemon did not answer"
    reject_hint="$(
      echo "start it and wait until it is running (macOS: open -a Docker)"
      docker_socket_hint
    )"
    return 1
  fi
  if ! docker compose version > /dev/null 2>&1; then
    reject_reason="docker is running without the compose v2 plugin"
    reject_hint="$(printf '%s\n' "install it:" "  brew install docker-compose" \
      "then check it with:  docker compose version")"
    return 1
  fi
  engine=docker
  compose=(docker compose)
  return 0
}

# Indent a possibly multi-line hint under an `error:`/`note:` line, on stderr.
print_hint() {
  local indent="${2:-       }"
  local line
  while IFS= read -r line; do
    [[ -n "$line" ]] && echo "${indent}${line}" >&2
  done <<< "$1"
}

# Podman first, docker as fallback (AC-14) — but never a *silent* fallback: a stack that
# quietly runs on the other engine is how "it must run under podman" turns into a surprise
# three commits later. Set STOREIT_ENGINE=podman (or =docker) to demand one and fail
# otherwise.
detect_engine() {
  case "${STOREIT_ENGINE:-}" in
    podman)
      podman_usable && return 0
      echo "error: STOREIT_ENGINE=podman was requested, but ${reject_reason}." >&2
      print_hint "$reject_hint"
      exit 1
      ;;
    docker)
      docker_usable && return 0
      echo "error: STOREIT_ENGINE=docker was requested, but ${reject_reason}." >&2
      print_hint "$reject_hint"
      exit 1
      ;;
    "") ;;
    *)
      echo "error: STOREIT_ENGINE must be 'podman' or 'docker', got '${STOREIT_ENGINE}'." >&2
      exit 1
      ;;
  esac

  podman_usable && return 0
  local podman_reason="$reject_reason"
  local podman_hint="$reject_hint"

  if docker_usable; then
    echo "note: ${podman_reason}, so this run uses docker instead." >&2
    print_hint "$podman_hint"
    echo "       set STOREIT_ENGINE=podman to make this an error rather than a fallback." >&2
    return 0
  fi

  echo "error: no usable container engine." >&2
  echo "       - ${podman_reason}" >&2
  print_hint "$podman_hint" "           "
  echo "       - ${reject_reason}" >&2
  print_hint "$reject_hint" "           "
  exit 1
}

# Refuse *Docker* Compose v1. It predates the Compose Spec: it rejects the top-level `name:`
# outright and, worse, it does not know `depends_on: condition: service_completed_successfully`
# (added to the spec in 2021). Silently accepting it would mean the API starts before
# migrations finish — a guarantee this stack is built on (AC-05a). Failing loudly is the
# lesser evil, and podman may delegate to whatever `docker-compose` binary it finds.
#
# The match has to name docker-compose, not just "version 1": `podman-compose` is also at
# 1.x and is a different implementation entirely, so a bare "version 1." test would refuse a
# perfectly good podman setup. That is why `podman compose` (a real Compose v2 provider) is
# preferred over the podman-compose fallback in podman_usable.
require_compose_spec_support() {
  local version_output
  version_output="$("${compose[@]}" version 2>&1 || true)"
  case "$version_output" in
    *"docker-compose version 1."* | *"Docker Compose version v1."* | *"docker compose version v1."*)
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
