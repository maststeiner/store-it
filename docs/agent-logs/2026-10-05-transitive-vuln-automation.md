# Agent Run Log: Close the transitive npm vulnerability blind spot

> **Date:** 2026-10-05
> **Spec:** none — tech-debt/security issue #192; the issue body is the frozen G1 input (established convention for review-derived items)
> **Persona(s):** developer, AI steward
> **Model:** Claude Fable 5.1 (claude-fable-5-1)
> **Branch / PR:** `feature/transitive-vuln-automation` → PR (linked from #192)

---

## Task

Five Dependabot alerts (three high) were open again, all transitive-only dev dependencies in
`frontend/package-lock.json` — the exact pattern of #117 (August) and the `qs` pair (September).
Fix them, and close the decision #117 had left open: how such alerts get remediated without a
human noticing them first.

## Plan

1. Confirm the alerts are in-range lockfile-only fixes (same analysis as #117).
2. Fix them with `npm update --package-lock-only`, verify with `npm audit`, `npm ci`, tests, build.
3. Implement both automation options from #117, as decided by Marcel: Renovate
   `lockFileMaintenance` (routine) **and** GitHub Dependabot security updates (alert-driven).
4. Update `docs/SETUP.md` so the platform state is readable from the repo again.

## Key Decisions

- **Both options, not one.** They cover different failure modes. `lockFileMaintenance` is a weekly
  routine that keeps transitive drift from accumulating, inside the tool that already owns
  dependency updates. Dependabot security updates react to an alert within hours, but only ever to
  alerts. Alone, the first leaves a window of up to a week for a serious advisory; alone, the
  second leaves everything that is not (yet) an advisory. Together, Renovate stays the owner of
  version updates and Dependabot PRs should be rare.
- **`lockFileMaintenance` schedule written out as an eight-hour window** (Sunday 22:00 to Monday
  05:59 Europe/Zurich, inside the nightly window), not the preset default `before 4am on monday`
  (a four-hour window). Same reasoning as the nightly window on 2026-08-20: Mend's four-hourly
  cadence can miss a four-hour window indefinitely.
- **Automerge for lock file maintenance.** The result is a lockfile-only change that every CI gate
  still verifies (unit tests, E2E, quality gates, Trivy, dependency review). Same documented G3
  exception as minor/patch. A human still sees it: the dashboard lists it and the merge shows up
  on `develop`.
- **Dependabot security updates enabled via API** (`PUT /repos/{owner}/{repo}/automated-security-fixes`),
  not via a `dependabot.yml`. A `dependabot.yml` would additionally switch on Dependabot *version*
  updates, which is Renovate's job and would produce duplicate PRs. The repo setting alone is
  exactly the lockfile-only security remediation we want.
- **The SETUP.md item "Vulnerability PRs: still to verify" is closed as resolved**, with the
  corrected cause from #117: Renovate's `vulnerabilityAlerts` only remediates packages that appear
  in a package file. The Mend permission hypothesis remains unverified and is irrelevant for the
  transitive case.
- **Not touched:** nothing in the backend. The .NET side has no lockfiles (`packages.lock.json`
  is not used), so `lockFileMaintenance` is effectively npm-only here, and no backend alert exists.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "zuerst dependabot, was ist da genau noch offen?" — asked for the state of the open thread | Session steering after PR #123 (vitest 5) was merged |
| 2 | "1 und 2 umsetzen" — chose both automation options | Closed the decision left open in #117 |

## Outcome

- **Result:** `npm audit` 0 vulnerabilities (was 5 alerts: browserslist #22, fast-uri #27/#28,
  baseline-browser-mapping #26, brace-expansion #31). `npm ci` clean, 160 frontend unit tests
  pass, `ng build` ok. `lockFileMaintenance` added to `renovate.json` with explicit schedule and
  automerge; Dependabot security updates enabled (`enabled: true, paused: false`); `docs/SETUP.md`
  §3 and §4 updated. PR opened against `develop`.
- **Deviations from spec:** none (no spec — tech-debt convention).
- **Harness follow-up:** watch the first Sunday-night run — a `chore(deps): lock file maintenance`
  PR should appear and automerge once CI is green. If Dependabot and Renovate ever open PRs for
  the same lockfile change, the Renovate one wins and the Dependabot one is closed; that would be
  the signal to tighten the Dependabot side, not to drop it.
