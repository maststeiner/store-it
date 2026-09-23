# Agent Run Log: CI builds the container images on every run (#156)

> **Date:** 2026-09-23
> **Spec:** none — tech-debt issue [#156](https://github.com/maststeiner/store-it/issues/156) is the frozen G1 input; it revisits SPEC-004 decision 7, recorded there as amendment A8
> **Persona(s):** developer
> **Model:** Claude Fable 5.1
> **Branch / PR:** `feature/ci-image-build-gate` → PR linked from #156

---

## Task

Close the detection gap found by the failed release `v0.1.0` (#155): nothing in CI built the
Dockerfiles, so a Renovate major bump of the .NET base images (#130, merged 2026-09-10) went
unnoticed for eleven days. #156 asked for a human decision first because SPEC-004 decision 7 had
kept the container stack out of CI on purpose.

## Plan

1. Present the options to Marcel (new build-only job · reuse the release build job as a
   callable workflow in dry-run · do nothing) with cost and consequences.
2. On his decision: add the job to `ci.yml`, pinned to the same action SHAs as `release.yml`,
   sharing its cache scopes; lint with actionlint 1.7.12.
3. Amend SPEC-004 decision 7 (A8), note the new job and the follow-up platform task in
   `docs/SETUP.md`, open the PR against `develop`.

## Key Decisions

- **Option 1 — a separate build-only job `1c · Container images build`** (Marcel, 2026-09-23:
  "1 umsetzen"). Build only, amd64 only, no push, no container started: catches the whole class
  of "Dockerfile no longer builds" without reopening the login question that motivated decision 7.
- **Not option 2 (reuse `release.yml`'s build job via `workflow_call`).** Less duplication, but it
  would rework the release workflow one day after it first proved itself; the three build steps
  are small enough to duplicate.
- **Always run, no path filter.** The job is meant to become a required status check on
  `develop`/`main`; a workflow-level `paths` filter would leave the check missing (never
  reported) on unrelated PRs and block them. It runs in parallel and is shorter than `1a`
  (mutation testing), so the critical path does not grow.
- **Same GHA cache scopes as the release** (`backend-amd64`, `web-amd64`): a release tag on `main`
  can read the cache written by the `develop` post-merge run, so the release build gets faster,
  not slower. PR runs write into PR-scoped caches, which GitHub isolates by design.
- **Required-check registration is left to Marcel** (`docs/SETUP.md` §3, unchecked `[platform]`
  item): GitHub only offers a check name after it has reported once.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | 2026-09-23: "führe mich durch punkt 3" → options presented → "1 umsetzen" | The decision was reserved for a human in #156 (revisits a frozen spec decision). |

## Outcome

- **Result:** PR opened against `develop`; actionlint 1.7.12 clean locally. The first CI run of the
  PR is itself the proof that the three images build (`v0.1.1` Dockerfiles, unchanged).
- **Deviations from spec:** SPEC-004 decision 7 changed knowingly — amendment A8 with the
  reason and the boundary (build yes, run no).
- **Harness follow-up:** none.
