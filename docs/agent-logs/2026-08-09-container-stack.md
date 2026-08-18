# Agent Run Log: Environment-driven config and the container stack (SPEC-004)

> **Date:** 2026-08-09
> **Spec:** [SPEC-004](../specs/SPEC-004-env-config-and-container-stack.md) — frozen at G1 by
> Marcel Steiner, 2026-08-09
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `feature/spec-004-container-stack`

---

## Task

Implement SPEC-004: configuration entirely from the environment, and a one-command
container stack (database, migrations, API, web) that also works under podman, with a
start script and a teardown script that cleans up its images.

## What was built

| Artifact | Purpose |
|---|---|
| `backend/Dockerfile` | Two images from one build: the API runtime, and a `migrate` image that runs `dotnet ef database update` and exits. Same build ⇒ they cannot disagree about which migrations exist. |
| `frontend/Dockerfile` + `frontend/nginx.conf` | Angular built and served by nginx, which proxies `/api` and `/auth` to the API — one origin, so the SPEC-003 cookie session and CSRF pair work without CORS. |
| `compose.stack.yaml` | Own compose project (`storeit-stack`), so it cannot collide with the Postgres from `compose.yaml`. Ordering lives here: postgres healthy → migrate completed → backend healthy → web. |
| `.env.example`, `.gitignore` | The environment contract, with `.env` ignored. |
| `scripts/stack-up.sh`, `stack-down.sh`, `stack-lib.sh` | Podman first, docker fallback; teardown removes containers, volumes and only this stack's images. |
| `StartupConfigurationCheck` + `Program.cs` | Fail fast on missing configuration (AC-02/AC-09). |
| `README.md` | How to run it, and that it is a local testing tool (AC-12). |

## Three things that only showed up by running it

The stack was built and started for real, not just written. Each of these would have
shipped as a latent bug otherwise.

1. **`postgres:18` rejects the old volume mount.** Mounting at
   `/var/lib/postgresql/data` makes the container exit 1: since 18 these images keep the
   cluster in a version-specific subdirectory and expect a single mount at
   `/var/lib/postgresql`. Fixed here — **and the repository's existing `compose.yaml` had
   the same mount**, so the native dev database had been failing to start since the
   postgres 18 bump. Initially reported rather than changed, because AC-15 keeps that file
   out of scope; Marcel then asked for the fix to ride along in this PR, so it did.
   Verified afterwards: the dev database starts, a row written before a restart is still
   there after it, and the cluster now lives under `/var/lib/postgresql/18`.
2. **`localhost` is ambiguous inside the container.** The web health check failed with
   "connection refused" against `http://localhost:8080` while nginx was serving perfectly:
   `localhost` resolved to `::1`, nginx listens on IPv4. Health checks now address
   `127.0.0.1`. This is the same class of problem the review flagged for the host side, and
   it is now EC-11 in the spec.
3. **An unhandled exception does not stop the container.** The first implementation of
   AC-02 was an `IHostedService` that threw. The message appeared — "Hosting failed to
   start" — and the process then sat there: still running 90 seconds later, and 60 seconds
   later even when the throw was moved before the host was built. The check now *reports*
   rather than throws, and the entry point returns exit code 1. Measured result: **exit 1
   in under a second**, with a one-line message instead of a stack trace, which is what
   AC-09 actually asked for.

## What the first run on Marcel's machine found

The sandbox has docker and no podman, so engine *selection* had never actually been
exercised. Marcel's first run — podman is the engine he wants — produced
`container engine: docker`, followed by a raw
`failed to connect to the docker API at unix:///var/run/docker.sock`. Three defects behind
one symptom:

1. **Detection asked the wrong question.** `docker compose version` is a client-side
   plugin: it answers while no daemon is running at all. So an engine was chosen that could
   not build anything, and the failure surfaced as an API error out of the middle of the
   build. Detection now asks for a fact only a live server can supply
   (`docker info --format '{{.ServerVersion}}'`, `podman info …`), and an engine whose
   daemon is silent is skipped rather than selected.
2. **The fallback was silent.** Podman first, docker as fallback is the intended behaviour
   (AC-14) — but the script only ever printed the winner, never why podman lost. Now it
   names the reason (not installed / VM not started / no compose provider) and the command
   that fixes it, and `STOREIT_ENGINE=podman|docker` pins the engine so the fallback
   becomes an error instead.
3. **The Compose v1 guard would have refused podman-compose.** It matched `*version 1.*`,
   and `podman-compose` is itself at 1.x — a different implementation, not Docker Compose
   v1. The very setup the guard was meant to protect would have been rejected by it. The
   match now names docker-compose explicitly.

The error text for a stopped daemon also names the socket it can see
(`~/.docker/run/docker.sock`, Colima, Rancher Desktop) and the `docker context` command,
because on macOS that is the usual reason `/var/run/docker.sock` is missing.

## Design notes worth keeping

- **Why the check is not a hosted service.** Build-time OpenAPI generation legitimately has
  no database — that is why the connection string is resolved lazily. `GetDocument.Insider`
  does not merely build the host, it *starts* it, so hosted services run and a naive check
  broke `dotnet build`. The reliable discriminator is the entry assembly: during generation
  it is the tool, not `StoreIt.Api`.
- **Ordering belongs in compose, not the scripts**, so a plain `compose up` gets the same
  guarantees as the script (spec, technical constraints).
- **Nothing is published except the web port**, on `127.0.0.1` only (AC-11). Postgres is
  not published at all, which also keeps port 5432 free for the existing dev database.

## Verification

