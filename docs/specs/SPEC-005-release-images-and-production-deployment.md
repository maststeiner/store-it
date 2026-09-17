# Spec: Release images and automatic deployment to the production VM

> **Status:** Draft
> **Sprint:** 2026-S38
> **Author:** Claude Fable 5.1 (developer agent), from Marcel Steiner's request
> **Last updated:** 2026-09-17

---

## User Story

As the **owner of store-it** I want **every release to be published as container images and
rolled out to the production VM automatically** so that **the application is online for my
family at no running cost, and I never touch a server to ship a release**.

---

## Context: what already exists

| Already true | Evidence |
|---|---|
| Both services are containerised, with a `migrate` target from the same build as the API | `backend/Dockerfile` (targets `migrate`, `runtime`), `frontend/Dockerfile` |
| The single-origin topology is proven locally | `compose.stack.yaml`, `frontend/nginx.conf` proxying `/api` and `/auth` |
| Startup order and failure semantics are defined | SPEC-004 A3: `postgres` healthy → `migrate` exit 0 → `backend` → `web`; no restart loop on config errors |
| Configuration is environment-only, missing config fails at startup | SPEC-004 AC-01/02, `StartupConfigurationCheck` |
| Releases are annotated `vMAJOR.MINOR.PATCH` tags on `main` | ADR-007 |
| Renovate keeps base images and dependencies current on `develop` | `renovate.json` |
| The hosting decision is drafted | [ADR-005](../architecture/ADR-005-hosting-deployment.md) (Proposed) |

| Missing | Consequence |
|---|---|
| CI publishes no image | Nothing to deploy; `compose.stack.yaml` builds locally every time |
| No production compose file | The local one binds to `127.0.0.1`, builds from source and has no TLS |
| The API does not read forwarded headers | Behind a TLS terminator the OIDC redirect URI would be `http://…` and be rejected |
| No update mechanism on a host | A new release would require a manual pull |
| No backup, no runbook | Data loss on the single VM would be total; provisioning knowledge lives in one head |

---

## Open decisions (to be answered before the freeze)

These are the inputs the spec cannot decide by itself. Defaults are what the acceptance
criteria below assume.

| # | Question | Default assumed below |
|---|---|---|
| D1 | Public hostname | a subdomain of a Hostpoint-managed domain, e.g. `storeit.<domain>`; the exact name is needed for the OIDC redirect URIs and Caddy |
| D2 | What production follows | **releases only** (`latest` = last `v*` tag). Alternative: also publish a `develop` tag and let the VM follow it |
| D3 | Image architectures | **arm64 + amd64** multi-arch on native runners. Alternative: arm64 only (simpler workflow, VM only) |
| D4 | Backup target and retention | **OCI Object Storage bucket, nightly `pg_dump`, 14 days** |
| D5 | Alerting when the timer or a migration fails | **none in this spec** — the runbook documents how to check; alerting is a follow-up issue |
| D6 | Image names | `ghcr.io/maststeiner/store-it-backend`, `…-migrate`, `…-web` |

---

## Acceptance Criteria (EARS Notation)

### A · Release images

- [ ] AC-01: WHEN an annotated tag matching `v[0-9]+.[0-9]+.[0-9]+` is pushed THE release
      workflow SHALL build the `backend`, `migrate` and `web` images and push them to GHCR
      tagged with the exact version (`vX.Y.Z`) and `latest`.
- [ ] AC-02: WHEN the release workflow builds THE images SHALL be published for
      `linux/arm64` and `linux/amd64` under one multi-arch manifest per image (D3), so
      `docker pull` picks the right one on the VM and on a developer laptop.
- [ ] AC-03: WHEN images are built THE `migrate` and `backend` images SHALL come from the
      same build stage of the same commit — one never ships without the other.
- [ ] AC-04: WHEN a pull request or a push to `develop` or `main` runs CI THE system SHALL
      publish **no** image; only tags publish (D2).
- [ ] AC-05: WHEN the release workflow runs THE workflow SHALL use only the repository's own
      `GITHUB_TOKEN` with `packages: write`; no long-lived registry credential SHALL be stored.
- [ ] AC-06 (Error): WHEN any image build or push fails THE workflow SHALL fail and publish
      **none** of the three images under the version tag (no partially released version).

### B · Production topology

