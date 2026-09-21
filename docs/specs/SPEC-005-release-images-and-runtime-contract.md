# Spec: Release images and a runtime contract for external deployments

> **Status:** Frozen (Gate 1) — approved by Marcel Steiner, 2026-09-21
> **Sprint:** 2026-S38
> **Author:** Claude Fable 5.1 (developer agent), from Marcel Steiner's request
> **Last updated:** 2026-09-21 (A1)

---

## User Story

As the **owner of store-it** I want **every release published as container images together
with a documented runtime contract** so that **a separate deployment repository can run the
application anywhere — first on a free Oracle VM, later elsewhere or twice — while this
repository stays free of hosting-specific data**.

---

## Scope split (ADR-005 decision 1)

| Concern | Lives in | Governed by |
|---|---|---|
| Building and publishing images on release tags | `store-it` (this repo) | this spec, section A |
| The runtime contract: env vars, ports, health, ordering, forwarded headers | `store-it` | this spec, section B |
| Production `compose.yaml`, Caddy, systemd timer, backup, runbook, per-deployment settings | `store-it-deploy` (private) | that repository's README/runbook; not under this repo's gates |
| Hostnames, provider details, buckets, image tag pins | `store-it-deploy/deployments/<name>/` | — |
| Secrets | the host only (`secrets.env`, git-ignored) | — |

The end-to-end acceptance (a release observed from tag to a working sign-in on the public URL)
spans both repositories and is the human G3 test of this spec.

---

## Context: what already exists

| Already true | Evidence |
|---|---|
| Both services are containerised, with a `migrate` target from the same build as the API | `backend/Dockerfile` (targets `migrate`, `runtime`), `frontend/Dockerfile` |
| The single-origin topology is proven locally | `compose.stack.yaml`, `frontend/nginx.conf` proxying `/api` and `/auth` |
| Startup order and failure semantics are defined | SPEC-004 A3: `postgres` healthy → `migrate` exit 0 → `backend` → `web`; no restart loop on config errors |
| Configuration is environment-only, missing config fails at startup | SPEC-004 AC-01/02, `StartupConfigurationCheck` |
| The environment contract is partially documented | `.env.example` (local stack), SPEC-004 A3 table |
| Releases are annotated `vMAJOR.MINOR.PATCH` tags on `main` | ADR-007 |
| The hosting decision is drafted | [ADR-005](../architecture/ADR-005-hosting-deployment.md) (Proposed) |

| Missing | Consequence |
|---|---|
| CI publishes no image | Nothing to deploy; `compose.stack.yaml` builds locally every time |
| No single, complete runtime contract document | A deployment author has to read code and three files to learn what the services need |
| Forwarded-header behaviour is untested | Whether `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` really yields `https` redirect URIs is assumed, not verified |
| **The `web` image overwrites `X-Forwarded-Proto`** | `frontend/nginx.conf` sets `X-Forwarded-Proto $scheme`, and nginx's own scheme inside the container is always `http`. Behind a TLS terminator (Caddy → nginx → API) the API therefore sees `http` even with forwarded headers enabled, and builds `http://` redirect URIs. Found 2026-09-18 while writing the deployment topology. |
| The docs still say "Kubernetes, hosting pending" | Tech stack, architecture §7/§9, README and threat model point at a TODO |

---

## Decisions taken (Marcel, 2026-09-21)

Answers to the open questions of the draft. They settle the individual points; the freeze of
the whole spec is a separate step recorded in the gate table.

| # | Question | Decision |
|---|---|---|
| D2 | What is published | **Releases only**: `vX.Y.Z` and `latest` on `v*` tags. No `develop` tag; a staging channel can be added later without touching anything else. |
| D3 | Image architectures | **arm64 + amd64**, one multi-arch manifest per image, built on native runners. |
| D6 | Image names | `ghcr.io/maststeiner/store-it-backend`, `…/store-it-migrate`, `…/store-it-web`. |
| D7 | Deployment repository | **Private** `maststeiner/store-it-deploy`; hosts pull it with a read-only deploy key. |

D1 (hostname), D4 (backup target) and D5 (alerting) from the first draft moved to
`store-it-deploy` — they are deployment settings, not application concerns.

---

## Amendments (post-freeze)

Corrections found during implementation. None changes scope or an acceptance criterion's
intent; they fix statements that were imprecise when frozen.

