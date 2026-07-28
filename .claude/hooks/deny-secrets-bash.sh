#!/usr/bin/env bash
# PreToolUse guardrail (Bash): refuse commands that read secrets/credentials
# (.env files, SSH/AWS keys, .pgpass/.npmrc, or a full environment dump).
# Fails open — no match exits 0 and the normal permission flow decides.
set -euo pipefail
input="$(cat)"

if printf '%s' "$input" | grep -qiE '\.env([ ."&|;>)]|$)|\.ssh/|id_rsa|id_ed25519|id_ecdsa|\.pem([ ."&|;>)]|$)|\.aws/|\.pgpass|\.npmrc|(^|[[:space:]"|;&(])printenv|(^|[[:space:]"|;&(])(env|set)(["|;&>)]|$)|export[[:space:]]+-p'; then
  cat <<'JSON'
{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"Blocked by store-it guardrail: this command appears to read secrets/credentials (.env, SSH/AWS keys, .pgpass/.npmrc, or printenv). Agents must not access secrets."}}
JSON
fi
exit 0
