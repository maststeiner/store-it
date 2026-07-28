#!/usr/bin/env bash
# PreToolUse guardrail (Read|Edit|Write): refuse to touch files that look like
# secrets/credentials. Fails open — on no match it exits 0 and the normal
# permission flow decides. store-it enforces "no agent ever reads secrets".
set -euo pipefail
input="$(cat)"

if printf '%s' "$input" | grep -qiE '\.env([."/]|$)|\.pem"|\.p12"|\.pfx"|\.key"|id_rsa|id_ed25519|id_ecdsa|/\.ssh/|/\.aws/|\.pgpass|\.npmrc|secrets?\.(json|ya?ml|toml)|\.secrets\.'; then
  cat <<'JSON'
{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"Blocked by store-it guardrail: this path looks like a secret/credential file. Agents must never read or edit secrets — load them from the environment at runtime instead."}}
JSON
fi
exit 0
