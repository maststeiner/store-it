# Agent Run Log: develop branch red — Sonar S9383 (unawaited router.navigate)

> **Date:** 2026-10-05
> **Spec:** none — tech-debt/bug issue #199; the issue body is the frozen G1 input (established convention for review-derived items)
> **Persona(s):** developer
> **Model:** Claude Fable 5.1 (claude-fable-5-1)
> **Branch / PR:** `fix/sonar-s9383-unawaited-navigate` → PR (linked from #199)

---

## Task

Every push to `develop` since 2026-09-29 failed `3 · Frontend quality gate`. Found while verifying
the new Sonar token after #196: the Sonar branch gate reports reliability C from one `typescript:S9383`
finding (`storage-list-page.ts:42`, bare `this.router.navigate(...)`). Make the develop gate green.

## Plan

1. Confirm the single cause via the SonarCloud API (`qualitygates/project_status`, `issues/search`).
2. Apply the codebase's existing convention for ignored navigation promises (`void …`).
3. Run frontend tests and lint; open PR against `develop`.

## Key Decisions

- **`void`, not `await` or `.catch`.** `auth.service.ts`, `auth.interceptor.ts` and `join-page.ts`
  already mark ignored `router.navigate*` promises with `void`; S9383 accepts that as an explicit
  ignore. Making the handlers `async` would change method signatures for no behavioural gain, and
  a navigation rejection has nowhere useful to go here.
- **Only one line changed.** A first grep in the main checkout (which sits on `main`) showed three
  more bare `this.router.navigate(['/storages'])` statements in `storage-detail-page.ts`; on `develop`
  they already carry `void` (SPEC-007 work). The issue text was corrected accordingly — the develop
  tree now has no bare `router.navigate*` call left in `src/app`.
- **Why PRs stayed green while develop was red:** PR analyses rate only the diff; the branch
  analysis rates "new code" since the previous version (2026-07-16), which is effectively the whole
  frontend. The rule was activated in the Sonar Way profile around 2026-09-29. Nothing in the repo
  changed to cause this. Follow-up thought, not done here: the develop branch run's conclusion is
  currently not watched by anyone — a red integration branch went unnoticed for six days.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "sonar cube noch grün machen" | Go-ahead after the finding was reported |

## Outcome

- **Result:** one `void` prefix (`storage-list-page.ts:42`); frontend unit tests and lint green locally (see
  PR); develop gate expected green on the first push after merge.
- **Deviations from spec:** none (no spec — tech-debt convention).
- **Harness follow-up:** consider surfacing develop-branch run failures (e.g. a notification on
  `workflow_run` failure for `develop`), so a profile-side rule activation is seen the same day.
