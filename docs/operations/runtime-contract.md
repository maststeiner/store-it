# Runtime contract — running the store-it images

> **Audience:** whoever writes a deployment (compose file, Kubernetes manifests, a NAS
> stack). This document is complete on purpose: you should not need to read code or
> Dockerfiles to run the published images.
> **Governance:** SPEC-005 AC-08/AC-12 — any change to a variable, port, path, health
> endpoint or the start ordering updates this file **in the same pull request**
> (coding guidelines, *Project-Specific Rules*). The deployment repository
> `store-it-deploy` implements this contract; ADR-005 explains the split.

---

## 1. Images

Published by `.github/workflows/release.yml` on every release tag (ADR-007), as multi-arch
manifests for `linux/amd64` and `linux/arm64`:

| Image | Built from | Runs | Tags |
|---|---|---|---|
| `ghcr.io/maststeiner/store-it-backend` | `backend/Dockerfile`, target `runtime` | the API | `vX.Y.Z`, `latest` |
| `ghcr.io/maststeiner/store-it-migrate` | `backend/Dockerfile`, target `migrate` | EF Core migrations, then exits | `vX.Y.Z`, `latest` |
| `ghcr.io/maststeiner/store-it-web` | `frontend/Dockerfile` | nginx: Angular bundle + reverse proxy | `vX.Y.Z`, `latest` |

`latest` always equals the newest release tag. Each index also carries buildx **attestation
manifests** (provenance/SBOM), which registries and `imagetools inspect` list as platform
`unknown/unknown` — expected, not a broken build. Images are pullable without credentials. **Run `backend` and `migrate` from the same
tag** — they come from the same build and agree on the set of migrations; mixing tags is
unsupported. Every image carries the OCI labels `org.opencontainers.image.{source,revision,
version,title}`.

---

## 2. Services and start ordering

```
postgres  ──healthy──▶  migrate  ──exit 0──▶  backend  ──healthy──▶  web  ──▶  (your TLS proxy)
```

| Service | Image | Listens | Health | User | Restart policy |
|---|---|---|---|---|---|
| `postgres` | `postgres:18-alpine` (or an external PostgreSQL ≥ 16) | 5432 | `pg_isready` | — | always |
| `migrate` | `…-migrate` | — | exit code `0` = done | root (SDK image) | **never** — a one-shot admin process |
| `backend` | `…-backend` | **8080** (`ASPNETCORE_HTTP_PORTS`) | `GET /health` → 200 (ships a `HEALTHCHECK`) | uid 10001, non-root | always |
| `web` | `…-web` | **8080** | `GET /` → 200 (ships a `HEALTHCHECK`) | nginx default | always |

Rules a deployment must keep:

1. **`migrate` runs to completion with exit 0 before `backend` starts** — on every start,
   including every release. `backend` never migrates itself. A failed migration must leave
   `backend` unstarted (or the previous `backend` running); a half-migrated database is not
   served. Never run two `migrate` processes concurrently.
2. **`web` reaches the API as `http://backend:8080`.** The hostname `backend` is fixed in the
   image's nginx configuration; provide DNS for it (compose service name, Kubernetes
   `Service` named `backend`, or an alias).
3. **Only `web` (or the proxy in front of it) is published.** `backend` and `postgres` are
   never reachable from outside the deployment network — see §5.
4. **Configuration errors are terminal.** A missing connection string makes `backend` exit
   with code 1 and a message naming the variable; do not restart-loop it into "healthy".
5. **Persistence:** the PostgreSQL data directory (`/var/lib/postgresql` for the 18 images)
   is the only state. Back it up (`pg_dump`). The application containers are stateless.

---

## 3. What `web` routes

The nginx in `…-web` serves the Angular bundle and proxies these paths to `backend:8080`,
preserving the browser's `Host` header (needed for OIDC redirect URIs):

| Path | Goes to | Notes |
|---|---|---|
| `/api/**` | backend | REST API, cookie session + `X-XSRF-TOKEN` double submit |
| `/auth/**` | backend | login, callback, logout, csrf, me |
| `/health` | backend | API health; anonymous |
| everything else | static files, fallback `index.html` | SPA routes |

A deployment that replaces nginx (e.g. an ingress routing straight to the API) must route
exactly these three prefixes to `backend` and everything else to static assets, **on one
origin** — the session cookie and CSRF contract are same-origin by design (SPEC-003/004).

---

## 4. Environment variables

.NET maps `Section__Key` (double underscore) to configuration `Section:Key`. Unset optional
variables fall back to the defaults below.

### `backend`