- [ ] AC-07: WHEN `docker compose -f compose.prod.yaml up -d` runs on a host with a filled
      `.env` THE system SHALL start `postgres`, `migrate`, `backend`, `web` and `caddy` from
      published images (no `build:`), with the SPEC-004 ordering: `migrate` completes with
      exit 0 before `backend` starts, `web` waits for a healthy `backend`.
- [ ] AC-08: WHEN a browser opens `https://<hostname>/` THE system SHALL serve the application
      over TLS with a valid, automatically obtained and renewed certificate, and `/api`,
      `/auth` and `/health` SHALL be reachable on that same origin.
- [ ] AC-09: WHEN the API receives a request through Caddy THE system SHALL build absolute
      URLs (in particular the OIDC `redirect_uri`) with scheme `https` and the public
      hostname — verified by a successful Google or Microsoft sign-in on the public URL.
- [ ] AC-10: WHEN the host is inspected THE system SHALL expose only ports 22, 80 and 443;
      `postgres`, `backend` and `web` SHALL NOT be published on any host interface.
- [ ] AC-11: WHEN the VM reboots THE system SHALL bring all long-running services back
      without manual action, and `migrate` SHALL NOT restart-loop (`restart: "no"` stays).
- [ ] AC-12: WHEN the repository is inspected THE system SHALL contain `compose.prod.yaml`
      and `.env.prod.example` with every variable the production stack reads, classified as
      in SPEC-004 A3, and no secret value.
- [ ] AC-13: WHEN `compose.stack.yaml` and `scripts/stack-*.sh` are used THE local stack SHALL
      behave exactly as before this change (no regression of SPEC-004).

### C · Automatic updates

- [ ] AC-14: WHEN a new `latest` image is available in GHCR THE VM SHALL pull it and recreate
      the affected services within **10 minutes**, without any human action.
- [ ] AC-15: WHEN the pulled release changes the `backend`/`migrate` images THE system SHALL
      run the new `migrate` to completion **before** recreating `backend`.
- [ ] AC-16 (Error): WHEN the new `migrate` fails THE system SHALL keep the previous `backend`
      running and serving, SHALL NOT recreate it, and SHALL retry on the next timer run.
- [ ] AC-17: WHEN the operator sets `STOREIT_IMAGE_TAG=vX.Y.Z` in the VM's `.env` THE next
      timer run SHALL roll the stack to exactly that version (rollback path).
- [ ] AC-18: WHEN the update runs THE system SHALL remove dangling images afterwards so the
      boot volume does not fill up with old releases.
- [ ] AC-19: WHEN nothing changed THE timer run SHALL be a no-op — no container is recreated,
      no downtime.

### D · Backup and restore

- [ ] AC-20: WHEN the nightly backup job runs THE system SHALL upload a `pg_dump` of the
      production database to the configured Object Storage bucket (D4) and delete dumps older
      than the retention period.
- [ ] AC-21: WHEN the restore procedure in the runbook is followed on a fresh VM THE system
      SHALL come back with the restored data — exercised **once** as part of acceptance.

### E · Documentation

- [ ] AC-22: WHEN a person provisions the VM THE runbook (`docs/operations/production.md`)
      SHALL take them from an empty OCI tenancy to a running, TLS-secured installation, and
      SHALL cover: update timer, rollback, backup, restore, log access, and what to check
      when a release does not appear.
- [ ] AC-23: WHEN ADR-005 is accepted THE tech-stack (row *Runtime*), `ARCHITECTURE.md` §7
      and §9, `README.md` (deployment note) and `docs/security/threat-model.md` (R-07, the
      "at-rest / in-transit" and "DoS" bullets) SHALL reference the decision instead of
      "Kubernetes / ADR-005 pending".

---

## Edge Cases

- EC-01: **Tag pushed twice / re-run of the workflow** — pushing the same version tag again is
  refused by GitHub's tag protection (ADR-007); a manual re-run of a succeeded workflow
  re-pushes identical images, which is harmless.
- EC-02: **Tag on a commit not on `main`** — the workflow builds whatever the tag points at;
  ADR-007's discipline (tags only on release merges) is a human rule, not enforced here.
  Documented, not guarded.
- EC-03: **Timer fires while a previous run is still pulling** — systemd does not start a
  second instance of a oneshot service that is still running; no overlap.
- EC-04: **GHCR unreachable** — `pull` fails, `up -d` is skipped (the script stops on the
  first error), the stack keeps running the current images; the next run retries.
