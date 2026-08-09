# store-it

Digital pantry management: know what's in your pantry or freezer — and when it expires.

**store-it** lets you track the contents of a storage (pantry, freezer, cellar shelf): add items, remove what you consume, and see at a glance what expires soon. Storages can be shared across accounts, so a family or flat-share manages the same pantry together.

## Stack

| Layer | Technology |
|-------|------------|
| Backend | .NET (C#), API-first REST |
| Frontend | Angular (TypeScript) |
| Mobile | iPhone app (planned, consumes the same API) |
| Runtime | Cloud-native, Kubernetes |
| DevOps | GitHub + GitHub Actions |

## Repository structure

| Path | Purpose |
|------|---------|
| `backend/` | .NET solution (API) — *scaffold pending* |
| `frontend/` | Angular app — *scaffold pending* |
| `docs/` | Specs, arc42 architecture doc, ADRs, guidelines, agent logs |
| `.claude/` | KAIFe agent personas + permission tiers |
| `.github/workflows/` | CI: DoD gates (build/test, security+SBOM, quality, architecture, format) |

## Development process

This project follows the **KAIFe Framework (L4)** — AI-driven development with three non-negotiable human gates:

1. **G1 · Spec Freeze** — every work item starts as a spec in `docs/specs/`, frozen by a human
2. **G2 · Review** — automated + human code review on every PR
3. **G3 · DoD/Merge** — CI fully green; only a human merges

See `CLAUDE.md` for orchestration rules and `docs/SETUP.md` for the setup checklist.

## Run it locally

```bash
./scripts/dev.sh
```

Starts PostgreSQL (Podman), applies migrations, and launches the backend API
(http://localhost:5000) and the Angular frontend (http://localhost:4200).
Ctrl+C tears everything down. Prerequisites: `podman`, .NET 10 SDK, Node 22.

### …or everything in containers

Needs only `podman` (or Docker) — no .NET SDK, no Node. `curl` or `wget` is used to
confirm the stack answers before the script reports success; without either it starts the
stack and says it could not verify.

```bash
cp -n .env.example .env   # -n: never clobber an existing .env — it holds your secrets
./scripts/stack-up.sh     # → http://localhost:$STOREIT_WEB_PORT (8080 by default)
./scripts/stack-down.sh   # stops it and removes the images it built
```

The stack builds both images, applies migrations in a one-shot service, then starts the
API and an nginx that serves the built frontend and proxies `/api` and `/auth` — so
everything is one origin, exactly as the cookie session expects. Configuration comes from
`.env` only; see `.env.example` for every variable, and
[SPEC-004](docs/specs/SPEC-004-env-config-and-container-stack.md) for the reasoning.

**Which engine runs it.** Podman first, Docker as fallback — and the scripts require the
engine's *daemon* to answer, not merely its CLI to exist, because `docker compose version`
succeeds happily while nothing is running. The fallback is never silent: when podman is
installed but skipped, the script prints the reason (VM not started, no compose provider)
and the command that fixes it. Pin the choice to turn that fallback into an error:

```bash
STOREIT_ENGINE=podman ./scripts/stack-up.sh    # or =docker
```

> **This stack is a local testing tool, not a deployment artifact.** It binds to
> `127.0.0.1` only, serves plain HTTP, and relies on the browser trusting `localhost` for
> the `Secure` session cookie. Do not expose it on another interface and do not treat it as
> a template for hosting — that decision belongs to ADR-005 (#17).

Sign-in needs real OIDC credentials in `.env`, with the redirect URI registered at the
provider — and it must carry the same port as `STOREIT_WEB_PORT`, e.g.
`http://localhost:8080/auth/callback/google` for the default. Without credentials the
stack still starts and serves the app; only signing in is unavailable.

### Enabling sign-in

Until a provider has both a `ClientId` and an `Authority`, `/auth/login/{provider}` answers
`400 auth.provider.unconfigured` — an OIDC scheme is registered only for a fully configured
provider, so an empty `.env` can never break `/health`.

**The redirect URI follows the address bar, not the configuration.** Open the app at
`http://localhost:8080` and the callback is `http://localhost:8080/auth/callback/google`;
open it at `http://127.0.0.1:8080` and it is `http://127.0.0.1:8080/…`. Register the host
you actually type — or register both.

- **Google** — [Cloud console](https://console.cloud.google.com/apis/credentials) → *Create
  credentials* → *OAuth client ID* → **Web application**. Authorised redirect URI:
  `http://localhost:8080/auth/callback/google` (Google accepts `http` for loopback hosts).
  Put the id and secret in `Authentication__Google__ClientId` / `__ClientSecret`; the
  authority is already in `.env.example`.
- **Microsoft** — Entra ID → *App registrations* → *New registration*, redirect URI platform
  **Web**: `http://localhost:8080/auth/callback/microsoft`. Secret under *Certificates &
  secrets*. The authority has to be **tenant-specific**:
  `https://login.microsoftonline.com/<tenant-id-or-domain>/v2.0`. `common` and
  `organizations` publish the literal issuer
  `https://login.microsoftonline.com/{tenantid}/v2.0`, and nothing here resolves that
  template — token validation would fail.

Configuration is read when the container starts, so re-run `./scripts/stack-up.sh` after
editing `.env`. To see what the app really sends — the `redirect_uri` below is exactly what
the provider must have on file:

```bash
curl -si http://localhost:8080/auth/login/google | grep -i '^location'
```

One browser caveat: the session and the OIDC correlation cookies are `Secure`, so signing in
needs a browser that treats `http://localhost` as a secure context. Chrome, Edge and Firefox
do; Safari is stricter — if sign-in fails there with no visible error, try another browser.

`compose.yaml` stays the separate, native workflow: it starts PostgreSQL alone for
`./scripts/dev.sh`, and the container stack neither uses nor interferes with it — different
compose project, different volume.
