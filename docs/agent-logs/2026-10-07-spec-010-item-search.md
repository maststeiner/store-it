# Agent Run Log: SPEC-010 search field on the storage page

> **Date:** 2026-10-07
> **Spec:** [docs/specs/SPEC-010-item-search-in-storage.md](../specs/SPEC-010-item-search-in-storage.md) — frozen at G1 the same day (Marcel Steiner, "passt so")
> **Persona(s):** analyst → developer (spec drafted in the same session, frozen by Marcel before any code)
> **Model:** Claude Fable 5.1 (claude-fable-5-1)
> **Branch / PR:** `feature/item-search` → PR (issue #210)

---

## Task

A storage with many items is hard to scan. Add a search field to the storage page that filters
the item list while typing.

## Plan

1. Confirm the page already holds every item (it does: `GET …/items` returns the full list, the
   groups are a client-side computation) → pure client-side filter, no API change.
2. Spec with seven decisions (control, matching, groups, feedback, add/edit, Escape, no memory);
   G1 by Marcel.
3. Pure matching module + page wiring + i18n + styles; tests derived from the ACs.

## Key Decisions

- **Followed the frozen spec (D1–D7); nothing deviated.** Two details decided while implementing:
  - **Escape ownership (D6)** is implemented by `stopPropagation()` in the field's own Escape
    handler. The add form's Escape is a document-level `HostListener`; without stopping the
    event, one key press would clear the search *and* close the add form.
  - **"Searching" means at least one word**, not a non-empty string: `queryWords('   ')` is
    empty, so a whitespace-only field shows all items and no result line (EC-01), while the field
    stays open (the user is still typing).
- **The matching rule is a module without Angular** (`item-search.ts`) so AC-10 tests it
  directly; the component only composes signals: `items → filteredItems → groups`.
- **The browser's native search-cancel control is suppressed** in CSS: the page has its own
  clear button (AC-02 needs focus to return to the header button, which the native control
  cannot do), and two differently styled clear controls side by side would be clutter.
- **Two SPEC-007 tests** that enumerate the header buttons by label now include *Search items*
  — expected, as they assert the exact header composition.

## Verification

| Check | Result |
|---|---|
| `ng test` | 185 passed / 19 files (16 new: 6 rule, 10 page) |
| `ng lint` · `prettier --check .` | clean |
| `ng build --configuration production` | clean, no budget warning |
| Backend | untouched — no build needed |

**Not verified visually in a browser** (no signed-in session in the sandbox): the look of the
search bar under the header, the result line, and the feel of typing into a long list are for
the human G3 test.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Froze the spec with all seven proposed decisions unchanged ("passt so") | G1 |

## Outcome

- **Result:** PR open, awaiting G2/G3
- **Deviations from spec:** none
- **Harness follow-up:** none
