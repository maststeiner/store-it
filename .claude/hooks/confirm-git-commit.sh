#!/usr/bin/env bash
# PreToolUse guardrail (Bash): every `git commit` requires explicit human
# sign-off. Returns an "ask" decision so Claude Code prompts before committing —
# the technically-enforced form of store-it's "confirm the message first" rule.
# Fails open — non-commit commands exit 0 and proceed normally.
set -euo pipefail
input="$(cat)"

# Match `commit` even behind git global options (git -C dir commit, git -c k=v commit, …).
if printf '%s' "$input" | grep -qE 'git([[:space:]]+[^[:space:]]+)*[[:space:]]+commit'; then
  cat <<'JSON'
{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"store-it requires explicit human sign-off on every commit. Review and confirm the commit message before it lands."}}
JSON
fi
exit 0
