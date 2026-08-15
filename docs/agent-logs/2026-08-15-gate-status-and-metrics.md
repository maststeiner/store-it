# Agent Run Log: backfill the gate tables and the metrics baseline

> **Date:** 2026-08-15
> **Spec:** none — records/documentation task, requested directly by the orchestrator after the
> SPEC-004 merge. No `docs/specs/` document and no issue; nothing about the product changes.
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `docs/gate-status-and-metrics`

---

## Task

After PR #83 was merged, two records were found to be out of date: SPEC-004's Gate Status table
still showed G2 and G3 as unticked, and `docs/metrics.md` still held only data point #1 (SPEC-001)
although the document itself asks that every further feature be recorded the same way.

## What was found first

The gap was wider than the trigger suggested. **SPEC-001 and SPEC-003 have the same empty G2/G3
rows** — no spec in the repository has ever had its review or merge gate recorded. Filling in only
SPEC-004 would have produced a record where the most recent feature looks governed and the two
before it look abandoned, which is the opposite of the truth. All three were backfilled.

## Plan

1. Read the merge state, review history and merging user for PRs #5, #76, #83 from the GitHub API.
2. Read test counts, coverage and mutation score from the archived CI logs of each PR's final green
   run — not from memory, and not from the PR text.
3. Fill the three Gate Status tables, each with an evidence paragraph naming the PR, the CI run and
   the retroactive recording.
4. Add data points #2 and #3 to `docs/metrics.md` in the existing format, plus a trend table.

## Sources — every figure in this change

| Spec | PR | Merged by | CI run |
|---|---|---|---|
| SPEC-001 | [#5](https://github.com/maststeiner/store-it/pull/5) | Marcel Steiner, 2026-07-21 | [29864654653](https://github.com/maststeiner/store-it/actions/runs/29864654653) |
| SPEC-003 | [#76](https://github.com/maststeiner/store-it/pull/76) | Marcel Steiner, 2026-08-08 | [31251325271](https://github.com/maststeiner/store-it/actions/runs/31251325271) |
| SPEC-004 | [#83](https://github.com/maststeiner/store-it/pull/83) | Marcel Steiner, 2026-08-15 | [31876478530](https://github.com/maststeiner/store-it/actions/runs/31876478530) |

Test counts come from the `Passed: … Total: …` lines of the three backend test assemblies and the
`Tests … passed` line of vitest. Coverage comes from the coverlet and vitest tables in the same
logs. Mutation scores come from Stryker's `The final mutation score is …` line. The method was
validated against the existing data point #1: the log-derived backend total is 94 tests and the
frontend figure 86.7%, which is what `metrics.md` already recorded (`94`, `~87%`).

## Key Decisions

- **One existing number was corrected.** Data point #1 recorded backend "line coverage ~98%". That
  is the `StoreIt.Domain.Tests` run's Domain-only total (97.87%); the cross-module total from the
  service-test run — the figure that is comparable across features — is 99.21%. The entry now reads
  99.2%, and a new note at the end of the section says which of the two totals is meant, because the
  logs print both and the smaller one is the easier to grab.
- **G3 is recorded as "merged to `develop`", not merged outright.** `CLAUDE.md` reserves G3 for the
  merge to `main`, and no release PR to `main` has happened yet — `main` still sits at the Renovate
  config commit. Writing a bare ✅ would have claimed a release that does not exist.
- **Review-comment counts are labelled as comment counts, not defect counts.** Classifying 100+
  threads into defects would have meant judgement calls on someone else's review; where the agent
  logs state a defect explicitly (SPEC-003's HTTPS metadata gating and the `/health` 500;
  SPEC-004's `POSTGRES_PASSWORD` guard) it is recorded as a defect, and the rest is reported as what
  it is.
- **The trend gets two sentences of interpretation.** A table of falling coverage and a mutation
  score resting on its own gate reads as "fine" if nobody says otherwise. Both are margins that have
  narrowed since SPEC-001, and the note says so rather than leaving it to be discovered later.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Asked for both records to be backfilled after the #83 cleanup | The merge had left the gate table and the metrics baseline stale |

## Outcome

- **Result:** PR open, awaiting G2/G3
- **Deviations from spec:** none applicable — no spec governs this change
- **Harness follow-up:** none committed here, but worth a decision: three consecutive features
  merged with their gate rows left empty, so the record was reconstructed months later from API and
  CI history. The evidence survived, but only because CI logs are still retained. If the gate table
  is meant to be the accountability record, filling it belongs in the merge step rather than in a
  later archaeology pass.
