# Spec: Tags on items — assign, suggest, filter by tag

> **Status:** Frozen (Gate 1) — approved by Marcel Steiner, 2026-10-08 (issue #220)
> **Sprint:** 2026-S41
> **Author:** Claude Fable 5.1 (analyst/developer agent), from Marcel Steiner's request and answers (issue #220, 2026-10-08)
> **Last updated:** 2026-10-08 (implementation on `feature/item-tags`, verification table filled)

---

## User Story

As a **user of a storage** I want to **put one or more tags on an item ("Dosen", "homemade", …),
pick existing tags while typing, and filter the storage by a tag with one click** so that **I
find things by category, not only by name — and nobody has to maintain a tag list.**

---

## Context & Relationship to Existing Work

- Items (SPEC-001) carry name, amount, unit and dates; they belong to exactly one storage, which
  is an aggregate owning its items (`Storage.AddItem / UpdateItem / RemoveItem`). Owner and
  members have the same rights on items (SPEC-007: `HasAccess` guards every item endpoint).
- SPEC-010 added the client-side text search on the storage page; its result line and "groups
  shrink" behaviour are reused here, and a tag filter must combine with it.
- The admin dashboard (SPEC-008) counts items by unit; tags are not part of it (out of scope).
- Account deletion (SPEC-006) and storage deletion cascade to items; tags follow the same cascade.

## Decisions taken with Marcel before the draft (2026-10-08)

Answers to the clarification questions; they settle the shape of the feature. The freeze of
the whole spec is a separate step (gate table).

| # | Decision | Marcel's answer |
|---|----------|-----------------|
| D1 | **Scope: tags belong to the storage and are shared with its members.** The same word in two storages is two tags. | 1c |
| D2 | **No separate tag management.** A tag comes into existence when it is first put on an item and disappears when the last item of the storage loses it (edit, item delete, storage delete). While tagging, the storage's existing tags are offered for selection. | 2a + note, 8 (both yes), closing question |
| D3 | **Suggestions while typing** from the storage's existing tags. | 3 |
| D4 | **Naming:** uniqueness per storage is case-insensitive (`Dosen` = `dosen`); the spelling of the first assignment is kept and shown everywhere; max **30** characters; max **10** tags per item; spaces inside a tag allowed (`selbst gemacht`). | 4 |
| D5 | **No rename** — remove and re-assign. | 5 |
| D6 | **Filter by clicking a tag chip** on an item, within the storage, combinable with the text search (AND). The text search does **not** match tags. | 6b |
| D7 | **Plain text chips, no colours.** | 7 |

### Decisions taken by the draft (confirmed at G1, 2026-10-08)

| # | Decision | Rationale / alternative |
|---|----------|-------------------------|
| D8 | **Normalisation of a typed tag:** trim, collapse inner whitespace to one space, compare case-insensitively after Unicode NFC. Empty strings are ignored; duplicates within one request collapse to one. The limit (D4) applies after trimming. | Makes `" Dosen "`, `"dosen"` and `"Dosen"` the same tag without surprising anyone. Accent folding is **not** applied (`Käse` ≠ `Kase` as a tag — the search in SPEC-010 folds accents for *finding*, a tag is an *identity*). |
| D9 | **Tags travel with the item:** `ItemRequest` gains `tags: string[]` (required array, may be empty) and `ItemResponse` gains `tags: string[]` (canonical spelling, sorted case-insensitively). No separate "assign tag" endpoint. | One round trip per item change, exactly like name or amount; the client's add/edit form sends the whole item anyway. |
| D10 | **Suggestions come from `GET /api/v1/storages/{storageId}/tags`** → `[{ name, itemCount }]`, canonical spelling, sorted case-insensitively. Also used to render the active filter's count. | A dedicated read keeps the item list lean; `itemCount` lets the chip say "Dosen · 4" without client-side counting over a filtered list. |
| D11 | **Cleanup is part of the item use cases**, not a background job: after add/update/delete of an item, the storage drops tags that no item carries. Storage deletion cascades. | Deterministic and testable; nothing runs "later". |
| D12 | **Filter UI:** clicking a chip on any item row sets that tag as the active filter (one tag at a time); the active tag is shown above the list as a chip with its count and a ✕; clicking the same chip again or the ✕ clears it. Text search (SPEC-010) and tag filter combine with AND; the SPEC-010 result line counts the combined result. | One active tag keeps the UI simple; multi-tag filtering (AND/OR) is a later step if wanted. |
| D13 | **Tag input in the item form:** a field below the dates; typing shows matching existing tags of the storage; Enter or `,` adds the typed value (new or existing), selecting a suggestion adds it; chips with ✕ above the field; the input is blocked at 10 chips with a hint. Available on the add form and the inline edit. | Same control in both places; the 10-tag limit is visible before the API says no. |

---

## Amendments (post-freeze)

| # | Date | Change |
|---|------|--------|
| A1 | 2026-10-08 | **D13 — explicit *Add* button for tags.** The tag field gets its own *Add tag* button right after it (`items.tags.add`), disabled while the field is empty; the button, Enter or `,` add the typed text. Text left in the field is **not** committed when the field loses focus — the first implementation did that, which made the item's *Add* button look as if it also added the tag. Requested by Marcel Steiner during G2: "Einen eigenen Button um Tags hinzuzufügen, gleich hinter dem Textfeld für Tags. Dasselbe wie für das Hinzufügen der Items ist verwirrend." Scope and acceptance criteria unchanged (AC-09 names Enter and `,`; the button is an additional way). |

---

## Acceptance Criteria (EARS Notation)

### Backend — tags on items

- [ ] AC-01: WHEN an item is added or updated with `tags` THE system SHALL store the item with
      those tags, normalised per D8, and SHALL return them in `ItemResponse.tags` in canonical
      spelling, sorted case-insensitively.
- [ ] AC-02: WHEN a tag of the request matches an existing tag of the storage case-insensitively
      THE system SHALL use the existing tag (its spelling), not create a second one; WHEN it
      matches none THE system SHALL create the tag in the storage with the typed spelling.
- [ ] AC-03 (Error): WHEN a request carries more than 10 distinct tags THE system SHALL answer
      `400` with `errorCode` `item.tags.tooMany`; WHEN a tag is longer than 30 characters after
      trimming THE system SHALL answer `400` with `item.tags.tooLong`; the item SHALL stay unchanged.
- [ ] AC-04: WHEN after an item add/update/delete no item of the storage carries a given tag
      THE system SHALL remove that tag from the storage (D2/D11); WHEN a storage is deleted THE
      system SHALL remove its tags with it.
- [ ] AC-05: WHEN `GET /api/v1/storages/{storageId}/tags` is called by the owner or a member
      THE system SHALL return the storage's tags with `name` (canonical spelling) and `itemCount`,
      sorted case-insensitively; WHEN called by anyone else THE system SHALL answer `404` (same
      rule as the item endpoints).
- [ ] AC-06: WHEN an item is listed via `GET …/items` THE system SHALL include `tags` for every
      item; items without tags SHALL carry an empty array.
- [ ] AC-07: THE OpenAPI contract SHALL contain `tags` on `ItemRequest` and `ItemResponse` and
      the operation `getTags` under the `Items` tag; the breaking-change gate SHALL report no
      breaking change (additive: a client that omits `tags` sends an empty array — see EC-07).

### Web — assigning tags

- [ ] AC-08: WHEN the add form or the inline edit of an item is open THE client SHALL show the
      item's tags as chips with a ✕ each and a tag input below the dates (`items.tags.label`).
- [ ] AC-09: WHEN the user types in the tag input THE client SHALL list the storage's existing
      tags whose name contains the typed text (case-insensitive) and that are not yet on the item;
      selecting one adds it; Enter or `,` adds the typed text as a tag (trimmed); an empty value
      adds nothing.
- [ ] AC-10: WHEN the item already has 10 tags THE client SHALL disable the input and show
      `items.tags.limit`; WHEN the API answers `item.tags.tooMany` or `item.tags.tooLong` THE
      client SHALL show the translated message in the form (existing error handling).
- [ ] AC-11: WHEN the item is saved THE client SHALL send the chips as `tags` and reload the
      list; the tags shown afterwards are the API's canonical spelling (AC-01/02).

### Web — showing and filtering

- [ ] AC-12: WHEN an item has tags THE client SHALL show them as plain chips after the item
      name in the row (D7), in the API's order; WHEN it has none THE row SHALL look as today.
- [ ] AC-13: WHEN a chip in an item row is clicked THE client SHALL set that tag as the active
      filter: only items carrying the tag are shown (groups shrink as in SPEC-010 AC-05), and an
      active-filter chip with the tag name, its `itemCount` and a ✕ appears above the list.
- [ ] AC-14: WHEN the active-filter chip's ✕ is clicked, or a row chip of the active tag is
      clicked again, THE client SHALL remove the tag filter; WHEN another row chip is clicked
      THE client SHALL replace the active tag.
- [ ] AC-15: WHEN a text query (SPEC-010) and a tag filter are both set THE client SHALL show
      items matching both; the SPEC-010 result line SHALL count the combined result and SHALL
      be shown whenever either filter is active.
- [ ] AC-16: All new user-facing strings SHALL exist in de / en / fr / it (i18n completeness
      test).

---

## Edge Cases

- EC-01: **Same word, different case or surrounding spaces** (`Dosen`, `dosen`, ` DOSEN `) → one
  tag, the first spelling wins (D4/D8). Shown with that spelling on every item.
- EC-02: **Accents** (`Käse` vs `Kase`) → two different tags (D8). The suggestion list shows both,
  so the duplicate is visible before it is created.
- EC-03: **Last item with a tag is edited to drop it** → the tag is gone from the suggestions
  immediately after the save (AC-04). Re-typing it creates it again with the new spelling.
- EC-04: **Item deleted while its tag is the active filter** → the list reloads; if the tag no
  longer exists the active filter is cleared and the full list is shown; if other items still
  carry it, the filter stays.
- EC-05: **Storage deleted, account deleted** → tags cascade with the storage; no orphan rows.
- EC-06: **Request with 10 tags where two are the same word in different case** → 9 distinct
  tags after normalisation, accepted (the limit counts distinct tags).
- EC-07: **Older clients that omit `tags`** (none exist, SPEC-002: the web client is the only
  client and is released together) → the server treats a missing `tags` as an empty array, so an
  update without `tags` clears the item's tags. Stated here so nobody is surprised.
- EC-08: **Whitespace-only tag, empty string** → ignored, not an error (D8).
- EC-09: **Tag text with a comma typed into the input** → the comma is the separator (D13), the
  parts become separate tags; a tag cannot contain a comma.
- EC-10: **Member of a shared storage** tags an item → the tag belongs to the storage; the owner
  and all members see and can use it (D1).
- EC-11: **Dev login / tests** → nothing special; tags are per storage.

---

## UI Requirements (web)

- Item row: chips (`.tag-chip`, button elements for the filter click, `aria-pressed` when active)
  after `.item-name`, before the quantity; no colours (D7); wrap on narrow screens.
- Active filter: a `.tag-filter` line above the groups (below the SPEC-010 search bar when open):
  chip "Dosen · 4" with ✕ (`items.tags.clearFilter`).
- Form control (`app-tag-input`, shared by add form and inline edit): chips with ✕, text input
  with `role="combobox"`, suggestion `listbox` (existing tags not yet assigned, filtered by the
  typed text), Enter / `,` add, Escape closes the suggestions only; hint `items.tags.limit` at 10.
- Strings: `items.tags.label`, `items.tags.placeholder`, `items.tags.add`, `items.tags.remove`
  (`{{name}}`), `items.tags.limit`, `items.tags.filterBy` (`{{name}}`), `items.tags.clearFilter`,
  `errors.item.tags.tooMany`, `errors.item.tags.tooLong`.

---

## Out of Scope

- Colours or icons on tags; renaming a tag (D5); a tag management page.
- Tags per account or across storages; a tag filter on the storage list page; searching tags
  through the SPEC-010 text field (D6); filtering by more than one tag at a time (D12).
- Tag statistics on the admin dashboard.
- Bulk tagging of several items.

---

## Technical Constraints (from Architect Agent)

<!-- Proposed before G1, confirmed during implementation; deviations from the proposal marked ⚠ -->

- [x] Domain: `Tag` (Id, Name, NormalizedName; `Clean` = trim + collapse whitespace + NFC with
      the 30-character check, `Normalize` = lower-case) as a child of the `Storage` aggregate;
      `Item.Tags` with an internal `SetTags`; `Storage.AddItem / UpdateItem` take
      `IEnumerable<string>? tags` (optional, so existing callers compile) and resolve them
      through `ResolveTags` (blank dropped, duplicates collapsed, existing reused by normalized
      name, new created with the typed spelling, `item.tags.tooMany` above 10 distinct);
      `PruneUnusedTags` after update, amount-0 removal and remove (AC-04);
      `GetTagsWithCounts` (AC-05).
- [x] Persistence: tables `tags` (FK `storage_id` cascade, unique index
      `(storage_id, NormalizedName)`, both names max 30) and `item_tags` (`item_id`, `tag_id`,
      cascade both ways) — EF many-to-many via `UsingEntity("item_tags")`; migration
      `20261008113709_ItemTags`; repository includes `Items.Tags` and `Tags` (split query).
- [x] Application: `AddItemInput.Tags` / `UpdateItemInput.Tags` (optional), `ItemWithStatus.Tags`
      (canonical, sorted ordinal-ignore-case), `TagWithCount`, `GetStorageTagsUseCase`.
- [x] Api: `ItemRequest.Tags` (`IReadOnlyList<string>?`, absent = none, EC-07), `ItemResponse.Tags`,
      `TagResponse(Name, ItemCount)`, `GET /api/v1/storages/{storageId}/tags` (`getTags`, tag
      `Items`, 400/404 like the item routes); contract and typed client regenerated and committed.
- [x] Web: `TagInput` (`shared/tag-input.ts`, `model<string[]>` + `suggestions` input; combobox with
      listbox, Enter / `,` / blur commit, arrow keys, Escape closes the list only, Backspace removes
      the last chip, locked at 10); the storage page holds `tags`, `activeTag`, `filterActive` and
      combines the tag filter with SPEC-010's `filteredItems`; `getTags` is loaded with the items
      and after every add / save / delete (EC-04 clears a vanished active tag).
      Text left in the tag field is not committed on blur (amendment A1): the field has its own *Add* button.
- [x] Tests: `StorageTagsTests` (13, domain), `ItemTagsTests` (11, service incl. member access,
      cascade, 400s), `OpenApiContractTests` (`getTags`); web `tag-input.spec.ts` (10),
      `storage-detail-page.spec.ts` → block "tags (SPEC-011)" (8), `i18n.spec.ts`; E2E: the
      Playwright item flow adds a tag with the *Add tag* button and filters by its chip
      (`e2e/storage.spec.ts`). The item's *Add* is now located exactly — the tag button also
      matches `/add/i`.
- [x] Docs: release PR must say "migration included" (`migrate` before `backend`, runtime contract
      §2 rule 1); no new variables, so `runtime-contract.md` is unchanged.
- [x] Dependencies: none new.
- [x] ADR required: no.

---

## Verification

| AC | Test | Status |
|----|------|--------|
| AC-01 | `ItemTagsTests.AddItem_WithTags_ReturnsThemCanonicalAndSorted`; `StorageTagsTests.AddItem_WithTags_CreatesTheTagsOnTheStorage` | ✅ |
| AC-02 | `ItemTagsTests.AddItem_WithExistingTagInOtherCase_ReusesTheFirstSpelling`; `StorageTagsTests.AddItem_WithExistingTagInOtherCase_ReusesTheFirstSpelling`, `…_NormalisesWhitespaceAndKeepsAccents` (EC-02) | ✅ |
| AC-03 | `ItemTagsTests.AddItem_WithTooManyTags_Returns400AndKeepsTheStorageUnchanged`, `…UpdateItem_WithTooLongTag_Returns400AndKeepsTheItem`; `StorageTagsTests.AddItem_WithMoreThanTenDistinctTags_Throws`, `…_WithATagLongerThanThirtyCharacters_Throws`, `…_WithExactlyThirtyCharactersAfterTrimming_Passes`, `…_IgnoresBlankTagsAndCollapsesDuplicates` (EC-06/EC-08) | ✅ |
| AC-04 | `ItemTagsTests.UpdateAndDelete_PruneTagsNoItemCarries`, `…DeleteStorage_TakesItsTagsWithIt` (EC-05); `StorageTagsTests.UpdateItem_ReplacesTheTagsAndPrunesTheUnused`, `…_ToAmountZero_RemovesTheItemAndItsOrphanedTags`, `RemoveItem_PrunesTagsNoOtherItemCarries` | ✅ |
| AC-05 | `ItemTagsTests.GetTags_ListsNameAndItemCountSorted`, `…GetTags_UnknownOrForeignStorage_Returns404`, `…GetTags_MalformedStorageId_Returns400`, `…Member_SeesAndReusesTheStorageTags` (EC-10); `StorageTagsTests.GetTagsWithCounts_…` | ✅ |
| AC-06 | `ItemTagsTests.AddItem_WithoutTagsField_HasNoTags` (EC-07) | ✅ |
| AC-07 | `OpenApiContractTests` (`getTags`); CI job *API contract gate* (additive) | ✅ · ⏳ CI |
| AC-08 | `tag-input.spec.ts` → "AC-08: shows the item tags as chips …"; page → "AC-08 / AC-11: the add form carries a tag input …", "… the inline edit starts with the item tags …" | ✅ |
| AC-09 | `tag-input.spec.ts` → "Enter adds the typed text …", "a comma separates tags …" (EC-09), "lists existing tags that match …", "a suggestion is picked …", "D4: typing an existing tag in another case …", "A1: the Add button next to the field adds the typed text …", "A1: leaving the field commits nothing …" | ✅ |
| AC-10 | `tag-input.spec.ts` → "AC-10: at ten tags the input is disabled …"; page → "AC-10: the API refusal for tags is shown in the form" | ✅ |
| AC-11 | page → add form sends `tags: ['Dosen', 'neu']`, inline edit sends the changed set | ✅ |
| AC-12 | page → "AC-12: shows the tags of an item as chips …" | ✅ |
| AC-13 | page → "AC-13: clicking a chip filters the list …" (groups shrink, active chip with count, `aria-pressed`) | ✅ |
| AC-14 | page → "AC-14: the ✕ clears the filter, the same chip toggles it off, another chip replaces it" | ✅ |
| AC-15 | page → "AC-15: tag filter and text search combine with AND and share the result line" | ✅ |
| AC-16 | `i18n.spec.ts` (de/en/fr/it parity) | ✅ |
| EC-04 | page → "EC-04: the active filter is cleared when its tag no longer exists after a reload" | ✅ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ | 2026-10-08 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
