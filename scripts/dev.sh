#!/usr/bin/env bash
# Local dev launcher: PostgreSQL (Podman) + backend API + Angular frontend, one command.
# Ctrl+C tears everything down cleanly. Requires: podman, dotnet, node 22.
#
#   ./scripts/dev.sh
#     → API   http://localhost:5000  (health: /health)
#     → Web   http://localhost:4200  (proxies /api to the backend)
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

export ConnectionStrings__storeit="Host=localhost;Port=5432;Database=storeit;Username=storeit;Password=storeit"

# node@22 is keg-only on this setup; prepend if present, otherwise rely on PATH.
if [ -d /opt/homebrew/opt/node@22/bin ]; then
  export PATH="/opt/homebrew/opt/node@22/bin:$PATH"
fi

backend_pid=""
frontend_pid=""

cleanup() {
  echo ""
  echo "› shutting down…"
  [ -n "$frontend_pid" ] && kill "$frontend_pid" 2>/dev/null || true
  [ -n "$backend_pid" ] && kill "$backend_pid" 2>/dev/null || true
  podman compose down 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "› starting PostgreSQL (podman compose)…"
podman compose up -d

echo "› applying migrations…"
# dotnet-ef is a local tool (backend/dotnet-tools.json) — restore and run from backend/
(cd backend \
  && dotnet tool restore \
  && dotnet ef database update \
       --project src/StoreIt.Infrastructure \
       --startup-project src/StoreIt.Api)

echo "› starting backend on http://localhost:5000 …"
dotnet run --project backend/src/StoreIt.Api --no-launch-profile --urls http://localhost:5000 &
backend_pid=$!

echo "› waiting for backend health…"
for _ in $(seq 1 30); do
  if curl -sf http://localhost:5000/health >/dev/null 2>&1; then
    echo "  backend up"
    break
  fi
  sleep 1
done

if [ ! -d frontend/node_modules ]; then
  echo "› installing frontend dependencies (first run)…"
  (cd frontend && npm ci)
fi

echo "› starting frontend on http://localhost:4200 …"
(cd frontend && npm start) &
frontend_pid=$!

echo ""
echo "› store-it is running — API :5000 · Web :4200 — press Ctrl+C to stop"
wait
