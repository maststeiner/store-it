# Spec: Search field on the storage page — filter the item list while typing

> **Status:** Frozen (Gate 1) — approved by Marcel Steiner, 2026-10-07 (issue #210)
> **Sprint:** 2026-S41
> **Author:** Claude Fable 5.1 (analyst/developer agent), from Marcel Steiner's request (issue #210, 2026-10-07)
> **Last updated:** 2026-10-07 (implementation on `feature/item-search`, verification table filled)

---

## User Story

As a **user with a storage that holds many items** I want **a search field on the storage page
that filters the item list as I type** so that **I find an item without scrolling through
everything**.

---

## Context & Relationship to Existing Work

- SPEC-001 built the storage page: items grouped by the API-computed `expiryStatus`
  (*Expired / Expiring soon / Others*, AC-10), and listed "Search / filtering — later" under Out of
  Scope. This spec is that follow-up.
- **The page already holds every item of the storage.** `GET /api/v1/storages/{id}/items` returns
  the full list; grouping is a pure client-side computation over it. Filtering by name is the
  same kind of presentation step — no business rule moves to the client (ADR-002: the server owns
  expiry and validation rules, the client arranges what it is given). **No API or contract
  change**, the ADR-007 gate sees nothing.
- The page header is a row of icon buttons (*rename*, *new item*, *share / members*, *delete /
  leave*); the add form opens on demand behind the *＋* button (#182) and closes on Escape. The
  search control follows that pattern so the header stays calm when nobody is searching
  (SPEC-001 design rule: every element on screen must earn its place).
- Storage sizes today are small (SPEC-008 shows the median; the maximum is what motivates this
  spec). Client-side filtering over hundreds of items is instantaneous; a server-side search
  would only become necessary with pagination, which does not exist.

## Chosen Approach (summary)

A *Search* icon button in the storage header opens a text field. While it has a value, the item
groups show only items whose **name** contains every typed word (case- and accent-insensitive);
a status line says how many of the storage's items are shown; a clear button or Escape resets the
filter. Everything happens in the browser on the already loaded list.

### Decisions taken (confirmed at G1, 2026-10-07)

| # | Decision | Rationale / alternative |
|---|----------|-------------------------|
| D1 | **Search control = icon button in the header that opens the field**, like *＋* opens the add form (#182). The field opens empty and focused; it stays open while it has a value and closes when cleared via the button or Escape. | Keeps the header unchanged for the common case of a small storage. Alternative — an always-visible field above the list — is one more element on every storage page; it stays available if Marcel prefers the field to be visible at first sight. A third alternative, showing the field only above *n* items, was rejected: a control that appears and disappears with the item count is surprising. |
| D2 | **Matching on the item name only**: the query is split into words; every word must occur somewhere in the name; comparison is case-insensitive and accent-insensitive (`Käse` matches `kase`, `Crème` matches `creme`); substring, not prefix. | Name is what people remember. Amount, unit and dates are not searched — a query like `kg` would otherwise match everything in kilograms. AND over words makes `bio milch` narrow, not widen. Accent folding is cheap (`normalize('NFD')` + strip combining marks) and matters in four languages. |
| D3 | **Groups stay, they shrink.** The *Expired / Expiring soon / Others* grouping is applied to the filtered items; groups without a match disappear exactly as empty groups do today; the group counters show the matching count. | The expiry colours remain the primary signal (SPEC-001); a flat result list would lose them. |
| D4 | **Result line** `items.search.result` ("3 of 48 items") while a query is set, with `role="status"`; **no match** → the line reads `items.search.noMatches` ("No items match “…”") and offers the clear action. The `items.empty` hint ("No items in this storage yet") is **not** shown for an empty filter result — the storage is not empty. | The count tells at a glance whether the filter is too narrow; the live region announces it to screen readers. |
| D5 | **Adding and editing while filtering**: the filter stays as it is. A newly added item that does not match the query is not shown (the result line's total still increases); inline edit works on the filtered rows. | Predictable: the filter is the user's; the app does not silently drop it. The total in the result line makes an "invisible" add explainable. |
| D6 | **Escape**: in the search field, Escape clears the query and closes the field (focus returns to the search button). The existing document-level Escape that closes the add form is unchanged; when both are open, the one that has focus wins, then the other on the next Escape. | Same keyboard contract as the add form and the session menu. |
| D7 | **Not remembered**: the query lives in the component; leaving the page or reloading clears it. No URL parameter. | A URL parameter would be the natural next step if people want to share "the expired yoghurts" — not needed to solve the stated problem. |

---

## Acceptance Criteria (EARS Notation)

### Control

- [ ] AC-01: WHEN the storage page is shown THE client SHALL show a *Search* icon button in the
      page header next to *New item*; WHEN it is activated THE client SHALL show a text field
      (`type="search"`, labelled `items.search.label`) and move focus into it.
- [ ] AC-02: WHEN the field is cleared with its clear action, or Escape is pressed while it has
      focus, THE client SHALL remove the filter, close the field and return focus to the *Search*
      button.
- [ ] AC-03: WHEN the *Search* button is activated while the field is open and empty THE client
      SHALL close the field; WHEN it is activated while the field has a value THE client SHALL
      keep the field open and refocus it (never discard a query by a stray click).

### Filtering

- [ ] AC-04: WHEN the field has a value THE client SHALL show only items whose name contains
      every whitespace-separated word of the value, compared case-insensitively and
      accent-insensitively; WHEN the value is empty or whitespace THE client SHALL show all items.
- [ ] AC-05: THE filter SHALL be applied before grouping: each expiry group SHALL list only its
      matching items and show the matching count; groups without a match SHALL not be rendered.
- [ ] AC-06: THE filter SHALL update on every input change without a confirm step and without a
      request to the API (the loaded list is filtered in the browser).

### Feedback

- [ ] AC-07: WHILE a query is set THE client SHALL show a result line (`role="status"`) with the
      number of shown items and the storage's total (`items.search.result`); WHEN no item matches
      THE line SHALL read `items.search.noMatches` with the query and the `items.empty` hint SHALL
      not be shown.
- [ ] AC-08: WHEN an item is added or edited while a query is set THE client SHALL keep the query
      and re-apply the filter to the reloaded list.

### i18n & tests

- [ ] AC-09: All new user-facing strings SHALL exist in de / en / fr / it (i18n completeness
      test); the result line SHALL use the existing one/other plural pattern where a count is
      shown.
- [ ] AC-10: The matching rule (AC-04) SHALL be covered by unit tests independent of the page
      (words, case, accents, whitespace-only), and the page behaviour (AC-01…AC-08) by the page
      spec.

---

## Edge Cases

- EC-01: **Query with only spaces** → no filter (AC-04); the result line is not shown.
- EC-02: **Accented query against a plain name** and vice versa (`creme` ↔ `Crème`, `Kase` ↔
  `Käse`) → match. `ß` is not folded to `ss` (deliberately simple; a `Weißbrot` is found by `wei`).
- EC-03: **Two items with the same name** (SPEC-001 EC-01) → both shown; the search does not
  deduplicate.
- EC-04: **Storage with 0 items** → the *Search* button is still rendered (a stable header beats a
  button that appears with the first item); opening it and typing yields the no-match line.
- EC-05: **Inline edit open on a row that stops matching** (the user edits the name away from
  the query while the filter is active) → the row stays visible until the edit is saved or
  cancelled; after the reload the filter applies to the new name.
- EC-06: **Add form and search field both open** → Escape closes whichever has focus (D6); the
  add form's existing Escape handling otherwise behaves as before.
- EC-07: **Very long names / very long queries** → plain substring semantics, no truncation of
  the match logic; the field itself scrolls like any input.

---

## UI Requirements (web)

- Header: `icon-btn` *Search* (🔍, `items.search.toggle` as title/aria-label), `aria-expanded`
  bound to the field's open state, placed right after *New item* (＋).
- Field: `input type="search"` with `items.search.placeholder`, `aria-label` = `items.search.label`,
  a visible clear button (`items.search.clear`) when it has a value; styled like `inline-input`,
  full width above the groups; `autocomplete="off"`.
- Result line below the field: `count-hint` styling, `role="status"`; no-match text includes the
  query in quotes.
- No new dependency, no debounce library (a plain input event is fast enough for the list sizes).

---

## Out of Scope

- Server-side search, pagination, search across storages ("where is the flour?").
- Searching by unit, amount or dates; sorting; fuzzy or typo-tolerant matching.
- Remembering the query (URL parameter, session) across navigation or reload.
- A search on the storage list page.

---

## Technical Constraints (from Architect Agent)

<!-- Proposed before G1, confirmed during implementation -->

- [x] Layering: web client only. `StorageDetailPage` gained `searchOpen` / `query` signals, a
      `searching` computed (EC-01) and a `filteredItems` computed that feeds the existing `groups`
      computed; the matching rule lives in the pure module `storages/item-search.ts`
      (`normalizeForSearch`, `queryWords`, `matchesQuery`). No backend, contract or client change.
- [x] Escape handling (D6): the field's `(keydown.escape)` handler calls `stopPropagation()`, so
      the document-level Escape that closes the add form does not fire for the same key press.
- [x] Tests: `item-search.spec.ts` (6 rule tests), `storage-detail-page.spec.ts` → block
      "item search (SPEC-010)" (10 tests), two SPEC-007 header-button expectations extended by the
      new button, `i18n.spec.ts` (key parity).
- [x] Styles: `.search-bar`, `.search-field` (browser's own clear control suppressed — one clear
      button, ours), `.search-result` in `styles.scss`; the field reuses `inline-input`.
- [x] Dependencies: none new.
- [x] ADR required: no.

---

## Verification

| AC | Test | Status |
|----|------|--------|
| AC-01 | `storage-detail-page.spec.ts` → "AC-01: the header offers a Search button; activating it opens and focuses the field" | ✅ |
| AC-02 | → "AC-02: the clear button removes the filter, closes the field and focuses the button", "AC-02 / D6: Escape in the field clears and closes without touching the add form" | ✅ |
| AC-03 | → "AC-03: the button closes an empty field but only refocuses a field with a value" | ✅ |
| AC-04 | `item-search.spec.ts` (case, accents, ß, AND, blank); page → "AC-04: accent- and case-insensitive, every word must match" | ✅ |
| AC-05 | → "AC-04 / AC-05: filters by name while typing, groups shrink and empty groups vanish" (group counters `Expired · 1`, `Others · 1`) | ✅ |
| AC-06 | → "AC-06: typing never calls the API" (`HttpTestingController.verify()` after typing) | ✅ |
| AC-07 | → "AC-07: a result line counts shown vs. total; no match says so and hides the empty hint" (incl. EC-01 whitespace) | ✅ |
| AC-08 | → "AC-08: adding an item keeps the query and re-applies it to the reloaded list" (D5: hidden non-matching add, total grows) | ✅ |
| AC-09 | `i18n.spec.ts` (de/en/fr/it parity, placeholders); `items.search.result.one/other` | ✅ |
| AC-10 | `item-search.spec.ts` — rule tested without the component | ✅ |
| EC-04 | → "EC-04: an empty storage still offers the search; typing yields the no-match line" | ✅ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ | 2026-10-07 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
