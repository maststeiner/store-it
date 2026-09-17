# ADR-005: Hosting and deployment — separate deployment repository, one free Oracle Cloud VM, Docker Compose, pull-based updates

> **Status:** Proposed
> **Date:** 2026-09-17
> **Deciders:** Marcel Steiner

---

## Context

The stack has been "cloud-native, Kubernetes" on paper since the first ADRs, but no hosting
decision was ever taken (issue #17). Meanwhile SPEC-004 delivered everything a deployment needs
except the deployment itself: both services are containerised, configuration comes strictly from
the environment, migrations run as a one-shot admin process, and the local stack already proves
the single-origin topology (nginx serving the Angular bundle and proxying `/api` and `/auth`).
SPEC-004 was explicit that its stack is *a local testing tool, not a deployment artifact* and
parked TLS, registry publishing and non-localhost hostnames here.

The constraints that drive this decision, in order of weight:

1. **Cost: zero or as close as possible.** store-it is a family tool with a handful of users;
   a monthly hosting bill is not justified. This rules out managed Kubernetes and managed
   PostgreSQL at every provider.
2. **Automatic updates.** Once a release is cut, the running installation must pick it up
   without anyone touching a server. The human gates stay where the AI-Dev Process puts them
   (spec, review, merge, release tag); what happens *after* the tag is automation.
3. **Location Switzerland or EU.** Personal data (account identifiers from OIDC, what a family
   stores in its pantry) stays in a jurisdiction the owner is comfortable with.
4. **Keep the 12-factor shape.** Whatever runs the containers must not force application
   changes that would make a later move (to Kubernetes, to another provider) harder.
5. **No hosting-specific data in the application repository.** Hostnames, provider names,
   IP addresses, bucket names and per-installation settings do not belong next to the code —
   the application repository is public, and more than one installation must be possible
   without forking it.

An existing Hostpoint account was evaluated and is **not** a runtime option: Hostpoint's shared
webhosting is a PHP/MySQL platform without .NET, containers or PostgreSQL, and its Managed Flex
Server line advertises Node.js and PHP but no container runtime. Hostpoint stays useful for what
it is good at — the domain, DNS and e-mail.

---

## Decision

### 1. Two repositories: `store-it` publishes images, `store-it-deploy` runs them

The application repository stays **hosting-agnostic**. Its deliverables for operations are
the published images and a documented **runtime contract** — every environment variable the
services read, the ports, the health endpoint, the start ordering (`migrate` before
`backend`), and the forwarded-headers requirement behind a TLS terminator. It contains no
hostname, provider, IP, bucket or installation-specific value; `compose.stack.yaml` remains
the local testing tool of SPEC-004 and nothing more.

A separate, **private** repository `maststeiner/store-it-deploy` holds everything specific to
running the application somewhere: the production `compose.yaml`, the Caddy configuration,
systemd units, backup scripts, the runbook, and **one directory per deployment** under
`deployments/<name>/` — committed non-secret settings (hostname, image tag channel, backup
bucket, provider notes) plus a git-ignored secrets file that exists only on the host. A second
installation (another household, a staging host, the fallback provider) is another directory,
not a fork. The first deployment is `deployments/prod-oracle`, described in the decisions
below.

The host pulls the deployment repository (read-only deploy key) in the same timer run that
pulls images, so a configuration change reaches the installation the same way a release does.

### 2. Runtime: one Oracle Cloud Infrastructure (OCI) VM from the Always Free tier

- **Shape:** `VM.Standard.A1.Flex` (Ampere, arm64) at the Always Free limit in force since
  2026-06-15: **2 OCPU, 12 GB RAM**. Boot volume 100 GB (Always Free covers 200 GB of block
  storage in total). Ubuntu 24.04 LTS (aarch64).
- **Region:** `eu-zurich-1` (Switzerland North). This is the tenancy's *home region* and cannot
  be changed after sign-up.
- **Account mode:** the tenancy is upgraded to **Pay-As-You-Go** with a **budget alert at
  CHF 1**. Always Free resources remain free under Pay-As-You-Go; the upgrade removes the two
  known Free-only problems (idle instances being reclaimed, and lower priority when A1
  capacity is scarce). No paid resource is provisioned; the alert exists to catch a mistake.
- **Environments:** one, production. No staging environment — the local stack (SPEC-004) and
  CI's end-to-end job cover pre-release testing.

### 3. Orchestration: Docker Compose, not Kubernetes — for now

The `compose.yaml` of `store-it-deploy` describes the production topology: `postgres`,
`migrate`, `backend`, `web`, plus `caddy` (see 4). It references **published images only**
(no `build:`), is shared by all deployments (per-deployment differences are environment
values, never a second compose file), and it reuses the ordering guarantees of
`compose.stack.yaml` verbatim: `migrate` runs to completion before `backend` starts, `web`
waits for a healthy `backend`.

Kubernetes is **deferred, not rejected**. The application stays 12-factor (config from the
environment, stateless processes, health endpoint, logs to stdout, admin process for
migrations), so the same images run unchanged on k3s or a managed cluster. The revisit
triggers are: a second environment, a second node, or a second maintainer. Until one of them
fires, a cluster would add operational surface without adding a user-visible property.

### 4. Ingress and TLS: Caddy in front of the existing nginx image

A `caddy` container terminates TLS with automatic Let's Encrypt certificates, listens on 80/443,
and reverse-proxies everything to `web:8080`. The `web` image (nginx) stays exactly the image
the local stack uses, so the single-origin contract of SPEC-004 (cookie session, `X-XSRF-TOKEN`
double submit, no CORS) is unchanged in production.

Because TLS now terminates *in front of* the API, the backend must trust the forwarded scheme
and host — otherwise it would build `http://` OIDC redirect URIs and the providers would reject
them. The deployment repository enables this through
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, the framework's built-in switch; the application
repository only has to document the requirement in the runtime contract and verify that the
switch produces `https` URLs.

### 5. Database: PostgreSQL in a container on the same VM, backed up nightly

No managed database — the cheapest one costs more than the entire rest of this decision.
PostgreSQL 18 runs as a container with its data on a named volume on the VM's block storage.
A nightly `pg_dump` is uploaded to an **OCI Object Storage** bucket (Always Free covers 20 GB),
retention 14 days. The restore procedure is part of the runbook and is exercised once as part
of the acceptance test.

### 6. Registry and image build: GHCR, built by GitHub Actions on release tags

Images are published to the GitHub Container Registry under
`ghcr.io/maststeiner/store-it-{backend,migrate,web}`. A release workflow triggers on an
annotated `v*` tag (the release marker of ADR-007), builds all three images for **`linux/arm64`
and `linux/amd64`** on native runners, and pushes them tagged `vMAJOR.MINOR.PATCH` and `latest`.
The `migrate` image comes from the same build as `backend`, so the two can never disagree
about which migrations exist. CI continues to publish nothing on pull requests or `develop`.

### 7. Deployment: the VM pulls; nothing pushes into it

A systemd timer on the VM runs every five minutes:

```bash
git -C /opt/store-it-deploy pull --ff-only --quiet          # configuration
docker compose --env-file deployments/$DEPLOYMENT/deployment.env \
               --env-file deployments/$DEPLOYMENT/secrets.env pull --quiet
docker compose … up -d --remove-orphans                     # same env files
docker image prune -f
```

Compose recreates only services whose image changed. A new release changes `backend` and
`migrate` together, so `migrate` re-runs and `backend` is recreated only after it succeeded.
If the migration fails, the previous `backend` keeps serving the previous schema (EF Core
migrations on PostgreSQL are transactional), and the timer retries idempotently.

Production follows `latest`, i.e. releases only. Rolling back is pinning the previous version
in the deployment's committed `deployment.env` (`STOREIT_IMAGE_TAG=v1.2.3`) — a commit in
`store-it-deploy`, applied by the same timer, visible in that repository's history.

Two mechanisms were deliberately **not** chosen:

- **Watchtower** (or its maintained fork): it restarts a container when its image changes, but
  knows nothing about the `migrate` → `backend` ordering; the original project was archived in
  December 2025.
- **Push deployment over SSH from GitHub Actions**: faster (seconds instead of minutes) but
  requires an SSH key in GitHub secrets and inbound SSH from the internet. Pulling needs
  neither; GitHub holds no credential for the VM.

### 8. Secrets and configuration

`deployments/<name>/secrets.env` (mode 0600, git-ignored, present only on the host) is the
only place production secrets live: database password, OIDC client secrets. Non-secret
settings — hostname, image tag, backup bucket — are committed in `deployments/<name>/deployment.env`
of the deployment repository. The application repository commits nothing deployment-specific.
GitHub Actions needs only `packages: write` on its own token to push images; the host holds a
read-only deploy key for `store-it-deploy` and no credential for anything else.

### 9. DNS and domain

A subdomain of a domain already managed at Hostpoint points (A record, reserved public IP) at
the VM. The concrete name is recorded in `deployments/prod-oracle/deployment.env`, not in
the application repository.

---

## Rationale

| Option | Verdict | Why |
|---|---|---|
| **OCI Always Free VM (chosen)** | ✅ | CHF 0, region Zurich, 2 OCPU/12 GB is ample for this workload, always on, full control. |
| Hetzner Cloud CAX11 (arm64, Nuremberg) | fallback | ≈ CHF 6/month, more predictable than Oracle (no reclaim policy, no capacity lottery). **Identical mechanics** — if Oracle disappoints, only the VM provisioning step changes. |
| Hostpoint webhosting / Managed Flex Server | ✗ | No .NET, no containers, no PostgreSQL. Kept for DNS/e-mail. |
| Render free tier + Neon free PostgreSQL | ✗ | Service sleeps after 15 min idle (≈ 30–60 s cold start for a .NET container), no pre-deploy command on the free tier (migrations), image-based services do not auto-deploy. |
| Azure Container Apps (consumption) + Neon | ✗ | Fits the Microsoft identity story and has a Swiss region, but a managed PostgreSQL costs ≈ CHF 15/month, and the setup surface (ACA environment, ACR or GHCR auth, jobs for migrations) is out of proportion for one app. |
| Managed Kubernetes (any provider) | ✗ | Control-plane and node fees alone exceed every other option combined. Deferred per decision 3. |
| Hosting configuration inside `store-it` | ✗ | Mixes concerns, puts hostnames and provider details into a public repository, and bakes exactly one installation into the code repo. A private deployment repository with per-deployment directories keeps the application portable and lets a second installation be a directory, not a fork. |
| Watchtower for updates | ✗ | Migration ordering; upstream archived. |
| Push deploy via SSH from CI | ✗ | Inbound SSH + VM credential in GitHub for a gain of a few minutes. |

**Why arm64 *and* amd64 images.** The VM is arm64, but developers on x86 and any later move
to another host should be able to run the *published* images unchanged. Native runners for
both architectures make the multi-arch build a matter of a manifest, not of emulation time.

**Why Pay-As-You-Go on a "free" decision.** Community experience with Free-only tenancies is
consistent: idle instances get reclaimed, and A1 capacity in a small region is granted to paying
tenancies first. The upgrade costs nothing as long as usage stays inside Always Free limits; the
budget alert guards the one way this could go wrong.

---

## Consequences

**Positive:**
- Running cost CHF 0 while the Always Free terms hold; the fallback is ≈ CHF 6/month.
- Data stays in Zurich.
- A release (ADR-007 tag) is live within minutes with no manual step; the human gates are
  untouched because they all sit *before* the tag.
- The application is unchanged; SPEC-004's images and topology are reused, and the door to
  Kubernetes stays open.
- No credential for the production host exists anywhere outside the host.
- The application repository stays public and provider-free; a second installation or a
  provider switch is a new `deployments/<name>/` directory in `store-it-deploy`.
- Configuration changes are versioned and roll out exactly like releases.

**Negative / Trade-offs:**
- **Single point of failure.** One VM, one disk, no redundancy. Acceptable for the audience;
  the nightly backup bounds the damage to one day of data.
- **Oracle can change the terms unilaterally** (it halved the A1 allowance in June 2026 without
  an announcement) or reclaim the instance. Mitigations: Pay-As-You-Go, backups off-box, and a
  fallback provider with identical mechanics.
- **Operator duties land on the owner:** OS patches (`unattended-upgrades`), Docker updates,
  disk watching, certificate renewal (Caddy does it, but someone notices if it fails).
- **Brief downtime on every release** — the seconds compose needs to recreate `backend` and the
  time `migrate` takes. No blue/green.
- **Up to five minutes** between tag and rollout, by construction of the pull timer.
- Base-image updates (postgres, caddy, nginx) reach production only through a release: Renovate
  proposes them on `develop`, and the next `v*` tag ships them.
- No Kubernetes experience is gained; if the revisit triggers fire, that work starts then.
- **Two repositories share a contract.** A change to an environment variable, a port or the
  start ordering in `store-it` must be mirrored in `store-it-deploy`; the runtime contract
  document is the place where such a change is visible in review.
- The host holds a read-only deploy key for the private deployment repository.

---

## Layering Rules (for the Architecture Conformance Gate)

Not applicable — this ADR decides infrastructure, not code structure. ADR-001 rules stand.

---

## Relationship to other ADRs and specs

- **Closes the TODO left by ADR-003** ("managed offerings exist at every provider") in the
  negative: no managed PostgreSQL, for cost reasons.
- **Completes ADR-007:** how a tagged release reaches production is decided here.
- **Builds on SPEC-004:** its images, ordering guarantees and environment contract are reused;
  its out-of-scope list (TLS, registry, non-localhost hostnames) is exactly this ADR's scope.
- **Implemented by SPEC-005** (release images and runtime contract, application side) and by
  the `store-it-deploy` repository (topology, host, runbook — deployment side).
- On acceptance, `docs/project/tech-stack.md` (row *Runtime*) and `ARCHITECTURE.md` §7 are
  updated to say "single VM, Docker Compose; Kubernetes deferred" instead of "Kubernetes".