- EC-05: **Migration takes longer than the timer interval** — same as EC-03: no overlap.
- EC-06: **Caddy recreated repeatedly** — its data volume (certificates, ACME account) is
  persistent, so recreation never re-requests a certificate; Let's Encrypt rate limits are not
  hit.
- EC-07: **OCI Ubuntu images ship iptables rules that drop everything except 22** — opening
  80/443 in the VCN security list is not enough; the runbook adds the host rules and persists
  them.
- EC-08: **Ephemeral public IP changes on stop/start** — the runbook reserves a public IP
  before the DNS record is created.
- EC-09: **`latest` on a laptop** — a developer pulling `latest` gets the amd64 variant (D3);
  the local stack keeps building from source and is unaffected.
- EC-10: **Disk full** — AC-18 prunes images; the backup job writes to Object Storage, not
  the boot volume, except for one transient dump file which it removes.
- EC-11: **The instance is reclaimed or the tenancy is closed by Oracle** — the runbook's
  restore path (AC-21) on a new VM (Oracle again, or the Hetzner fallback named in ADR-005) is
  the answer; recovery point is the last nightly dump.
- EC-12: **`ASPNETCORE_FORWARDEDHEADERS_ENABLED` without a trusted proxy list** — the built-in
  switch clears `KnownNetworks`/`KnownProxies` and trusts any `X-Forwarded-*`. This is safe
  here only because `backend` is not published on any host interface (AC-10) and its sole
  caller is `web`/`caddy` on the compose network. The runbook states this coupling.

---

## Out of Scope

- Kubernetes (k3s or managed), Helm/Kustomize manifests — deferred by ADR-005.
- A staging environment or preview deployments per pull request.
- Alerting/monitoring beyond `/health` and the runbook's manual checks (D5, follow-up issue).
- Zero-downtime / blue-green releases.
- Publishing images from `develop` (unless D2 is decided otherwise).
- Changing the `web` image to serve TLS itself — Caddy fronts the unchanged nginx image.
- Application-level rate limiting or a WAF (threat model R-07 stays "hosting layer").
- Anything Hostpoint beyond a DNS record.

---

## Technical Constraints (from Architect Agent)

<!-- To be confirmed by the architect persona after Gate 1 -->

- [ ] Layering: no application code changes are expected. The one runtime switch
      (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`) is configuration, set in `compose.prod.yaml`.
      If the implementation finds that a code change is unavoidable, it lives in
      `StoreIt.Api` (composition root) only.
- [ ] Dependencies: new external components are Caddy (container) and the OCI CLI on the VM
      (backup upload); both are justified in ADR-005. No new NuGet or npm dependency.
- [ ] Workflow: `release.yml` is a separate workflow from `ci.yml`, with `permissions:
      contents: read, packages: write`, pinned action SHAs like the existing workflow, and
      covered by the existing workflow-lint job.
- [ ] Images stay non-root and on unprivileged ports (SPEC-004 AC-08); Caddy is the only
      container binding 80/443 on the host.
- [ ] ADR required: yes → [ADR-005](../architecture/ADR-005-hosting-deployment.md) — must be
      **Accepted** before this spec is frozen, since the spec implements it.

---

## Verification

<!-- Filled in by QA / developer during implementation -->

Most criteria are operational and are verified once on the real VM, recorded here with date
and evidence (workflow run URL, command output). Human G3 testing (sign-in on the public URL,
one release rollout observed end to end) is part of the acceptance.

| AC | How verified | Status |
|----|--------------|--------|
| AC-01 – AC-06 | Release workflow run on a `v0.x.y` pre-release tag; GHCR package pages show tags and both platforms | ⬜ |
| AC-07 – AC-13 | `compose config` in CI (syntax); `up` on the VM; `ss -tlnp` for AC-10; reboot test for AC-11; `stack-up.sh` still passes for AC-13 | ⬜ |
| AC-14 – AC-19 | One real release observed: timer log, container recreate timestamps, a deliberately failing migration on a test tag for AC-16, a pinned tag for AC-17 | ⬜ |
| AC-20 – AC-21 | Bucket listing after the first night; one restore on a scratch VM | ⬜ |
| AC-22 – AC-23 | Runbook followed by its author on a fresh tenancy; doc diff | ⬜ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ⬜ | | |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
