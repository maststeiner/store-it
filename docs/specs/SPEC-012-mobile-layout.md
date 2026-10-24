# Spec: Mobile layout — usable on a phone without sideways scrolling

> **Status:** Frozen (Gate 1) — approved by Marcel Steiner, 2026-10-24 (issue #250)
> **Sprint:** 2026-S43
> **Author:** Claude Fable 5.1 (analyst agent), from Marcel Steiner's request and iPhone screenshot (issue #250, 2026-10-24)
> **Last updated:** 2026-10-24 (implementation on `feature/mobile-layout`, verification table filled)

---

## User Story

As a **user on a phone** I want to **use store-it on a narrow screen without sideways scrolling,
with controls big enough to tap and every form field inside the screen** so that **the pantry
can be managed where it is used: in the kitchen, on the phone.**

---

## Context & Relationship to Existing Work

- The design system (`frontend/src/styles.scss`) has one breakpoint at **780 px**; below it the
  item forms fall back to a two-column grid and the item row to `1fr auto`. That fallback predates
  the tag control (SPEC-011) and the on-demand add form (#182).
- SPEC-001 defines the item row (name · amount · date · actions) and the item form; SPEC-010 the
  search bar and the result line; SPEC-011 the tag chips on rows, the tag list under the search
  field and the tag input in both forms. None of them states a requirement for narrow screens —
  this spec adds it without changing their behaviour.
- The web client is the only client (SPEC-002); no API change.
- **Standards:** the requirements below apply [WCAG 2.2](https://www.w3.org/TR/WCAG22/) level AA
  (reflow 1.4.10, resize text 1.4.4, target size 2.5.8; 2.5.5 AAA for the 44 px targets) and the
  [Apple Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines/)
  (44 pt targets, 16 px inputs). They are written down as the project's
  [`docs/guidelines/ui-guidelines.md`](../guidelines/ui-guidelines.md), introduced with this spec
  (D10), so that the Developer and Reviewer personas have a binding reference for every later UI change.

## Observed on an iPhone (Marcel Steiner, 2026-10-24)

| # | Observation | Cause (analysis) |
|---|-------------|------------------|
| O1 | In the inline edit of an item the right column (unit, production date, ✓/✕) sits beyond the card and partly beyond the screen; the page scrolls sideways, the sticky header shifts with it. | `.item-edit` under 780 px is `1fr 1fr` with auto-placement; the tag control (input + non-wrapping *Add tag* button) sets a min-content width that widens column 1 until column 2 leaves the card. |
| O2 | Sideways scrolling on several views on a narrow screen. | O1, plus iOS Safari zooming into any input/select below **16 px** font size (ours are 0.95 rem = 15.2 px); the zoom leaves the page wider than the viewport. |
| O3 | Icons and buttons are hard to hit (✎, 🗑, header icon buttons). | `.icon-btn` is 36 × 36 px, the row actions render a 14 px glyph — below the 44 pt minimum of the Apple HIG / WCAG 2.5.5. |
| O4 | With tags on an item the quantity jumps next to the chips and the date drops to a third line; rows with and without tags look different. | `.item-row` under 780 px pins only `.item-actions`; the other cells are auto-placed, so an extra tag cell shifts everything after it. |
| O5 | *(added at G3, A1)* On a wide screen ✎ and 🗑 are not at the same place in every row: as soon as an item has tags they drop to a second line on the left. | The desktop `.item-row` grid (`1fr 130px 150px 80px`) auto-places its cells; the tag cell takes the quantity column and pushes the actions out of the four columns. Same cause as O4, other breakpoint. |
| O6 | *(added at G3, A2)* The status hints on the storage cards ("3 expired", "2 expiring soon", "All fresh") are plain text in the default colour. | `storage-list-page.html` has used `chip expired` / `chip expiring` / `chip ok` since SPEC-001; `styles.scss` never got a rule for `.chips` or `.chip`. |

## Decisions taken by the draft (confirmed at G1, 2026-10-24)

| # | Decision | Rationale / alternative |
|---|----------|-------------------------|
| D1 | **"Narrow" = viewport width ≤ 780 px** (the existing breakpoint), **320 px is the smallest supported width** (iPhone SE 1st gen / small Android). Everything in this spec is verified at **320 px and 390 px** (iPhone 13–16 class). | One breakpoint keeps the stylesheet simple; 320 is the smallest phone still in use. Tablets in portrait (768 px) fall under the same rules. |
| D2 | **Tap targets ≥ 44 × 44 CSS px on narrow screens** for every button, link-button, select and chip-remove ✕; icon glyphs grow to about 18–20 px; adjacent targets keep ≥ 8 px spacing. Desktop (> 780 px) keeps the 36 px icon buttons (≥ 24 px, WCAG 2.5.8 AA, holds there too). | Apple HIG 44 pt / WCAG 2.5.5 (AAA); 2.5.8 (AA) is the floor everywhere; the visual design stays as approved on desktop. Tag chips on rows (filter) are text buttons: they get at least 32 px height and 8 px wrap gap — a full 44 px chip would double the row height; the same filter is reachable through the tag list under 🔍 with 44 px chips. |
| D3 | **No horizontal page scroll in any state**: storage list, storage detail (plain, search open with tag list, add form open, inline edit open — with 0 and with 10 tags —, sharing panel open, dialogs), about, admin statistics, login, join. The document's scroll width equals the viewport width. | WCAG 1.4.10 Reflow (320 px without horizontal scrolling). This is the user-visible symptom; stating it for every view makes it testable with one assertion per state. Achieved by fixing the causes (O1/O2), **not** by hiding overflow — a hidden overflow would hide the regression from the test as well. |
| D4 | **Inline edit on narrow screens** is a card of stacked rows: name (full width) · amount + unit (side by side) · expiry + production (side by side) · tags (full width) · actions ✓/✕ right-aligned on their own row · error line (full width). Every field lies inside the card. The **add form** uses the same stacking with its labels, the *Add* / *Cancel* buttons side by side in one full-width row. | Mirrors the desktop column order top-to-bottom; two short fields per row keep the card compact. Explicit grid areas replace auto-placement so a later field cannot shift the others again. |
| D5 | **Item row on narrow screens**: row 1 name (wrapping) + actions on the right; row 2 the tag chips (only when the item has tags, wrapping, full width); row 3 quantity on the left, date on the right. The layout is identical with and without tags apart from the optional chip row. | The quantity/date pair stays where the eye expects it; the chip row is an insert, not a shift. |
| D6 | **Inputs, selects and the search field are 16 px on narrow screens** (`1rem`); desktop keeps 0.95 rem. | Apple HIG input behaviour: removes the iOS focus zoom (O2) without a `maximum-scale` viewport hack, which would also disable the user's pinch zoom (WCAG 1.4.4). |
| D7 | **Detail head and header on narrow screens**: the storage title may wrap over several lines (long words break), the icon buttons wrap onto a second line when the title leaves no room; the header keeps logo, language select and session chip in one line within the viewport at 320 px. Storage cards use `minmax(min(280px, 100%), 1fr)` so a card never exceeds its container. | Nothing is hidden or truncated; a wrapped button row beats an overflowing one. |
| D8 | **Tests are Playwright, in a dedicated `mobile` project** (Chromium mobile emulation: 390 × 844, `isMobile`, `hasTouch`, device scale 3; one case at 320 × 568) running `e2e/mobile-layout.spec.ts` only; the existing desktop project keeps its tests. The assertions are **layout facts** read from the real browser: document scroll width ≤ viewport width (D3), bounding boxes of every form control inside the bounding box of its card (D4), tap targets ≥ 44 px (D2), computed input font size ≥ 16 px (D6), chip row below the name and quantity/date on one line (D5). | jsdom (the unit-test runner) has no layout engine, so these facts can only be asserted in a real browser. One project with its own spec keeps CI cost near the existing E2E job (same backend, same `ng serve`). WebKit is **not** added: the fixes are CSS the engines share, and Chromium is the only browser installed in CI (`1b`); a WebKit run can be a follow-up if a Safari-only defect ever appears. |
| D9 | **axe-core runs in the mobile tests**: every page state the `mobile` project visits is scanned with `@axe-core/playwright` (rule tags `wcag2a`, `wcag2aa`, `wcag21aa`, `wcag22aa`); **zero violations** is the assertion. A rule is excluded only with a comment naming the criterion and the reason, plus a tech-debt issue. | Turns the standard into a gate instead of a reading list: target size (2.5.8), contrast (1.4.3/1.4.11), labels and roles are checked by the same engine Lighthouse uses. New dev dependency `@axe-core/playwright` (Deque, MPL-2.0, no runtime footprint); justification recorded here and in the PR. |
| D10 | **A binding UI guideline** `docs/guidelines/ui-guidelines.md` is added and linked from the coding guidelines and `CLAUDE.md`: standards (WCAG 2.2 AA, Apple HIG, Material 3 for spacing), the layout rules of this spec (reflow, one breakpoint, intrinsic sizing, explicit grid areas, 16 px inputs, safe areas), target sizes, contrast, how layout is tested, and a reviewer checklist. | The repo had coding and test guidelines but none for the UI, so the Reviewer persona had no yardstick for layout — the tag field slipped through for that reason. The guideline is the harness fix (orchestrator loop: sharpen the harness, not only the output). |
| D11 | *(A1)* **The item row has fixed grid areas at every width.** Wide screens: name · quantity · date · actions in four columns on one line, the chip line (only when the item has tags) under the name in the first column. Narrow screens: D5 unchanged. Quantity, date and the actions never change column or line because of optional content. | Marcel at G3: the actions must always sit at the same place. Explicit areas instead of auto-placement, the same remedy as D4/D5 applied to the desktop grid; the UI guideline's rule "explicit grid areas" now reads "at every width". |
| D12 | *(A2)* **Storage-card status chips are styled**: pill with a dot in the light status colour (`--expired` / `--expiring` / `--accent`) and text in the strong one (`--expired-strong` / `--expiring-strong` / `--accent-strong`) on a light tint, wrapping inside the card. Not interactive, so D2 does not apply. | Marcel at G3: "gleich in dieser Story umsetzen". Same colour vocabulary as the group heads and item dates (D9), so the list and the detail read the same way; the strong tokens keep 4.5 : 1 on the tint. |

---

## Amendments (post-freeze)

| # | Date | Change |
|---|------|--------|
| A1 | 2026-10-24 | Marcel Steiner (G3 on the branch): ✎/🗑 move when a row has tags on a wide screen (O5). Adds D11, AC-15 and a 1280 px block in the mobile test file; `ui-guidelines.md` layout rule extended to every width. No change to any other criterion. |
| A2 | 2026-10-24 | Marcel Steiner (G3 on the branch): style the storage-card status chips that had no rule since SPEC-001 (O6). Adds D12 and AC-16; the list state of the mobile test seeds a fresh storage as well and asserts all three chips. |

---

## Acceptance Criteria (EARS Notation)

### No sideways scrolling

- [ ] AC-01: WHEN any page of the app (storage list, storage detail, about, admin statistics,
      login, join) is shown at a viewport width between 320 px and 780 px THE client SHALL render
      it so that the document is not wider than the viewport (no horizontal page scroll).
- [ ] AC-02: WHEN on the storage detail at a narrow width the search panel (with the tag list),
      the add form, the inline edit of an item (with 0 and with 10 tags), the sharing panel or a
      dialog is opened THE client SHALL still render the document not wider than the viewport.
- [ ] AC-03: WHEN an input or select is focused on a narrow screen THE client SHALL render it with
      a computed font size of at least 16 px (D6), so that iOS Safari does not zoom the page.

### Forms inside the screen

- [ ] AC-04: WHEN the inline edit of an item is open at a narrow width THE client SHALL lay the
      fields out as in D4 and every control (name, amount, unit, expiry, production, tag chips, tag
      input, *Add tag*, ✓, ✕) SHALL lie completely inside the edit card's bounding box, which
      itself lies inside the viewport.
- [ ] AC-05: WHEN the add form is open at a narrow width THE client SHALL lay it out as in D4
      with labels, and every control including *Add* and *Cancel* SHALL lie inside the form's
      bounding box, which lies inside the viewport.
- [ ] AC-06: WHEN the tag suggestion list opens in either form at a narrow width THE client SHALL
      render it within the viewport width.

### Tappable controls

- [ ] AC-07: WHEN the app is shown at a narrow width THE client SHALL render every button (header
      and detail-head icon buttons, row actions ✎/🗑, form buttons, ✓/✕, dialog buttons, chip
      remove ✕, the language select, the session chip) with a tap target of at least 44 × 44 CSS px
      (D2); row tag chips SHALL be at least 32 px high.
- [ ] AC-08: WHEN the app is shown at more than 780 px THE client SHALL keep the approved desktop
      sizes (36 px icon buttons) — the desktop appearance is unchanged.

### Stable rows

- [ ] AC-09: WHEN an item without tags is shown at a narrow width THE client SHALL render the row
      as name + actions on the first line and quantity (left) + date (right) on the second (D5).
- [ ] AC-10: WHEN an item with tags is shown at a narrow width THE client SHALL render the chips
      on their own line between the name line and the quantity/date line; the quantity and the
      date SHALL share one line exactly as for an item without tags; chips SHALL wrap inside the
      row when they do not fit.
- [ ] AC-11: WHEN an item name or storage title is longer than the available width THE client
      SHALL wrap it (breaking long words) instead of widening the row or the page (D7); the
      detail-head icon buttons SHALL wrap onto a further line when the title leaves no room.
- [ ] AC-15 *(A1)*: WHEN items with and without tags are shown at any viewport width THE client
      SHALL render the quantity, the date and the edit/delete actions of every row at the same
      horizontal position and on the line of the item name; the chips, when present, SHALL
      take their own line below the name without moving any other cell (D11).
- [ ] AC-16 *(A2)*: WHEN a storage card shows a status hint THE client SHALL render it as a chip
      (tinted pill with a status dot, text in the strong status colour) inside the card at every
      width, for the states expired, expiring soon and all fresh (D12).

### Accessibility gate and guideline

- [ ] AC-13: WHEN the mobile tests visit a page state (every state of AC-01/AC-02) THE axe-core
      scan with the WCAG 2.x A/AA rule set SHALL report zero violations (D9).
- [ ] AC-14: THE repository SHALL contain `docs/guidelines/ui-guidelines.md` (D10), linked from
      `docs/guidelines/coding-guidelines.md` and `CLAUDE.md`, and this spec's rules SHALL be
      consistent with it.

### Tests

- [ ] AC-12: THE repository SHALL contain a Playwright project `mobile` (D8) whose tests assert
      AC-01, AC-02 (every listed state), AC-03, AC-04, AC-05, AC-07, AC-09 and AC-10 as layout
      facts read from the browser, so that a regression in any of them fails CI job
      `1b · End-to-end`.

---

## Edge Cases

- EC-01: **Item with 10 tags of 30 characters** on 320 px → chips wrap over several lines inside
  the row; the row grows in height, never in width.
- EC-02: **Item name without spaces longer than the row** (`Dosentomaten230gBioQualitätAktion…`)
  → breaks inside the word (`overflow-wrap: anywhere`), the actions stay on the first line.
- EC-03: **Storage title longer than the detail head** → wraps; the icon buttons move to the next
  line as a group (D7). The rename input in the detail head is full width on narrow screens.
- EC-04: **Member view** (↩ *Leave* instead of 🗑) and **owner view** have the same number of
  buttons — both fit at 320 px with the wrapping rule.
- EC-05: **Error line in the inline edit** (`item.dates.missing`, `item.tags.tooMany`) → full
  width inside the card, below the actions.
- EC-06: **Tag suggestion dropdown** near the right edge → anchored to the tag control's full
  width, never beyond the viewport (AC-06).
- EC-07: **Sharing panel link row** (read-only link + *Copy*) at 320 px → the link field shrinks,
  the button keeps its 44 px target; the member rows wrap name and role above the actions when
  needed.
- EC-08: **Dialogs** (`.dialog`, max-width 420 px) at 320 px → already fluid; buttons ≥ 44 px, the
  challenge input 16 px.
- EC-09: **Landscape phone (e.g. 844 × 390)** → above the breakpoint: desktop layout, which fits
  at that width today. Not part of the test matrix.
- EC-10: **Desktop** (> 780 px) → nothing changes (AC-08); the desktop E2E project is the guard.

---

## UI Requirements (web)

- Breakpoint stays `@media (max-width: 780px)`; the narrow rules live in that block of
  `styles.scss` (single design-system file, as today).
- `.item-row` (narrow): named grid areas `"name actions" "tags tags" "qty date"`; `.item-tags`
  moves to area `tags` with `display: flex; flex-wrap: wrap`; cells with `min-width: 0`.
- `.item-edit` (narrow): named grid areas `"name name" "amount unit" "expiry production"
  "tags tags" "actions actions" "error error"`; the five inputs, `.item-edit-tags`, `.item-actions`
  and `.form-error` get their area through a class each (`.edit-name`, `.edit-amount`, …) — the
  template adds those classes, nothing else changes in the template.
- `.add-form` (narrow): same areas via the existing `.field` elements (`.field-name`, …, the
  tags field already spans), `.form-actions` as a full-width flex row.
- `.icon-btn`, `.item-actions button`, `.btn-*`, `.lang`, `.session-chip`, `.tag-chip-remove`
  (narrow): `min-width: 44px; min-height: 44px`; row glyphs `font-size: 18px`; `.tag-chip`
  (row) `min-height: 32px`; chip gap 8 px.
- Inputs/selects (narrow): `font-size: 1rem`.
- `.detail-head` (narrow): `flex-wrap: wrap`; `h1 { min-width: 0; overflow-wrap: anywhere }`;
  `.item-name`, `.storage-name`: `overflow-wrap: anywhere`.
- `.storage-grid`: `minmax(min(280px, 100%), 1fr)`.
- No `overflow-x: hidden` on `html`/`body`/`main` (D3 — the symptom must stay visible to the test).
- No new strings; i18n untouched.

---

## Out of Scope

- A native or PWA app, offline mode, home-screen installation beyond the existing icon.
- A mobile navigation (hamburger): `nav` has one link and is hidden under 780 px today; the logo
  leads to the storage list.
- Redesign of the desktop layout, dark mode, landscape-specific layouts (EC-09).
- WebKit/Safari in CI (D8) and visual-regression screenshots (pixel diffs) — the assertions are
  geometric facts, not images.
- Fixing axe findings on **desktop-only** states or pages the mobile tests do not visit: the scan
  runs where the mobile tests go; a desktop axe run is a possible follow-up.
- Contrast changes to the approved desktop palette beyond what axe flags on the scanned states.
- Swipe gestures or pull-to-refresh.

---

## Technical Constraints (from Architect Agent)

<!-- Proposed before G1, confirmed during implementation; deviations from the proposal marked ⚠ -->

- [x] Layering: web only — `frontend/src/styles.scss` (responsive block rewritten, wrap rules,
      two contrast tokens), `storage-detail-page.html` (area classes `edit-*` / `field-*`, the
      `.detail-actions` group — ⚠ one wrapper `div` more than the draft said, so the icon buttons
      wrap as a group, D7), `frontend/playwright.config.ts` (project `mobile`),
      `frontend/e2e/mobile-layout.spec.ts`; no API or backend change, no new strings.
- [x] Dependencies: **one new dev dependency** `@axe-core/playwright` ^4.14.0 (D9; Deque,
      MPL-2.0, test-time only); `@playwright/test` already present, Chromium mobile emulation is
      built in. CI job `1b` needs nothing new (`npx playwright install --with-deps chromium` already
      installs the browser's system libraries).
- [x] Colour tokens (D9, found by axe and the G2 review): `--accent-strong #2b766e` for filled
      buttons and link text (5.4 : 1 on white; `--accent` gave 3.3 : 1), `--expired-strong #a84456`
      for the danger button, the *expired* group head and overdue dates (5.8 : 1 on white, 5.0 : 1
      on the tinted row), `--expiring-strong #8f5e15` for the *expiring soon* group head and dates
      (5.6 : 1 / 4.8 : 1; `--expiring` gave 2.9 : 1), `--text-soft` alpha 0.62 → 0.72 (5.5 : 1 on
      white, was 4.1 : 1). Tints, dots, borders and the focus ring keep `--accent` / `--expired` /
      `--expiring`. ⚠ Visible on desktop too — a contrast correction, not a layout change (AC-08 is
      about sizes and placement).
- [x] Tag chips wrap their text (`max-width: 100%; overflow-wrap: anywhere`): a 30-character tag
      without spaces (SPEC-011 D4) otherwise widens the card at 320 px — CI job `1b` caught it on
      Ubuntu fonts (328 px document) where the local run had passed; the fixture now pads the long
      tags with the widest glyph so the result no longer depends on the runner's fonts.
- [x] ADR required: no.

---

## Verification

<!-- Filled in by QA Agent -->

| AC | Test | Status |
|----|------|--------|
| AC-01 | `mobile-layout.spec.ts` → "AC-01: no page is wider than the viewport (storage list, about, join, login)"; "… at 320 px" (`expectNoHorizontalOverflow`) | ✅ |
| AC-02 | → "AC-02 / AC-07 / AC-09 / AC-10: … plain state", "AC-02 / AC-03 / AC-05: the add form …" (incl. suggestions open), "AC-02 / AC-04: the inline edit (with 0 and with 10 tags) …", "AC-02: search panel with the tag list, sharing panel and the delete dialog", "… at 320 px" | ✅ |
| AC-03 | → `expectInputFontSizes` in the add form, inline edit, search, sharing and rename states | ✅ |
| AC-04 | → "AC-02 / AC-04: the inline edit (with 0 and with 10 tags) lies inside its card" (`expectInside` for every control, ✓, ✕, chips); 320 px variant | ✅ |
| AC-05 | → "AC-02 / AC-03 / AC-05: the add form lies inside its card, 16 px controls"; 320 px variant | ✅ |
| AC-06 | → add-form test: `.tag-suggestions` within the viewport, state "add form with suggestions" | ✅ |
| AC-07 | → `expectTapTargets` in every state (buttons, selects, options, session chip ≥ 44; row chips ≥ 32) | ✅ |
| AC-08 | desktop project (`storage.spec.ts`, `auth.spec.ts`) green and untouched; visual check at G3 | ✅ · G3 ⬜ |
| AC-09 | → plain-state test: name/actions on one line, quantity left + date right on the next | ✅ |
| AC-10 | → plain-state test: chips start under the name, quantity/date share one line below the chips | ✅ |
| AC-11 | → plain-state test (long name, ten long tags grow the row in height only); "AC-11 / EC-03: a long storage title wraps and the icon buttons move below it" | ✅ |
| AC-12 | `playwright.config.ts` project `mobile` (`testMatch` mobile-layout.spec.ts; desktop project ignores it); CI job `1b` runs both | ✅ · ⏳ CI |
| AC-13 | → `expectAccessible` (axe, tags wcag2a/wcag2aa/wcag21aa/wcag22aa) in every visited state — three colour-contrast findings fixed on the way (see Technical Constraints) | ✅ |
| AC-14 | `docs/guidelines/ui-guidelines.md`, linked from `coding-guidelines.md` and `CLAUDE.md` | ✅ |
| AC-15 | → `expectActionsAligned` (same x and width of `.item-actions` and `.item-qty` across all five rows, actions on the name line) in "detail plain", "320 detail" and the new "wide viewport (1280 px)" block, which also runs the overflow and axe checks on the desktop layout | ✅ |
| AC-16 | → `expectStatusChips` (all three chips visible, inside their card, non-transparent background, pill radius) in "storage list" and "320 storage list"; axe covers the text contrast | ✅ |

**Regression proof (test effectiveness):** with the `develop` stylesheet swapped in, all 7 mobile
tests fail — document wider than viewport (detail, search, add form, inline edit), tap targets
below 44 px (list), controls below 16 px (rename). Local run 2026-10-24, documented in the agent log.
A1: with the stylesheet of the previous commit the 1280 px block fails at "actions x, row 2" while
the phone row test still passes — the desktop defect was invisible to the narrow-only tests.

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ | 2026-10-24 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
