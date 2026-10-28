# Agent Run Log: CI 1b red since 2026-10-27 — mobile row actions follow the date width

> **Date:** 2026-10-28
> **Spec:** none — bug issue [#261](https://github.com/maststeiner/store-it/issues/261); the acceptance criterion is SPEC-012 AC-15 (D11), the test `expectActionsAligned` in `frontend/e2e/mobile-layout.spec.ts`
> **Persona(s):** qa → developer (diagnosis from the CI logs and the Playwright report, then a one-line stylesheet fix)
> **Model:** Claude Fable 5.1 (claude-fable-5-1)
> **Branch / PR:** `fix/mobile-actions-align` → PR (issue #261)

---

## Task

Marcel: "es gibt mehrere prs die failen". Three Renovate PRs (#257 postgres digest, #259 sonar
action digest, #260 node 26) and the nightly `develop` runs were red; PR #253 (docs only, run of
2026-10-25) was green.

## Plan

1. Read the check lists of all open PRs — find the common failing job instead of looking at the
   PRs' diffs.
2. Compare the last green and the first red run of the **same** `develop` commit `b2183c1`:
   environment (runner image, browsers, SDKs, service image) and the Playwright report.
3. Reproduce locally with the full stack, prove the cause, fix, re-run, open the PR.

## Key Decisions

- **Not the PRs' fault.** All three fail in the same job `1b · End-to-end (full stack)` with the
  same two assertions (`detail plain: actions x, row 4` and `320 detail: actions x, row 4`,
  received 8.421875 against < 1.5). `develop` at `b2183c1` passed on 2026-10-26 22:31 and failed on
  the scheduled runs of 2026-10-27 and 2026-10-28 with identical runner image
  (`ubuntu-26.04/20261012.163`), Chromium 156.0.8078.4, Node 24.21.0, .NET SDK 10.0.403 and
  Postgres digest `898c60cb…`. Only the date changed.
- **Cause: the phone row's `auto` column follows the date, per row.** The narrow layout of
  `.item-row:not(.item-edit)` is `minmax(0, 1fr) auto` with areas `name actions` / `tags tags` /
  `qty date`. The second column holds the date and the ✎/🗑 actions; its width is the wider of
  the two, and every row is its own grid. On the runner's monospace (`DejaVu Sans Mono`, ≈ 8.4 px
  per glyph at 0.86 em) a date is wider than the two 44 px buttons, so the column is as wide as
  the date text. The fixture seeds "Plain pasta" with `isoDate(400)`: on 2026-10-26 that renders
  as `Nov 30, 2027`, from 2026-10-27 as `Dec 1, 2027` — one glyph less than row 0's
  `Oct 27, 2026`, hence 8.4 px. The stretched `.item-actions` cell therefore starts 8.4 px further
  right in that row, and `expectActionsAligned` (AC-15) compares the cell's x across rows.
- **The buttons were visually right-aligned** (`justify-content: flex-end` inside the cell), so no
  user saw the icons jump; but the cell's x — and with it the spec's "same place in every row" —
  depended on which day the calendar showed. The failure would have cleared itself on
  2026-11-05 (`isoDate(400)` = Dec 10) and come back with the next single-digit day in any of
  the five seeded offsets.
- **Fix in the stylesheet, not in the test.** `justify-self: end` on `.item-actions` in the
  narrow block: the cell shrinks to its two buttons at the right edge of the row, so x and width
  are the same in every row whatever the date's width. Making the fixture's dates all two-digit
  would have hidden a real rule violation instead of fixing it; the test keeps biting.
- **Guideline, not just a comment:** `ui-guidelines.md`'s rule "explicit grid areas at every
  width" gains the clause that a cell in an `auto` column that must keep its x is justified to
  the edge, not stretched.
- **The spec file is untouched.** SPEC-012's verification and gate tables are being filled by
  PR #253 on another branch; AC-15's test mapping does not change.

## Verification

| Check | Result |
|---|---|
| Reproduction on unchanged `develop`, local full stack (Postgres `18-alpine@898c60cb…` container, backend Release in Development mode, `ng serve`), Playwright `mobile` project | **passes** with the sandbox's monospace (date narrower than 88 px) — after `sudo apt-get install fonts-dejavu-core` (the runner's font): **2 failed**, `actions x, row 4`, received **8.421875**, byte-identical to CI |
| Same stack with the fix | `--project=mobile`: 8 passed; `npm run e2e` (both projects): 13 passed |
| `ng lint` · `prettier --check` | clean |
| `ng test` | 224 passed / 23 files (unchanged — no logic touched) |

Date proof from the CI timestamps: first red run 2026-10-27 03:25 UTC (#257); `date -d
2026-10-26 +400 days` = Nov 30, 2027, `2026-10-27 +400 days` = Dec 1, 2027.

**For the human G3 test (what the machine cannot see):** on a phone, open a storage whose
items have dates with one- and two-digit days (and one without expiry date, which shows the
longer "produced on" text) — ✎ and 🗑 in one column, flush right, on every row.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "es gibt mehrere prs die failen" | trigger — no further input during the run |

## Outcome

- **Result:** PR open, awaiting G2/G3. Once merged, the three Renovate PRs need a rebase or a
  re-run of CI (Renovate rebases automatically on conflict, otherwise "Rebase/retry" in the PR
  checkbox) before they go green.
- **Deviations from spec:** none — AC-15 unchanged, now true at every date.
- **Harness follow-up:** `docs/guidelines/ui-guidelines.md` extended (one clause). Lesson for the
  harness: a layout test that passes locally can still fail in CI through font metrics
  (second time after SPEC-012's first CI run) — reproduce with the runner's font before
  reasoning about the CSS.
