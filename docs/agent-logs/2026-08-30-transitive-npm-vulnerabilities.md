# Agent Run Log: Resolve transitive npm vulnerabilities in the frontend lockfile

> **Date:** 2026-08-30
> **Spec:** none — tech-debt/security issue #117; the issue body is the frozen G1 input (established convention for review-derived items)
> **Persona(s):** developer
> **Model:** Claude Fable 5 (claude-fable-5)
> **Branch / PR:** `feature/frontend-lockfile-vuln-fixes` → PR (linked from #117)

---

## Task

Close the last open Renovate thread from 2026-08-20: 11 Dependabot alerts (4 high) open since
2026-08-19 with no Renovate security PR and no vulnerability section in the Dependency
Dashboard (#93).

## Plan

1. Re-check the alerts and the dashboard state via the GitHub API.
2. Test the standing hypothesis (missing *Dependabot alerts: read* on the Mend app) against an
   alternative: the packages might not be remediable by Renovate at all.
3. If it is a lockfile-only problem, fix it directly with an in-range `npm update`.

## Key Decisions

- **Root cause reattributed.** All six alerted packages (`js-yaml`, `ip-address`, `fast-uri`,
  `hono`, `@hono/node-server`, `postcss`) are transitive-only dev dependencies — none is in
  `frontend/package.json`. Renovate's `vulnerabilityAlerts` only remediates dependencies present
  in a package file; transitive npm lockfile-only remediation is out of scope for it. The Mend
  permission question is therefore moot for these alerts (and remains unverified — portal API is
  not reachable from the sandbox).
- **Fix as `npm update --package-lock-only`** of the six packages: every patched version is
  in-range for its dependents, so no package.json change and no major risk surface. The
  `@hono/node-server` 1.19.14 → 2.1.1 jump is in-range too (`@modelcontextprotocol/sdk` declares
  `^1.19.9 || ^2.0.5`).
- **`brace-expansion` taken along:** `npm audit` surfaced a fresh high advisory
  (GHSA-mh99-v99m-4gvg / GHSA-rgw5-rvv9-x895) that Dependabot had not yet alerted on; same
  in-range lockfile fix. Audit result after both updates: 0 vulnerabilities.
- **Follow-up decision left to the human** (in #117): enable GitHub Dependabot security updates
  for future transitive coverage, or keep this manual. Not enabled unilaterally — it changes who
  opens PRs in the repo.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | none — session continuation ("weitermachen wo wir zuvor waren") | |

## Outcome

- **Result:** lockfile-only change; `npm ci` clean, all 99 frontend unit tests pass locally,
  `npm audit` reports 0 vulnerabilities. PR opened against `develop`.
- **Deviations from spec:** none (no spec — tech-debt convention).
- **Harness follow-up:** none; the Renovate blind spot and the open tooling decision are recorded
  in #117 rather than in config, since no repo config can change Renovate's transitive behaviour.
