# Agent Run Log: SPEC-012 mobile layout

> **Date:** 2026-10-24
> **Spec:** [docs/specs/SPEC-012-mobile-layout.md](../specs/SPEC-012-mobile-layout.md) — frozen at G1 the same day (Marcel Steiner, "ja") after a second draft
> **Persona(s):** analyst → architect → developer → qa (spec drafted from a screenshot analysis, two drafts, frozen by Marcel before any code)
> **Model:** Claude Fable 5.1 (claude-fable-5-1)
> **Branch / PR:** `feature/mobile-layout` → PR (issue #250)

---

## Task

Marcel's iPhone screenshot: the inline edit's right column outside the card and the screen, the
page scrolling sideways, icons too small to tap, and the item row breaking differently as soon as
an item carries tags. "Fix all of it and add tests so these blemishes cannot come back." Then, on
his question about established UI guidelines: reference them in the spec **and** add a binding UI
guideline to the repo ("ja, beides").

## Plan

1. Locate the causes in the stylesheet (not guess from the screenshot alone), write the spec
   with the observations and the causes side by side, G1.
2. Second draft on request: WCAG 2.2 / Apple HIG references, axe-core as a gate, a UI guideline
   as the harness fix; G1.
3. CSS first (responsive block rewritten with explicit grid areas), area classes in the
   template, then the Playwright `mobile` project with geometric assertions and axe.
4. Prove the tests bite by running them against the previous stylesheet.

## Key Decisions

- **Four causes, all CSS** (SPEC-012 O1–O4): auto-placement in the narrow `1fr 1fr` grid plus the
  tag control's min-content width; iOS focus zoom on 15.2 px inputs; 36 px icon buttons with a
  14 px glyph; only `.item-actions` pinned in the row grid. The fix uses **named grid areas** for
  the row, the inline edit and the add form, so a future field cannot shift its neighbours —
  this is now a rule in `ui-guidelines.md`.
- **No `overflow-x: hidden`** anywhere: it would hide the symptom from users and from the test.
- **One template change beyond classes:** the detail-head icon buttons are wrapped in
  `.detail-actions` so they wrap below a long title as a group instead of one by one (D7).
  The vitest specs query by role/label and were unaffected (224 green).
- **Contrast tokens instead of per-element colours** (found by axe, D9): `--accent-strong` for
  filled buttons and link text, `--expired-strong` for the danger button, `--text-soft` 0.62 → 0.72.
  Chips, dots, borders and the focus ring keep the lighter accent, so the desktop look stays.
  Visible on desktop as slightly darker buttons and secondary text — recorded in the spec as a
  conscious deviation from "desktop unchanged" (AC-08 is about sizes and placement).
- **Row tag chips stay 32 px, not 44** (D2): a 44 px chip row would double the row height; the
  same filter is reachable through the 44 px chips in the tag list under 🔍.
- **Test data through the API** (`/auth/csrf` + `X-XSRF-TOKEN`, then `POST /api/v1/storages` and
  `…/items`), not through the UI: the layout tests need three specific rows (no tags, one tag,
  ten 30-character tags on a 56-character name) and should not depend on the add-form flow they
  also test.
- **Playwright `mobile` project runs the layout spec only**; the desktop project ignores it. CI
  job `1b` picks both up without a workflow change (`npm run e2e`).

## Verification

| Check | Result |
|---|---|
| `ng test` | 224 passed / 23 files (unchanged — no logic touched) |
| `ng lint` · `prettier --check .` | clean |
| `npm run e2e` (local full stack: Postgres container, backend Release, `ng serve`) | 12 passed — 5 desktop, 7 mobile (390 × 844 and 320 × 568) |
| axe-core, WCAG 2.x A/AA, every visited state | 0 violations after three fixes: `.btn-primary` white on `--accent` 3.3 : 1 → `--accent-strong` 5.4 : 1; `.btn-ghost` `--text-soft` 4.1 : 1 → 5.5 : 1; `.sharing-link` 13.6 px → 16 px (desktop rule further down in the file won on specificity, now `.sharing-panel .sharing-link`) |
| **Regression proof** — `develop` stylesheet swapped in, mobile project re-run | **7 / 7 failed**: "document wider than viewport" (detail plain, search open, add form, inline edit), "tap targets below 44 px" (list), "controls below 16 px" (rename) — then the fixed stylesheet restored byte-identically |
| Screenshots at 390 px (detail, inline edit, add form with suggestions, list) | fields inside the card, icon group below the title, chips on their own line, quantity/date on one line |

Sandbox note: Chromium needed `sudo npx playwright install-deps chromium` (missing libglib) —
added to the test guidelines' local-run snippet. CI already runs `install --with-deps`.

**For the human G3 test (what the machine cannot see):** the feel of the 44 px targets on a real
iPhone, whether Safari still zooms into any field, the look of the darker buttons and secondary
text on desktop, and the row chips at 32 px.

## Review (G2, automated part) and first CI run

CodeRabbit (ASSERTIVE) posted two findings on `74e7295`, both taken; CI job `1b` failed once:

| Finding / failure | Fix |
|---|---|
| **CI 1b:** "320 inline edit, ten tags: document wider than viewport — received 328" (passed locally: the runner's Ubuntu fonts are wider) | the same defect CodeRabbit's Major described: an editable chip with a 30-character word cannot wrap → `.tag-chip { max-width: 100%; min-width: 0; overflow-wrap: anywhere }` (+ `> span { min-width: 0 }`); the fixture pads the long tags with `W` so chip width no longer depends on fonts |
| **Major:** long tag text must wrap inside editable chips at 320 px | see above |
| **Trivial:** the fixture had only `Ok` rows, so axe never saw the expired / expiring-soon status colours; "use strong text tokens for those selectors" | fixture gains one expired and one expiring-soon row (count 5); `--expired-strong` now `#a84456`, new `--expiring-strong #8f5e15` for the group heads and dates (both ≥ 4.8 : 1 on the tinted rows); dots, borders and tints keep the light tokens |

Lesson for the harness: a layout test that passes locally can still fail in CI through font
metrics — fixtures for "longest possible text" should use the widest glyph, not `x`.

## Amendment A1 (G3 feedback on the branch)

| Step | Result |
|------|--------|
| Cause | desktop `.item-row` grid `1fr 130px 150px 80px` with auto-placement: the tag cell took the quantity column, ✎/🗑 wrapped to a second line |
| Fix | `.item-row:not(.item-edit)` gets `grid-template-areas` ('name qty date actions' / 'tags . . .'), `row-gap: 0`, chips `margin-top: 6px`; the mobile selector is aligned so it still wins |
| Test | `expectActionsAligned` (x and width of `.item-actions`/`.item-qty` equal across rows, actions on the name line) in "detail plain", "320 detail" and a new 1280 px block with overflow + axe |
| Proof | previous stylesheet → 1280 block fails ("actions x, row 2"), phone test passes; new stylesheet → 8/8 mobile, 224 unit |
| Screenshots | desktop 1280 and phone 390 with rows with/without tags — icons in one column on every row |

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Asked for widely used UI guidelines to lean on | before G1 — led to the second draft |
| 2 | "ja, beides": standards in the spec **and** a repo UI guideline (plus axe-core) | G1 input |
| 3 | Froze the spec ("ja") | G1 |
| 4 | "review ist durch und build hat gefailed" — pointed at the CodeRabbit findings and the red `1b` | G2 |
| 5 | "edit und löschen icon nicht immer am gleichen ort, je nachdem ob es tags gibt" — the desktop row auto-placed its cells; the mobile spec had only pinned the narrow grid | G3 → amendment A1 (O5, D11, AC-15) |

## Outcome

- **Result:** PR open, awaiting G2/G3; amendment A1 (desktop row areas, AC-15, 1280 px test block) added after Marcel's first look on the branch
- **Deviations from spec:** `.detail-actions` wrapper (D7, noted in Technical Constraints);
  contrast tokens visible on desktop (D9/AC-08, noted)
- **Harness follow-up:** `docs/guidelines/ui-guidelines.md` (new), linked from
  `coding-guidelines.md` and `CLAUDE.md`; test-guidelines and tech-stack updated
