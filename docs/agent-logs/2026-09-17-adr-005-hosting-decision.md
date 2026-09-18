# Agent Run Log: ADR-005 hosting decision and SPEC-005 draft

> **Date:** 2026-09-17
> **Spec:** [SPEC-005](../specs/SPEC-005-release-images-and-runtime-contract.md) (Draft) — implements [ADR-005](../architecture/ADR-005-hosting-deployment.md) (Proposed)
> **Persona(s):** analyst, architect
> **Model:** Claude Fable 5.1
> **Branch / PR:** `feature/adr-005-oracle-hosting` → PR linked from issue #17

---

## Task

Marcel asked how store-it could be hosted online for free or nearly free (a Hostpoint account
exists), added that the installation must update itself whenever new images are published, and
then decided for Oracle Cloud Always Free and asked to prepare everything for it.

## Plan

1. Establish the stack facts from the repo (SPEC-004 images, compose ordering, ADR-007 release
   tags, no registry publishing in CI, no forwarded-headers handling in `Program.cs`).
2. Check current provider facts on the web (Hostpoint runtimes, Hetzner prices after the 2026
   increases, Render/Neon free tiers, Azure Container Apps free grant, Oracle Always Free limits
   and reclaim behaviour, Watchtower's archival).
3. Write ADR-005 as **Proposed** — the decision is Marcel's to accept.
4. Write SPEC-005 as **Draft** with the open decisions listed, and ask for the freeze
   separately. No implementation before G1.
5. Link ADR-005 from `ARCHITECTURE.md` §9 without touching the deployment TODO in §7 (that
   is AC-23 of the spec, on acceptance).

## Key Decisions

- **Hostpoint is not a runtime option** (PHP/MySQL shared hosting, Managed Flex Server without
  containers or .NET). It keeps the domain/DNS role. Recorded in the ADR context so the
  question is not re-opened.
- **Oracle Always Free with Pay-As-You-Go upgrade and a CHF 1 budget alert.** Marcel explicitly
  preferred Oracle over the Hetzner recommendation (≈ CHF 6/month); the ADR keeps Hetzner as the
  named fallback with identical mechanics. The June 2026 halving of the A1 allowance (2 OCPU /
  12 GB) is recorded as a known risk.
- **Pull-based updates via a systemd timer running `compose pull && up -d`** rather than
  Watchtower (no knowledge of the `migrate` → `backend` ordering; upstream archived December
  2025) or SSH push from Actions (inbound SSH and a VM credential in GitHub). The compose
  ordering from SPEC-004 gives migration-before-API for free.
- **Compose, not Kubernetes, with explicit revisit triggers** (second environment, second
  node, second maintainer). The 12-factor shape is kept so the move stays possible.
- **Forwarded headers via `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`**, not code — with the
  caveat (SPEC-005 EC-04) that the switch trusts any proxy and is safe only because `backend`
  is never published on a host interface.
- **Multi-arch images (arm64 + amd64)** proposed as default (D3) so published images also run
  on x86 laptops and a fallback host; listed as an open decision because arm64-only is simpler.
- **Separate private deployment repository `store-it-deploy`** (Marcel's requirement, see
  intervention 3): the application repository publishes images plus a runtime contract
  (`docs/operations/runtime-contract.md`) and carries no hostname, provider or bucket; the
  deployment repository holds compose, Caddy, timer, backups, runbook and one
  `deployments/<name>/` directory per installation. The host pulls that repository with a
  read-only deploy key in the same timer run that pulls images, so configuration rolls out like
  a release. SPEC-005 was rescoped to the application side (sections A–C), the spec file renamed,
  and D1/D4/D5 moved out of this repo's spec.

- **Finding while building the deployment topology (2026-09-18):** `frontend/nginx.conf`
  overwrites `X-Forwarded-Proto` with nginx's own `$scheme` (`http` inside the container), so
  the framework switch alone would still yield `http://` redirect URIs behind Caddy. Added
  AC-09a to SPEC-005 (pass an upstream `X-Forwarded-Proto` through, keep `$scheme` as default);
  the deployment repository does not work around it, so the contract stays in one place.
- **Deployment repository built out on 2026-09-18** ("weitermachen"): `compose.yaml` with
  `db`/`caddy` profiles, Caddyfile, systemd timers, `update/backup/restore/bootstrap` scripts,
  runbook, Renovate, validate workflow. Validated locally (`compose config` with and without
  profiles, shellcheck, `caddy validate`). The application side stays untouched until G1.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "warum nicht Oracle Cloud Always Free" — challenged the Hetzner-first ranking | The ranking weighed reliability risk over cost; Marcel weighs cost higher. ADR written for Oracle, Hetzner demoted to fallback. |
| 2 | "es soll automatisch aktualisiert werden, wenn neue images zur Verfügung stehen" | Added as constraint 2 in the ADR; ruled out Render's image-based services (no auto-deploy) for this reason. |
| 5 | 2026-09-18: "ADR-005 akzeptiert." | Status set to Accepted; ARCHITECTURE.md §9 row updated. The wider doc updates (tech stack, §7, README, threat model) stay with SPEC-005 AC-13 — the spec is not frozen yet. |
| 4 | 2026-09-18: "würde es auch gehen, wenn ich die Datenbank bei mir auf einem NAS laufen lassen würde?" → "ok, dann lassen wir es so" | Assessed: DB-only on the NAS combines both sites' failure modes plus latency; whole stack on the NAS is a viable alternative deployment. Marcel keeps the Oracle plan; both options recorded in the ADR rationale table. |
| 3 | "für das deployment ein eigenes Repo machen mit der Konfiguration … Auch wenn dann mehrere deployments gemacht werden möchten" | First draft had `compose.prod.yaml`, runbook and hostnames inside `store-it`. Added ADR constraint 5 and decision 1 (two repositories), rescoped SPEC-005 to images + runtime contract, created the private `store-it-deploy` skeleton. |

## Outcome

- **Result:** ADR-005 (Proposed) and SPEC-005 (Draft) committed in PR #145; the private
  `store-it-deploy` repository exists as a skeleton. Freeze and acceptance requested from
  Marcel. Implementation (release workflow, runtime contract; deploy compose and runbook in the
  other repository) starts only after G1.
- **Deviations from spec:** n/a (no implementation yet).
- **Harness follow-up:** none.
