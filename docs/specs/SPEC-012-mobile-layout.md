# Spec: Mobile layout — usable on a phone without sideways scrolling

> **Status:** Draft
> **Sprint:** 2026-S43
> **Author:** Claude Fable 5.1 (analyst agent), from Marcel Steiner's request and iPhone screenshot (issue #250, 2026-10-24)
> **Last updated:** 2026-10-24

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

## Observed on an iPhone (Marcel Steiner, 2026-10-24)

| # | Observation | Cause (analysis) |
|---|-------------|------------------|
| O1 | In the inline edit of an item the right column (unit, production date, ✓/✕) sits beyond the card and partly beyond the screen; the page scrolls sideways, the sticky header shifts with it. | `.item-edit` under 780 px is `1fr 1fr` with auto-placement; the tag control (input + non-wrapping *Add tag* button) sets a min-content width that widens column 1 until column 2 leaves the card. |
| O2 | Sideways scrolling on several views on a narrow screen. | O1, plus iOS Safari zooming into any input/select below **16 px** font size (ours are 0.95 rem = 15.2 px); the zoom leaves the page wider than the viewport. |
| O3 | Icons and buttons are hard to hit (✎, 🗑, header icon buttons). | `.icon-btn` is 36 × 36 px, the row actions render a 14 px glyph — below the 44 pt minimum of the Apple HIG / WCAG 2.5.5. |
| O4 | With tags on an item the quantity jumps next to the chips and the date drops to a third line; rows with and without tags look different. | `.item-row` under 780 px pins only `.item-actions`; the other cells are auto-placed, so an extra tag cell shifts everything after it. |

## Decisions taken by the draft (to be confirmed at G1)

| # | Decision | Rationale / alternative |
|---|----------|-------------------------|
| D1 | **"Narrow" = viewport width ≤ 780 px** (the existing breakpoint), **320 px is the smallest supported width** (iPhone SE 1st gen / small Android). Everything in this spec is verified at **320 px and 390 px** (iPhone 13–16 class). | One breakpoint keeps the stylesheet simple; 320 is the smallest phone still in use. Tablets in portrait (768 px) fall under the same rules. |
| D2 | **Tap targets ≥ 44 × 44 CSS px on narrow screens** for every button, link-button, select and chip-remove ✕; icon glyphs grow to about 18–20 px. Desktop (> 780 px) keeps the 36 px icon buttons. | Apple HIG / WCAG 2.5.5 (AAA) minimum; the visual design stays as approved on desktop. Tag chips on rows (filter) are text buttons: they get at least 32 px height and 8 px wrap gap — a full 44 px chip would double the row height; the same filter is reachable through the tag list under 🔍 with 44 px chips. |
| D3 | **No horizontal page scroll in any state**: storage list, storage detail (plain, search open with tag list, add form open, inline edit open — with 0 and with 10 tags —, sharing panel open, dialogs), about, admin statistics, login, join. The document's scroll width equals the viewport width. | This is the user-visible symptom; stating it for every view makes it testable with one assertion per state. Achieved by fixing the causes (O1/O2), **not** by hiding overflow — a hidden overflow would hide the regression from the test as well. |
| D4 | **Inline edit on narrow screens** is a card of stacked rows: name (full width) · amount + unit (side by side) · expiry + production (side by side) · tags (full width) · actions ✓/✕ right-aligned on their own row · error line (full width). Every field lies inside the card. The **add form** uses the same stacking with its labels, the *Add* / *Cancel* buttons side by side in one full-width row. | Mirrors the desktop column order top-to-bottom; two short fields per row keep the card compact. Explicit grid areas replace auto-placement so a later field cannot shift the others again. |
| D5 | **Item row on narrow screens**: row 1 name (wrapping) + actions on the right; row 2 the tag chips (only when the item has tags, wrapping, full width); row 3 quantity on the left, date on the right. The layout is identical with and without tags apart from the optional chip row. | The quantity/date pair stays where the eye expects it; the chip row is an insert, not a shift. |
| D6 | **Inputs, selects and the search field are 16 px on narrow screens** (`1rem`); desktop keeps 0.95 rem. | Removes the iOS focus zoom (O2) without a `maximum-scale` viewport hack, which would also disable the user's pinch zoom (WCAG 1.4.4). |
| D7 | **Detail head and header on narrow screens**: the storage title may wrap over several lines (long words break), the icon buttons wrap onto a second line when the title leaves no room; the header keeps logo, language select and session chip in one line within the viewport at 320 px. Storage cards use `minmax(min(280px, 100%), 1fr)` so a card never exceeds its container. | Nothing is hidden or truncated; a wrapped button row beats an overflowing one. |
| D8 | **Tests are Playwright, in a dedicated `mobile` project** (Chromium mobile emulation: 390 × 844, `isMobile`, `hasTouch`, device scale 3; one case at 320 × 568) running `e2e/mobile-layout.spec.ts` only; the existing desktop project keeps its tests. The assertions are **layout facts** read from the real browser: document scroll width ≤ viewport width (D3), bounding boxes of every form control inside the bounding box of its card (D4), tap targets ≥ 44 px (D2), computed input font size ≥ 16 px (D6), chip row below the name and quantity/date on one line (D5). | jsdom (the unit-test runner) has no layout engine, so these facts can only be asserted in a real browser. One project with its own spec keeps CI cost near the existing E2E job (same backend, same `ng serve`). WebKit is **not** added: the fixes are CSS the engines share, and Chromium is the only browser installed in CI (`1b`); a WebKit run can be a follow-up if a Safari-only defect ever appears. |

---

## Amendments (post-freeze)

| # | Date | Change |
|---|------|--------|
| | | |

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
- Swipe gestures or pull-to-refresh.

---

## Technical Constraints (from Architect Agent)

<!-- Filled in after G1 -->

- [ ] Layering: web only (`frontend/src/styles.scss`, two templates for the area classes,
      `frontend/playwright.config.ts`, `frontend/e2e/mobile-layout.spec.ts`); no API or backend change.
- [ ] Dependencies: none new (`@playwright/test` already present; Chromium mobile emulation is built in).
- [ ] ADR required: no.

---

## Verification

<!-- Filled in by QA Agent -->

| AC | Test | Status |
|----|------|--------|
| AC-01 | `mobile-layout.spec.ts` → "AC-01: no page is wider than the viewport" | ⬜ |
| AC-02 | → "AC-02: the storage detail stays inside the viewport in every state" | ⬜ |
| AC-03 | → "AC-03: inputs and selects are at least 16 px" | ⬜ |
| AC-04 | → "AC-04: every control of the inline edit lies inside its card" | ⬜ |
| AC-05 | → "AC-05: every control of the add form lies inside the form" | ⬜ |
| AC-06 | → covered by AC-02 (suggestion list open) | ⬜ |
| AC-07 | → "AC-07: every button has a 44 px tap target" | ⬜ |
| AC-08 | desktop project unchanged (existing `storage.spec.ts`); G3 visual check | ⬜ |
| AC-09 | → "AC-09: a row without tags is name/actions over quantity/date" | ⬜ |
| AC-10 | → "AC-10: a row with tags keeps quantity and date on one line below the chips" | ⬜ |
| AC-11 | → "AC-11: long names wrap instead of widening the page" | ⬜ |
| AC-12 | `playwright.config.ts` project `mobile`; CI job `1b` | ⬜ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ⬜ | | |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