| Variable | Required | Secret | Default | Meaning |
|---|---|---|---|---|
| `ConnectionStrings__storeit` | **yes** | yes (contains the password) | — | Npgsql connection string, e.g. `Host=postgres;Port=5432;Database=storeit;Username=storeit;Password=…`. Missing → exit 1 at startup. |
| `ASPNETCORE_ENVIRONMENT` | no | no | `Production` | **Must stay `Production`** in any reachable deployment. `Development` maps `POST /auth/dev-login`, which issues a session without credentials, and relaxes cookie/HTTPS rules. |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | **yes, behind TLS termination** | no | unset (off) | `true` makes the API trust `X-Forwarded-For` / `X-Forwarded-Proto` so it builds `https://` URLs (OIDC `redirect_uri`) when TLS ends at a proxy. See §5 for the trust caveat. |
| `ASPNETCORE_HTTP_PORTS` | no | no | `8080` (set in the image) | Listening port. Keep 8080 unless you also change `web`'s upstream. |
| `Authentication__Google__ClientId` | no | no | empty | Sign-in with Google is registered only when **ClientId and Authority** are both non-empty; otherwise `/auth/login/google` answers `400 auth.provider.unconfigured` and the rest of the app works. |
| `Authentication__Google__ClientSecret` | with ClientId | **yes** | empty | |
| `Authentication__Google__Authority` | no | no | `https://accounts.google.com` | |
| `Authentication__Google__CallbackPath` | no | no | `/auth/callback/google` | Register `https://<host>/auth/callback/google` at Google. |
| `Authentication__Microsoft__ClientId` | no | no | empty | Same registration rule as Google. |
| `Authentication__Microsoft__ClientSecret` | with ClientId | **yes** | empty | |
| `Authentication__Microsoft__Authority` | with ClientId | no | empty | Must name a **concrete** issuer: `https://login.microsoftonline.com/consumers/v2.0` (personal accounts) or `…/<tenant>/v2.0`. Not `common`/`organizations`. |
| `Authentication__Microsoft__CallbackPath` | no | no | `/auth/callback/microsoft` | |
| `Logging__LogLevel__Default` | no | no | `Information` | Standard .NET logging; logs go to stdout. |

### `migrate`

| Variable | Required | Secret | Default | Meaning |
|---|---|---|---|---|
| `ConnectionStrings__storeit` | **yes** | yes | — | Same value as `backend`. The migration runner needs DDL rights on the database. |

### `web`

None. Port 8080, upstream `backend:8080`, both fixed in the image. Hashed assets are served
with `Cache-Control: public, immutable`, `index.html` with `no-store`.

### `postgres` (when you run it yourself)

`POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` — the official image's variables; the
application only needs a database it can connect to with the connection string above.

---

## 5. Behind a TLS terminator (the normal case)

The browser must see **one origin over HTTPS**: `https://<host>/` for the app, `/api` and
`/auth` on the same host. Put your TLS proxy (Caddy, nginx, Traefik, an ingress, a tunnel) in
front of `web:8080` and make sure it:

1. **Preserves the `Host` header** the browser sent (Caddy's `reverse_proxy` does by
   default; nginx needs `proxy_set_header Host $http_host`). The API takes the host for its
   absolute URLs from `Host`; `X-Forwarded-Host` is **not** evaluated.
2. **Sets `X-Forwarded-Proto` to the browser's scheme** (`https`). `web` passes an upstream
   value of `https` through to the API and otherwise sends its own scheme, so a plain-http
   local stack keeps working.
3. **Sets `X-Forwarded-For`** if you want client IPs in the API's logs (optional).

And set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` on `backend`. Without it the API answers
OIDC challenges with `http://…` redirect URIs and the providers refuse the sign-in.

**Trust caveat.** That switch makes the API trust the forwarded headers from *any* caller —
it clears the framework's known-proxy list. This is safe **only because `backend` is not
reachable from outside the deployment network** (rule 3 in §2). Publishing `backend` on a host
port or an ingress while the switch is on lets a client spoof its scheme and address. Do not
do that; if you must expose the API directly, terminate TLS in front of it and configure
`ForwardedHeadersOptions.KnownProxies` in code instead (a change to `StoreIt.Api`).

Cookies: in `Production` the session and CSRF cookies are `Secure`; they work only over HTTPS
(or `http://localhost`, which browsers treat as secure — the local stack relies on that).

---

## 6. Checklist for a new deployment

- [ ] `migrate` before `backend`, `backend` healthy before `web`
- [ ] `backend` resolvable as `backend:8080` from `web`; neither published
- [ ] `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`
- [ ] TLS proxy preserves `Host`, sets `X-Forwarded-Proto: https`, fronts `web:8080` only
- [ ] `ConnectionStrings__storeit` on both `backend` and `migrate`, from a secret store
- [ ] OIDC redirect URIs registered for `https://<host>/auth/callback/{google,microsoft}`
- [ ] PostgreSQL data on persistent storage, backed up
- [ ] Both application images on the **same** tag

Reference implementation: `compose.yaml` in `store-it-deploy`.
