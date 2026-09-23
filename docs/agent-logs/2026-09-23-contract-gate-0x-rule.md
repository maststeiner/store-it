# Agent Run Log: contract gate applies the ADR-007 0.x rule (#166)

> **Date:** 2026-09-23
> **Spec:** none — tech-debt issue [#166](https://github.com/maststeiner/store-it/issues/166) is the frozen G1 input; the change is recorded as ADR-007 amendment A1
> **Persona(s):** developer
> **Model:** Claude Fable 5.1
> **Branch / PR:** `feature/contract-gate-0x-rule` → PR linked from #166

---

## Task

Resolve the contradiction inside ADR-007 found while writing the `v0.1.0` release PR (#152):
decision 2 keeps `/api/v1` unfrozen until the deliberate `v1.0.0`, decision 3 (and `ci.yml`)
take *any* `vMAJOR.MINOR.PATCH` tag as the breaking-change baseline — so `v0.1.1` had made the
gate blocking for a 0.x product whose only client ships in the same release.

## Plan

1. Lay out the three options (0.x rule · accept the freeze · label escape hatch) and let Marcel
   decide; record the decision in a tech-debt issue.
2. Change the baseline selection in the `2 · API contract gate` job; test all three paths
   locally with the real contracts (`v0.1.1` vs `develop`), a simulated breaking change and a
   simulated stable tag; actionlint.
3. Amend ADR-007 (A1), align `docs/SETUP.md` §3, open the PR against `develop`.

## Key Decisions

- **Option 1 — the 0.x rule** (Marcel, "1 umsetzen"). Baseline = latest tag with `MAJOR ≥ 1`.
  While only `0.x` tags exist the gate still runs oasdiff against the latest `0.x` tag, writes
  the markdown changelog into the job summary and emits **warning** annotations (oasdiff's
  `githubactions` format emits `::error`, downgraded with `sed`), but exits 0. So review sees
  every breaking change; only `v1.0.0` makes them fail — which is what ADR-007's rationale
  ("a deliberate human act") always meant.
- **Not "accept the freeze"**: breaking → `/api/v2` at 0.x would freeze a contract nobody
  outside the release depends on. **Not a PR label**: more mechanics and a habit-forming loophole.
- **Drift check untouched**, enforce path (`--fail-on ERR`) byte-identical to before.
- **The "release without committed contract" error stays a hard failure** in both modes — a
  tag without its contract breaks the baseline model regardless of 0.x.

## Verification (local, oasdiff 1.23.0)

| Case | Result |
|------|--------|
| develop contract vs `v0.1.1` (real state) | report mode, "No changes detected", exit 0, summary written |
| `/api/v1/storages` removed from the PR contract, 0.x | two `::warning` annotations, changelog in summary, **exit 0** |
| same, `STABLE_TAG` forced (simulated `v1.0.0`) | `2 changes: 2 error`, **exit 1** |
| unchanged contract, stable forced | "No changes detected", exit 0 |
| actionlint 1.7.12 | clean |

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | 2026-09-23: "gleich noch adr 007 anschauen" → options → "1 umsetzen" | Amending an accepted ADR is a human decision. |

## Outcome

- **Result:** PR opened against `develop`. Its own CI run exercises the report path (only 0.x
  tags exist; no contract change → "No changes detected" in the job summary).
- **Deviations from spec:** ADR-007 decision 3 changed knowingly (amendment A1).
- **Harness follow-up:** none. Side note for the worktree convention: after fetching a *tag*,
  `FETCH_HEAD` points at it — re-fetch `develop` before `git worktree add … FETCH_HEAD`
  (caught here before the first commit).
