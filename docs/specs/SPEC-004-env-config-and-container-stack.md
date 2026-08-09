# Spec: Environment-driven configuration and a one-command container stack

> **Status:** Frozen (Gate 1) — approved by Marcel Steiner, 2026-08-09
> **Sprint:** 2026-S32
> **Author:** Claude Opus 5 (developer agent), from Marcel Steiner's request
> **Last updated:** 2026-08-09

---

## User Story

As a **developer or operator** I want **the whole application configured from the
environment and started with a single container-stack command** so that **I can bring up
backend, frontend and database reproducibly on any machine with one command**.

---

## Context: what already exists

Recorded so the spec is judged against reality, not a blank slate.

| Already true | Evidence |
|---|---|
| The DB connection string is read from the environment | `AddInfrastructure` resolves `ConnectionStrings:storeit` and throws a message naming `ConnectionStrings__storeit`; `DesignTimeDbContextFactory` reads the same variable directly |
| CI already runs the full stack from environment config | job `1b · End-to-end (full stack)` sets `ConnectionStrings__storeit` and starts API + `ng serve` |
| OIDC settings are already configuration-driven and optional | `AuthenticationSetup` registers a provider only when both `ClientId` and `Authority` are non-empty (`Authentication__Google__ClientId`, … map by .NET's default env-var binding) |
| Postgres already runs in a container, podman-aware | `compose.yaml`, image `docker.io/library/postgres:18-alpine` (fully-qualified — required by podman), loopback-only port binding |
| No **OIDC** secret is committed | `appsettings.json` carries empty `ClientId`/`ClientSecret` placeholders only. Note the exception: `compose.yaml` does commit `POSTGRES_PASSWORD: storeit` for the local dev database — a real credential, though loopback-bound and worthless outside a laptop |

| Missing | Consequence |
|---|---|
| **No Dockerfile exists anywhere in the repo** | Backend and frontend can only run natively today |
| Nothing routes `/api` and `/auth` in a container network | The frontend relies on `frontend/proxy.conf.json` → `http://localhost:5000`, a dev-server-only mechanism |
| Migrations are applied by `scripts/dev.sh`, by CI, or by hand | `dev.sh` already runs `dotnet ef database update`; what is missing is the equivalent **inside a container stack**, not automated migration as such |
| No documented inventory of the environment contract | Variables are discoverable only by reading code |
| Missing config fails **late** | `AddInfrastructure` throws when the DbContext is first created — i.e. on the first request, not at startup |

---

## Decisions taken so far (Marcel, 2026-08-08 / 2026-08-09)

These answer individual questions; they are **not** a freeze of the whole spec.

1. **Frontend is served production-like**: built Angular assets served by nginx, which
   reverse-proxies `/api` and `/auth` to the backend. One entry URL, no CORS special case,
   and the images can later feed ADR-005 (#17). No hot reload.
2. **Real OIDC via environment variables** — not the `dev-login` shortcut.
3. **Migrations run as a separate one-shot service**, 12-factor admin process, mirroring
   what CI does — reaffirmed 2026-08-09: they run automatically as part of bringing the
   stack up, never as a step inside the backend's own startup.
4. **Option A for the cookie/TLS question** (2026-08-09): the stack runs with
   `ASPNETCORE_ENVIRONMENT=Production` over `http://localhost`, relying on the browser's
   localhost exception for the `Secure` session cookie. No TLS, no forwarded-header work,
   and `dev-login` stays unmapped.
5. **The stack is a testing tool, not a deployment artifact** (2026-08-09, Marcel: *"da es
   nur zum lokalen testen gedacht ist"*). This is why TLS, registry publishing and
   non-localhost hostnames are out of scope, and why loopback-only binding is a requirement
   (AC-11) rather than a nicety. The deployment ambition was removed from the user story —
   it contradicted this decision.
6. **The stack lives in its own compose file** (2026-08-09). `compose.yaml` keeps starting
   Postgres alone, so today's native workflow (`scripts/dev.sh`, `dotnet run`, `ng serve`)
   is untouched by this change.
7. **CI stays exactly as it is** (2026-08-09, revised the same day). Job
   `1b · End-to-end (full stack)` keeps starting backend and `ng serve` natively with
   `dev-login`. Briefly considered and dropped: running the stack in CI would have forced
   the login question (see the note below), and the point of this stack is a simple local
   tool. Consequence accepted knowingly: **nothing in CI exercises the stack**, so it can
   rot unnoticed until someone runs it.
8. **Two scripts** (2026-08-09): one brings the stack up, one tears it down *and* cleans up
   the images. Podman first, matching the existing `scripts/dev.sh`.

---

## Amendments (post-freeze)

Corrections found by the automated review after the G1 freeze. None change scope or any
acceptance criterion; they fix statements that were wrong or under-specified when frozen.

| # | Date | Change |
|---|------|--------|
| A1 | 2026-08-09 | "No secret is committed" was false: `compose.yaml` commits `POSTGRES_PASSWORD`. Narrowed to OIDC secrets, with the exception named. |
| A2 | 2026-08-09 | "Migrations are applied by hand or by CI" was wrong: `scripts/dev.sh` already applies them. The gap is a migration step *inside a container stack*. |
| A3 | 2026-08-09 | Startup order, restart policy and the environment contract were described in prose only; they are now stated precisely (see below), because they are the parts an implementation can silently get wrong. |
| A4 | 2026-08-09 | Option B was still listed as an alternative although option A had been chosen and the technical constraints forbid the new code B would need. Reduced to a note. |
| A5 | 2026-08-09 | Added EC-11 (dual-stack loopback), found by running the stack: `localhost` resolved to `::1` inside the container and the health check failed with "connection refused" while the service was fine. |
| A6 | 2026-08-09 | **`ASPNETCORE_ENVIRONMENT` is no longer configurable for this stack.** Decision 4 requires `Production`, but the environment contract allowed an override — and in `Development` the API maps `POST /auth/dev-login`, which issues a session with no credential. A one-line change in an untracked `.env` was too easy a way to drop the security contract. The value is fixed in compose; a Development run is what `scripts/dev.sh` is for. |
| A7 | 2026-08-09 | **Project isolation must be explicit, not implied by a separate file.** The stack declares `name: storeit-stack` and a `stack-pgdata` volume, but `name:` is not honoured by every compose implementation, so both scripts now pass `-p storeit-stack`. Verified: a teardown with a volume named like the dev database present leaves it untouched. |

### A3 — the parts that must be exact

**Startup order.** `postgres` must pass its health check; then `migrate` must run **to
completion with exit code 0**; only then does `backend` start, and `web` waits for
`backend` to be healthy. The ordering is expressed with compose `depends_on` conditions
(`service_healthy`, `service_completed_successfully`), not in the scripts, so a plain
`compose up` inherits the same guarantees.

**Failure is terminal, not a loop.** No service carries a restart policy that would retry a
configuration error: a failed migration or a missing connection string stops the stack with
a non-zero result instead of restart-looping.

**Environment contract.** `.env.example` lists every variable with its classification:

| Variable | Required | Secret | Default |
|---|---|---|---|
| `POSTGRES_DB`, `POSTGRES_USER` | no | no | `storeit` |
| `POSTGRES_PASSWORD` | **yes** | local-only credential | none — absent must fail loudly |
| `STOREIT_WEB_PORT` | no | no | `8080` (unprivileged, so rootless podman works) |
| `ASPNETCORE_ENVIRONMENT` | — | no | **fixed to `Production` in compose; deliberately not in `.env.example`** (A6) — in `Development` the API maps `/auth/dev-login`, which issues a session with no credential |
| `Authentication__{Google,Microsoft}__ClientId`/`ClientSecret`/`Authority` | no | **yes** | empty; sign-in is then unavailable while the stack still runs (AC-10) |

**The one-command contract.** `./scripts/stack-up.sh` serves the application at
`http://localhost:${STOREIT_WEB_PORT}`; `./scripts/stack-down.sh` removes containers,
volumes and the images this stack built.

Running compose directly works too, but **the project name must be passed every time** —
otherwise an implementation that ignores `name:` loses the isolation of A7 and a teardown
can reach the dev database:

```bash
docker compose -p storeit-stack -f compose.stack.yaml up -d
docker compose -p storeit-stack -f compose.stack.yaml down --volumes
# …and identically with `podman compose`.
```

---

## Why the E2E suite stays out of this

The Playwright suite authenticates with `POST /auth/dev-login`, which `Program.cs` maps
**only** in `Development`. The stack runs `Production` with real OIDC (decisions 2 and 4), so
those tests could not authenticate against it — there is no OIDC provider to log into in an
automated run. Rather than weaken the tests or complicate the stack, the two stay separate:
the E2E suite keeps its native, `Development`, `dev-login` setup, and the stack stays a
`Production`-like local tool.

---

## Acceptance Criteria (EARS Notation)

### Configuration from the environment

- [ ] AC-01: WHEN any configuration value (database, authentication, logging, URLs) is
      supplied as an environment variable THE system SHALL use it without a rebuild and
      without editing a committed file.
- [ ] AC-02: WHEN the backend starts **without** a usable database connection string THE
      system SHALL fail at startup with a message naming the missing variable — not on the
      first request, so a broken container exits instead of serving errors.
- [ ] AC-03: WHEN the repository is inspected THE system SHALL contain no secret value; a
      committed `.env.example` SHALL list every variable the stack reads, with safe
      placeholders, and the real `.env` SHALL be git-ignored.

### The container stack

- [ ] AC-04: WHEN a developer runs one command in a clean checkout with a filled `.env`
      THE system SHALL start database, migrations, backend and frontend, and serve the
      working application on a single URL.
- [ ] AC-05: WHEN the stack is brought up THE system SHALL apply database migrations
      **automatically, on every start**, in a dedicated one-shot service that runs to
      completion before the backend starts. The backend SHALL NOT migrate on startup, and no
      manual step SHALL be required.
- [ ] AC-05a (Error): WHEN the migration service fails THE system SHALL NOT start the
      backend, and the stack SHALL surface a non-zero result — a half-migrated database must
      not be served.
- [ ] AC-06: WHEN the backend is not yet healthy THE system SHALL keep dependent services
      waiting rather than starting them against a dead dependency (health checks, ordered
      startup).
- [ ] AC-07: WHEN the browser requests `/api/...` or `/auth/...` from the frontend origin
      THE system SHALL route it to the backend without CORS configuration, so the SPEC-003
      cookie session and the `X-XSRF-TOKEN` double-submit contract work unchanged.
- [ ] AC-08: WHEN the same commands are run with `podman compose` instead of
      `docker compose` THE system SHALL behave identically.

### Failure behaviour

- [ ] AC-09 (Error): WHEN a required environment variable is missing THE system SHALL exit
      non-zero with a message naming that variable, instead of restart-looping with a stack
      trace.
- [ ] AC-10 (Error): WHEN OIDC credentials are absent or wrong THE system SHALL still start
      and serve the app, and report the failure at login time — the existing behaviour of
      `AuthenticationSetup`, which must not regress.

### Scripts and coexistence

- [ ] AC-13: WHEN a developer runs the start script THE system SHALL bring the whole stack
      up with one command, and WHEN they run the teardown script THE system SHALL stop the
      stack, remove its containers and volumes, and remove the images it built — leaving no
      leftovers behind.
- [ ] AC-14: WHEN either script runs THE system SHALL use `podman` where available and
      `docker` otherwise, and SHALL fail with a clear message if neither is installed.
- [ ] AC-15: WHEN `docker compose up` / `podman compose up` is run against the existing
      `compose.yaml` THE system SHALL still start Postgres alone, exactly as before — the
      native development workflow must not change.

---

## Edge Cases

- **EC-01 — Secure cookie over plain HTTP.** `AuthenticationSetup` sets
  `Cookie.SecurePolicy = Always` outside `Development`. A production-like stack on
  `http://localhost:<port>` therefore relies on browsers treating `localhost` as a
  trustworthy origin. **Resolved by decision 4 (option A) — see below.**
- **EC-02 — OIDC redirect URI behind a reverse proxy.** The callback URL is derived from
  the incoming request. Behind nginx this only stays correct if the original `Host` is
  forwarded; the moment TLS terminates at nginx, the app also needs forwarded-header
  handling, which `Program.cs` does **not** configure today.
- **EC-03 — Redirect URI registration.** Google and Microsoft only accept redirect URIs
  registered in their console; the stack's URL must match exactly, including port.
- **EC-04 — Stale database volume.** An existing `pgdata` volume from an older schema must
  produce a clear migration failure, not silent misbehaviour.
- **EC-05 — Port already in use** (5432 from the existing dev compose, or the web port).
- **EC-06 — Rootless podman**: ports below 1024 are unavailable; SELinux hosts need `:z`
  on bind mounts.
- **EC-07 — Apple Silicon**: images must resolve for `arm64`, or the stack is x86-only.
- **EC-11 — Dual-stack loopback.** `localhost` may resolve to `::1` before `127.0.0.1`. A
  service bound to IPv4 only is then unreachable under a name that looks correct. Observed
  in practice while building this: the web container's health check failed with "connection
  refused" against `http://localhost:8080` although nginx was serving fine. Health checks
  must therefore address `127.0.0.1` explicitly, and the documented entry URL must state the
  IPv4 fallback.
- **EC-08 — The teardown script deletes images.** It must remove only what this stack built,
  never unrelated local images — a destructive script that over-reaches is worse than none.

---

## Resolved: the cookie/TLS question (was open at draft time)

Choice 2 (real OIDC) collided with EC-01/EC-02. **Option A was chosen**, on the grounds that
the stack is a local testing tool:

| Option | What it means | Cost |
|---|---|---|
| **A — `Production` over `http://localhost`** | Stack runs with `ASPNETCORE_ENVIRONMENT=Production`, so `dev-login` is **not** mapped and cookies are `Secure`. Works because browsers treat `http://localhost` as trustworthy. `RequireHttpsMetadata=true` is unproblematic — provider metadata is fetched from Google/Microsoft over HTTPS anyway. | None beyond care; fails if the stack is ever opened on a non-localhost hostname |
| **C — `Development`** | Relaxed cookie and metadata policy, simplest to get working. | **Exposes `POST /auth/dev-login`**, which SPEC-003 marks "never reachable in Staging or Production" — acceptable for a laptop, not for anything shared |

Option B (TLS terminating in nginx) was dropped rather than left standing: it would need
forwarded-header handling in `Program.cs`, which the technical constraints of this spec
rule out. It remains the right move when the images are aimed at a real deployment
(ADR-005 / #17), and would be its own spec.

**Chosen: A.** It honours the "real OIDC, no dev shortcut" decision, needs no new
application code, and keeps the stack honest about being a localhost tool. B is the right
follow-up when the images are aimed at a real deployment (ADR-005 / #17).

Two consequences that follow from A and are therefore binding:

- [ ] AC-11: WHEN the stack is started THE system SHALL bind its published ports to
      `127.0.0.1` only — on any other interface the `Secure` cookie stops working and real
      OIDC credentials would be exposed on the network.
- [ ] AC-12: WHEN the stack's documentation is read THE system SHALL state that it is a
      local testing tool and not a deployment artifact, so nobody promotes it by accident.

---

## Out of Scope

- Kubernetes manifests and deployment (ADR-005, issue #17). The stack is a testing tool;
  whether its images are later reusable for hosting is explicitly **not** an argument this
  spec makes, and no requirement here follows from it.
- Publishing images to a registry, and image signing/provenance.
- Secrets management beyond environment variables (no Vault, no sealed secrets).
- Production TLS certificates and hostnames.
- Hot-reload development workflow — explicitly traded away by decision 1.
- Replacing the native `dotnet run` / `ng serve` workflow; it stays as-is.

---

## Technical Constraints (from Architect Agent)


These were confirmed by the implementation (#83), which is why the boxes are ticked rather
than left pending — the note about the architect persona applied to the draft.

- [x] Layering: no change. New artifacts are infrastructure files (Dockerfiles, nginx
      config, compose), plus a startup-validation touch in the composition root for AC-02.
      Architecture tests pass.
- [x] Dependencies: no NuGet or npm package added; new base images only.
- [x] Image names stay fully qualified (`docker.io/library/...`) — podman does not
      assume a default registry (AC-08).
- [x] The build-time OpenAPI generation keeps working without a database — the lazy
      connection-string resolution exists for exactly that reason, so AC-02's fail-fast must
      not reintroduce a database requirement at build time.
- [x] ADR required: **no** for the stack itself; it must not pre-empt ADR-005 (#17).
- [x] New artifacts: `backend/Dockerfile`, `frontend/Dockerfile`, an nginx site config,
      `compose.stack.yaml`, `.env.example`, `.dockerignore` files, and two scripts under
      `scripts/` following the conventions of the existing `scripts/dev.sh` (bash,
      `set -euo pipefail`, repo-root resolution, podman first).
- [x] `compose.yaml` must not gain services (AC-15) — it is unchanged.
- [x] The ordering is expressed in compose itself, not in a script: the backend depends on
      the migration service having *completed successfully*, and on Postgres being healthy —
      so `up` alone is sufficient and the guarantee survives someone starting the stack
      without the scripts.

---

## Verification

<!-- Filled in by QA Agent -->

| AC | Test | Status |
|----|------|--------|
| AC-01 | ⬜ | ⬜ |
| AC-02 | ⬜ | ⬜ |
| AC-03 | ⬜ | ⬜ |
| AC-04 | ⬜ | ⬜ |
| AC-05 | ⬜ | ⬜ |
| AC-05a | ⬜ | ⬜ |
| AC-06 | ⬜ | ⬜ |
| AC-07 | ⬜ | ⬜ |
| AC-08 | ⬜ | ⬜ |
| AC-09 | ⬜ | ⬜ |
| AC-10 | ⬜ | ⬜ |
| AC-11 | ⬜ | ⬜ |
| AC-12 | ⬜ | ⬜ |
| AC-13 | ⬜ | ⬜ |
| AC-14 | ⬜ | ⬜ |
| AC-15 | ⬜ | ⬜ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ | 2026-08-09 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