| # | Date | Change |
|---|------|--------|
| A1 | 2026-09-21 | AC-09 names `X-Forwarded-Host: <public host>` as an input. The framework switch (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`) processes only `X-Forwarded-For` and `X-Forwarded-Proto`; the public host reaches the API in the **`Host` header**, which both nginx (`proxy_set_header Host $http_host`) and Caddy preserve. The test and the runtime contract therefore use `Host` + `X-Forwarded-Proto`; `X-Forwarded-Host` is neither needed nor evaluated. The AC's intent — `https://<public host>` in the redirect URI — is unchanged. |

---

## Acceptance Criteria (EARS Notation)

### A · Release images

- [ ] AC-01: WHEN an annotated tag matching `v[0-9]+.[0-9]+.[0-9]+` is pushed THE release
      workflow SHALL build the `backend`, `migrate` and `web` images and push them to GHCR
      tagged with the exact version (`vX.Y.Z`) and `latest`.
- [ ] AC-02: WHEN the release workflow builds THE images SHALL be published for
      `linux/arm64` and `linux/amd64` under one multi-arch manifest per image (D3).
- [ ] AC-03: WHEN images are built THE `migrate` and `backend` images SHALL come from the
      same build of the same commit — one never ships without the other.
- [ ] AC-04: WHEN a pull request or a push to `develop` or `main` runs CI THE system SHALL
      publish **no** image; only tags publish (D2).
- [ ] AC-05: WHEN the release workflow runs THE workflow SHALL use only the repository's own
      `GITHUB_TOKEN` with `packages: write`; no long-lived registry credential SHALL be stored.
- [ ] AC-06 (Error): WHEN any image build or push fails THE workflow SHALL fail and publish
      **none** of the three images under the version tag (no partially released version).
- [ ] AC-07: WHEN an image is published THE image SHALL carry OCI labels for source
      repository, revision and version, so a running container can be traced to its commit.

### B · Runtime contract and a hosting-agnostic repository

- [ ] AC-08: WHEN a deployment author reads `docs/operations/runtime-contract.md` THE document
      SHALL list, for each service: image name, every environment variable (required/optional,
      secret or not, default), the listening port, the health endpoint, the required start
      ordering (`postgres` healthy → `migrate` exit 0 → `backend` → `web`), and the
      forwarded-headers requirement behind a TLS terminator — complete enough to write a
      compose file or a Kubernetes manifest without reading code.
- [ ] AC-09: WHEN `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is set and a request arrives with
      `X-Forwarded-Proto: https` and `X-Forwarded-Host: <public host>` THE API SHALL build the
      OIDC `redirect_uri` (and any other absolute URL) with `https://<public host>` — covered
      by a service test, not only by a manual check.
- [ ] AC-09a: WHEN the `web` image receives a request that already carries
      `X-Forwarded-Proto` from an upstream proxy THE nginx SHALL pass that value on to the
      API instead of its own `$scheme`; WHEN the header is absent THE nginx SHALL keep sending
      `$scheme` (local stack unchanged). Implementation hint: an nginx `map` on
      `$http_x_forwarded_proto` with `default $scheme`.
- [ ] AC-10: WHEN the repository is inspected THE system SHALL contain no hostname, IP
      address, provider account detail, bucket name or other installation-specific value in
      any runnable or configuration file; such values live only in `store-it-deploy`. (ADR-005
      naming the provider as a *decision* is documentation, not configuration.)
- [ ] AC-11: WHEN `compose.stack.yaml` and `scripts/stack-*.sh` are used THE local stack SHALL
      behave exactly as before this change (no regression of SPEC-004).
- [ ] AC-12: WHEN the runtime contract changes in a later PR (new variable, port, ordering)
      THE PR SHALL update `runtime-contract.md` in the same change — recorded as a rule in
      `docs/guidelines/coding-guidelines.md` so review can check it.

### C · Documentation

- [ ] AC-13: WHEN ADR-005 is accepted THE tech stack (row *Runtime*), `ARCHITECTURE.md` §7
      and §9, `README.md` (a short "Deploying" note pointing at the contract and the deployment
      repository) and `docs/security/threat-model.md` (R-07, the at-rest/in-transit and DoS
      bullets) SHALL reference the decision instead of "Kubernetes / ADR-005 pending".

---

## Edge Cases

- EC-01: **Tag pushed twice / workflow re-run** — GitHub's tag protection (ADR-007) refuses a
  moved tag; a manual re-run of a succeeded workflow re-pushes identical images, harmless.
- EC-02: **Tag on a commit not on `main`** — the workflow builds whatever the tag points at;
  ADR-007's discipline is a human rule, not enforced here. Documented, not guarded.
- EC-03: **`latest` on a laptop** — a developer pulling `latest` gets the amd64 variant (D3);
  the local stack keeps building from source and is unaffected.
- EC-03a: **Spoofed `X-Forwarded-Proto` when nginx is reached directly** — with AC-09a a
  client that can reach `web` without a proxy could claim `https`. The consequence is only a
  scheme upgrade in generated URLs (cookies are `Secure` anyway in Production), and in every
  deployment `web` is reachable only through the proxy. Accepted; noted in the contract.
- EC-04: **Forwarded headers without a trusted-proxy list** — the built-in switch trusts any
  `X-Forwarded-*` header. The runtime contract SHALL state the coupling explicitly: the switch
  is safe only when `backend` is reachable exclusively from the reverse proxy (never published
  on a host interface). Enforcing that is the deployment's job; documenting it is this repo's.
- EC-05: **A native arm64 runner is unavailable** — the workflow SHALL fall back to QEMU
  emulation for that platform rather than publish an amd64-only manifest under `latest`.
- EC-06: **The `web` image and the API disagree on paths** (`/api`, `/auth`, `/health`) — both
  come from the same tag; the contract lists the paths nginx proxies so a deployment that
  replaces nginx (e.g. an ingress) knows what to route.

---

## Out of Scope

- Everything on the deployment side: the production compose file, Caddy, systemd timer,
  backups, runbook, provisioning, hostnames — `store-it-deploy`.
- Kubernetes manifests, Helm/Kustomize — deferred by ADR-005.
- A staging environment or per-PR preview deployments (D2 alternative would only publish the
  tag; running it is still a deployment concern).
- Alerting/monitoring beyond `/health`.
- Zero-downtime / blue-green releases.
- Application-level rate limiting or a WAF (threat model R-07 stays "hosting layer").
- Any code change to the API beyond what AC-09 needs — expected to be **none**. (AC-09a is a
  change to `frontend/nginx.conf`, not to application code.)

---

## Technical Constraints (from Architect Agent)

<!-- To be confirmed by the architect persona after Gate 1 -->

- [ ] Layering: no application code changes expected. If AC-09 reveals that the built-in
      switch is insufficient, the fix lives in `StoreIt.Api` (composition root) only.
- [ ] Dependencies: no new NuGet or npm dependency. New GitHub Actions (`docker/login-action`,
      `docker/setup-buildx-action`, `docker/build-push-action`, `docker/metadata-action`) are
      pinned by SHA like the existing ones.
- [ ] Workflow: `release.yml` is separate from `ci.yml`, `permissions: contents: read,
      packages: write`, and is covered by the existing workflow-lint job.
- [ ] Images stay non-root and on unprivileged ports (SPEC-004 AC-08).
- [ ] ADR required: yes → [ADR-005](../architecture/ADR-005-hosting-deployment.md) — must be
      **Accepted** before this spec is frozen, since the spec implements it.

---

## Verification

<!-- Filled in by QA / developer during implementation -->

| AC | How verified | Status |
|----|--------------|--------|
| AC-01 – AC-07 | `release.yml` written and linted (actionlint 1.7.12, clean); native runners per arch, push-by-digest, manifests only in the final job (AC-06), labels + index annotations (AC-07). **Runtime proof needs the first `v*` tag** — recorded here after the run. | 🟡 pending first release |
| AC-08 | `docs/operations/runtime-contract.md` written from `compose.stack.yaml`, `.env.example`, `appsettings.json`, both Dockerfiles, `nginx.conf`, `StartupConfigurationCheck` and `AuthenticationSetup`; the `store-it-deploy` compose was written against it | ✅ 2026-09-21 |
| AC-09 | `ForwardedHeadersTests.Login_BehindTlsTerminator_BuildsHttpsRedirectUri` (+ `…KeepsTheRequestScheme` as the control) — real host, `ForwardedHeaders_Enabled=true`, static OIDC discovery; 84/84 service tests green locally | ✅ 2026-09-21 |
| AC-09a | nginx `map` in `frontend/nginx.conf`; functional test with the `nginx:1.31-alpine` image against an echo backend: no header → `proto=http`, `X-Forwarded-Proto: https` → `proto=https`, `X-Forwarded-Proto: evil` → `proto=http`, SPA fallback 200; `nginx -t` ok | ✅ 2026-09-21 |
| AC-10 | grep over yml/yaml/json/conf/sh/cs/ts/Dockerfile/.env.example for hostnames, IPs, provider, bucket names: no hits (the GHCR image names are decision D6, not installation data) | ✅ 2026-09-21 |
| AC-11 | Only `nginx.conf` changed for the local stack, and only the header value when an upstream sends `https`; the no-header path is byte-identical in behaviour (tested above). `stack-up.sh` untouched | ✅ 2026-09-21 (by test of the changed path; full stack run left to G3) |
| AC-12 | Rule added to `docs/guidelines/coding-guidelines.md` (*Project-Specific Rules*), pointer row in `CLAUDE.md` | ✅ 2026-09-21 |
| AC-13 | tech-stack *Runtime* row, `ARCHITECTURE.md` §7 + §9, `README.md` *Deploying*, threat model R-07 updated + R-21 added + owner-responsibility bullets | ✅ 2026-09-21 |
| End to end (G3) | One release observed: tag → workflow → `store-it-deploy` host pulls → sign-in on the public URL | ⬜ human |

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ | 2026-09-21 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