| AC | How it was verified | Result |
|----|---------------------|--------|
| AC-01 | Config supplied only via `.env`/compose environment | stack runs with no edited committed file |
| AC-02, AC-09 | `docker run` the API image with no connection string | **exit 1 in 0s**, single-line message naming `ConnectionStrings__storeit` |
| AC-03 | `.env.example` committed with placeholders; `.env` git-ignored | working tree clean with a filled `.env` present |
| AC-04 | `./scripts/stack-up.sh` from a torn-down state (images deleted) | rebuilt and served `http://127.0.0.1:8080` |
| AC-05, AC-05a | migrate logs | `Applying migration '20260804210136_InitialCreate'` → `Done.`, service exited before the API started |
| AC-06 | `compose ps` | postgres healthy → backend healthy → web healthy |
| AC-07 | Through the web origin: `/` → **200**, `/health` → **200**, `/api/v1/storages` → **401** | routing works and secure-by-default survives the proxy |
| AC-08 | Fully-qualified image names, unprivileged ports, engine detection in the scripts | **partially verified — see below** |
| AC-10 | Stack started with empty OIDC values | starts and serves; only sign-in unavailable — `GET /auth/login/google` → **400 `auth.provider.unconfigured`** |
| EC-03 | Sign-in wiring probed with dummy client ids for both providers | `/auth/login/{provider}` → **302** to the provider's authorize endpoint, `response_type=code`, PKCE `S256`, `scope=openid profile email`. The `redirect_uri` mirrors the browser's Host: `http://localhost:8080/auth/callback/…` via localhost, `http://127.0.0.1:8080/…` via the IP — so the registered URI has to match the host that is typed. Recorded in the README, because it is the failure nobody guesses |
| AC-11 | `compose config` | `host_ip: 127.0.0.1` |
| AC-12 | README section | states it is a local testing tool, not a deployment artifact |
| AC-13 | `./scripts/stack-down.sh` with an unrelated image tagged as a canary | containers, volume, network and exactly the 3 stack images removed; canary untouched |
| AC-14 | Engine detection, all branches (podman stubbed: missing / not answering / no compose provider; docker: unreachable daemon, bad `STOREIT_ENGINE`, both dead) | selection, fallback note and every error path verified; **the real podman binary is still not installed in the agent sandbox** |
| AC-15 | `compose.yaml` still starts Postgres alone | satisfied: the file gains no service. Its volume mount was repaired on Marcel's instruction, which is what makes "starts Postgres alone" true again — it had not started at all |
| Backend build | `dotnet build -c Release` | succeeds; one pre-existing S125 warning in `StorageUseCases.cs` from `develop`, not from this change |
| Backend tests | `dotnet test -c Release` | 150/150 pass |
| OpenAPI contract | build-time generation with no database | unchanged, still generated |

**AC-08 is the honest gap.** Podman is not available in this sandbox, so the podman path is
*designed* for (fully-qualified images, unprivileged ports, engine detection, no
compose features known to be podman-hostile) but was exercised only through docker. First
run on Marcel's machine is the real test — and it already paid off: it produced the three
selection defects above, two of which no docker-only run could ever have shown. The
selection *logic* is now covered on every branch through stubbed podman binaries, but
running the stack itself under a real podman remains unverified from here.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Feature request: env-driven config + compose for backend and frontend | Task |
| 2 | Answers to the G1 questions: nginx, real OIDC, migrate one-shot; then "local testing only", option A, own compose file, CI untouched, two scripts | Shaped the spec |
| 3 | *"bevor du das nächste mal die spec einfrierst, immer zuerst explizit nachfragen"* | I had flipped G1 to frozen after an answer to one open question. Reverted, asked, and recorded as a standing rule. |
| 4 | *"e2e so lassen, das es einfach ist und kein login benötigt"* | Reversed the CI switchover before anything was built |
| 5 | Automated review raised 12 findings on the spec | Ten valid → spec amended (A1–A5); one false positive rebutted with evidence; one partially addressed |
| 6 | Automated review of the implementation, five rounds | Twelve findings, all fixed and answered on the PR: doc drift after the `compose.yaml` fix, a start script that reported success after a failed readiness loop, `cp` clobbering `.env`, `curl` assumed present, hard-coded ports in three places (README setup, the redirect-inspection command, the `.env.example` callbacks), a teardown that deletes the database without saying so, an engine pinned for start but not for teardown, `localhost` documented where only IPv4 is published, and prerequisites that named the engine but not the Compose v2 provider it needs. One finding was a real behaviour bug: `.env.example` shipped `POSTGRES_PASSWORD=storeit`, so the spec's "absent must fail loudly" (config table, EC) could never fire — the template now ships it empty and `compose.stack.yaml`'s `:?` guard reports it by name before anything starts. Ten of the twelve were documentation: the stack behaved correctly and described itself wrongly. |
| 7 | *"es muss mit podman laufen"* / *"es soll aber auch auf docker laufen, zuerst einfach podman prüfen. docker als fallback"* | Marcel's first real run picked docker and then failed. Confirmed the intended order and made the fallback loud instead of silent; see the section above. |

## Outcome

- **Result:** ready for review
- **Deviations from spec:** none. AC-08 verified only for docker, stated above rather than
  claimed.
- **Follow-up:** none outstanding. The `compose.yaml` mount — first recorded here as a
  separate concern — was fixed in this PR after Marcel asked for it, and verified.
- **Re-verified after the engine-detection change:** `./scripts/stack-up.sh` from a
  torn-down state built all three images, ran migrations to completion, and answered
  `/` → 200, `/health` → 200, `/api/v1/storages` → 401; `./scripts/stack-down.sh` removed
  the containers, the volume, the network and exactly the three stack images.
