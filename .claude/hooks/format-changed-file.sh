#!/bin/bash
# PostToolUse hook (Edit|Write): formats the touched file immediately.
# Keeps agent output format-clean without relying on agent discipline (SETUP §6).
# Always exits 0 — formatting problems surface in the CI format gate, not here.

file=$(python3 -c "import json,sys; print(json.load(sys.stdin).get('tool_input',{}).get('file_path',''))" 2>/dev/null)
[ -z "$file" ] || [ ! -f "$file" ] && exit 0

root="${CLAUDE_PROJECT_DIR:-$(pwd)}"

case "$file" in
    "$root"/backend/*.cs)
        (cd "$root/backend" && dotnet csharpier format "$file") >/dev/null 2>&1
        ;;
    "$root"/frontend/*.ts | "$root"/frontend/*.html | "$root"/frontend/*.scss | "$root"/frontend/*.json)
        if [ -d "$root/frontend/node_modules" ]; then
            (cd "$root/frontend" && npx prettier --write "$file") >/dev/null 2>&1
        fi
        ;;
esac

exit 0
