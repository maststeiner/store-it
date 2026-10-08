# Agent Run Log: frontend SonarCloud findings before v0.4.0 (#218)

> **Date:** 2026-10-08
> **Spec:** none — tech-debt item; [issue #218](https://github.com/maststeiner/store-it/issues/218) is the frozen G1 input (repository convention)
> **Persona(s):** developer
> **Model:** Claude Fable 5.1 (claude-fable-5-1)
> **Branch / PR:** `fix/sonar-frontend-smells` → PR (issue #218); then merged into `release/v0.4.0` (#213)

---

## Task

Marcel: "auf SonarQube gibt es im Frontend noch 1 Reliability und 8 Maintainability issues" — close them
so that v0.4.0 ships without open findings.

## Findings and fixes

| Sonar | Finding | Fix |
|---|---|---|
| S6819 ×6 | `role="status"` on `<p>`/`<span>` status lines (About ×2, storage search result, admin loading, join loading, sign-in notice) | `<output>` elements — the implicit `status` role; the five that replaced a `<p>` carry the new `.status-block` utility (`display: block; margin: 1em 0`, declared *before* the per-class margin rules so those keep winning) |
| S6819 | `role="img"` on the shared-storage badge (an emoji span with `aria-label`) | the emoji is decorative (`aria-hidden`), the accessible name is a `.sr-only` text node; `title` stays for sighted hover |
| S8786 | `runtimeVersionOf` used `/(\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?)/` — flagged for super-linear backtracking | no regex: `FrameworkDescription` is "<product> <version>", the version is the last whitespace token; a single-token string is shown as given |
| S2699 (Blocker) | SPEC-010 AC-06 test only called `http.verify()` | `expect(() => http.verify()).not.toThrow()` — same check, now an assertion |

Three specs asserted `getAttribute('role') === 'status'`; they now assert `tagName === 'OUTPUT'`. The
list-page badge test reads the `.sr-only` text instead of `aria-label`.

## Verification

`ng test` 202 passed / 22 files · `ng lint` clean · `prettier --check .` clean · production build clean.
SonarCloud's PR analysis is the authority for "0 open issues" — checked on the PR after the push.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Asked to include the Sonar fixes in the pending release | release scope |

## Outcome

- **Result:** PR open against `develop`; after the merge, `develop` is merged into `release/v0.4.0` so #213 carries it
- **Deviations from spec:** n/a
- **Harness follow-up:** none. Observation: S6819 is a newer rule in the Sonar Way profile — the six `role="status"` lines passed every earlier PR gate; worth remembering that **PR analyses only flag changed lines**, so a newly activated rule surfaces on old code only in the branch analysis.
