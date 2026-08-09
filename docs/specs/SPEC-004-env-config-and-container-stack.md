# Spec: Environment-driven configuration and a one-command container stack

> **Status:** Frozen (Gate 1) — decided by Marcel Steiner, 2026-08-09
> **Sprint:** 2026-S32
> **Author:** Claude Opus 5 (developer agent), from Marcel Steiner's request
> **Last updated:** 2026-08-09

---

## User Story

As a **developer or operator** I want **the whole application configured from the
environment and started with a single container-stack command** so that **I can bring up
backend, frontend and database reproducibly on any machine — and later deploy the same
images instead of a differently-built artifact**.

---

## Context: what already exists

Recorded so the spec is judged against reality, not a blank slate.

| Already true | Evidence |
|---|---|
| The DB connection string is read from the environment | `AddInfrastructure` resolves `ConnectionStrings:storeit` and throws a message naming `ConnectionStrings__storeit`; `DesignTimeDbContextFactory` reads the same variable directly |
| CI already runs the full stack from environment config | job `1b · End-to-end (full stack)` sets `ConnectionStrings__storeit` and starts API + `ng serve` |
| OIDC settings are already configuration-driven and optional | `AuthenticationSetup` registers a provider only when both `ClientId` and `Authority` are non-empty (`Authentication__Google__ClientId`, … map by .NET's default env-var binding) |
| Postgres already runs in a container, podman-aware | `compose.yaml`, image `docker.io/library/postgres:18-alpine` (fully-qualified — required by podman), loopback-only port binding |
| No secret is committed | `appsettings.json` carries empty `ClientId`/`ClientSecret` placeholders only |

| Missing | Consequence |
|---|---|
| **No Dockerfile exists anywhere in the repo** | Backend and frontend can only run natively today |
| Nothing routes `/api` and `/auth` in a container network | The frontend relies on `frontend/proxy.conf.json` → `http://localhost:5000`, a dev-server-only mechanism |
| Migrations are applied by hand or by CI | No admin-process step inside a stack |
| No documented inventory of the environment contract | Variables are discoverable only by reading code |
| Missing config fails **late** | `AddInfrastructure` throws when the DbContext is first created — i.e. on the first request, not at startup |

---

## Decisions taken at G1 (Marcel, 2026-08-08 / 2026-08-09)

1. **Frontend is served production-like**: built Angular assets served by nginx, which
   reverse-proxies `/api` and `/auth` to the backend. One entry URL, no CORS special case,
   and the images can later feed ADR-005 (#17). No hot reload.
2. **Real OIDC via environment variables** — not the `dev-login` shortcut.
3. **Migrations run as a separate one-shot service**, 12-factor admin process, mirroring
   what CI does.
4. **Option A for the cookie/TLS question** (2026-08-09): the stack runs with
   `ASPNETCORE_ENVIRONMENT=Production` over `http://localhost`, relying on the browser's
   localhost exception for the `Secure` session cookie. No TLS, no forwarded-header work,
   and `dev-login` stays unmapped.
5. **The stack is for local testing only** (2026-08-09, Marcel: *"da es nur zum lokalen
   testen gedacht ist"*). This is a scope decision, not a detail: it is why TLS, registry
   publishing and non-localhost hostnames are out of scope, and why binding to loopback is a
   requirement rather than a nicety.

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
- [ ] AC-05: WHEN the stack starts THE system SHALL apply database migrations in a
      dedicated one-shot service that runs to completion **before** the backend starts.
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

---

## Resolved: the cookie/TLS question (was open at draft time)

Choice 2 (real OIDC) collided with EC-01/EC-02. **Option A was chosen**, on the grounds that
the stack is a local testing tool:

| Option | What it means | Cost |
|---|---|---|
| **A — `Production` over `http://localhost`** | Stack runs with `ASPNETCORE_ENVIRONMENT=Production`, so `dev-login` is **not** mapped and cookies are `Secure`. Works because browsers treat `http://localhost` as trustworthy. `RequireHttpsMetadata=true` is unproblematic — provider metadata is fetched from Google/Microsoft over HTTPS anyway. | None beyond care; fails if the stack is ever opened on a non-localhost hostname |
| **B — `Production` with TLS in nginx** | Self-signed / mkcert certificate in the stack, `https://localhost`. Closest to production, no reliance on the localhost exception. | Certificate handling in compose, plus forwarded-header configuration in `Program.cs` (new code, EC-02) |
| **C — `Development`** | Relaxed cookie and metadata policy, simplest to get working. | **Exposes `POST /auth/dev-login`**, which SPEC-003 marks "never reachable in Staging or Production" — acceptable for a laptop, not for anything shared |

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

- Kubernetes manifests and deployment (ADR-005, issue #17) — this stack is the local
  runtime, not the hosting decision, though its images are meant to remain reusable.
- Publishing images to a registry, and image signing/provenance.
- Secrets management beyond environment variables (no Vault, no sealed secrets).
- Production TLS certificates and hostnames.
- Hot-reload development workflow — explicitly traded away by decision 1.
- Replacing the native `dotnet run` / `ng serve` workflow; it stays as-is.

---

## Technical Constraints (from Architect Agent)

<!-- To be confirmed by the architect persona after Gate 1 -->

- [ ] Layering: no change. New artifacts are infrastructure files (Dockerfiles, nginx
      config, compose), plus at most a startup-validation touch in the composition root for
      AC-02.
- [ ] Dependencies: no new NuGet or npm package expected; new base images only.
- [ ] Image names must stay fully qualified (`docker.io/library/...`) — podman does not
      assume a default registry (AC-08).
- [ ] The build-time OpenAPI generation must keep working without a database — the lazy
      connection-string resolution exists for exactly that reason, so AC-02's fail-fast must
      not reintroduce a database requirement at build time.
- [ ] ADR required: **no** for the stack itself; it feeds, and must not pre-empt, ADR-005
      (#17).

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
| AC-06 | ⬜ | ⬜ |
| AC-07 | ⬜ | ⬜ |
| AC-08 | ⬜ | ⬜ |
| AC-09 | ⬜ | ⬜ |
| AC-10 | ⬜ | ⬜ |
| AC-11 | ⬜ | ⬜ |
| AC-12 | ⬜ | ⬜ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ | 2026-08-09 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
