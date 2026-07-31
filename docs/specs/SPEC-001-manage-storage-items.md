# Spec: Manage storages and their items (MVP core)

> **Status:** Frozen (Gate 1) · with formal amendments (see below)
> **Sprint:** 2026-S15
> **Author:** Analyst Agent (orchestrated by Marcel Steiner)
> **Frozen:** 2026-07-13 · **Last amended:** 2026-07-19

---

## Amendments (post-freeze, PO-approved)

Changes after the G1 freeze are recorded here rather than silently editing the frozen body — the original acceptance criteria remain intact and traceable.

| # | Date | Change | Approved by |
|---|------|--------|-------------|
| A1 | 2026-07-19 | **AC-01a** added — storage list also returns per-storage `expiredCount` / `expiringSoonCount` (server-computed) for the overview status chips. | Marcel Steiner (PO) |
| A2 | 2026-07-19 | Technical constraint added — API versioning + OpenAPI contract gate per **ADR-006** (does not change any AC). | Marcel Steiner (PO) |
| A3 | 2026-07-31 | Editorial — unit display labels in the fixed list normalized to English (`Stück` → `Piece`, `Packung` → `Pack`) to match the enum codes and the English-only repo. No AC or behavior change. | Marcel Steiner (PO) |

---

## User Story

As a **household member** I want to **track what is in my storages (pantry, freezer, …) including expiry dates** so that **I always know what I have and what needs to be used soon**.

---

## Acceptance Criteria (EARS Notation)

> **Done-state:** all criteria below are implemented and verified — the [Verification](#verification) table is the source of truth for coverage and status. The checkboxes are left as authored on purpose: the frozen G1 body stays intact and traceable (changes are recorded as amendments, not by editing criteria in place).

### Storages

A storage is a named object holding a list of items. Multiple storages can exist.

- [ ] AC-01: WHEN the user creates a storage with a name THE system SHALL persist it and return it in the storage list.
- [ ] AC-01a *(addendum, PO decision 2026-07-19 — mockup status chips)*: WHEN the user views the storage list THE system SHALL deliver, per storage, the count of expired items and of items expiring soon (server-computed, ADR-002).
- [ ] AC-02: WHEN the user creates a storage with an empty name THE system SHALL reject the request with a validation error.
- [ ] AC-03: WHEN the user renames a storage THE system SHALL update the name (same validation as AC-02).
- [ ] AC-04: WHEN the user deletes a storage THE system SHALL remove it including all of its items. (UI asks for confirmation, see UI section.)

### Items

An item has: name · amount (decimal, max. **one decimal place**, > 0) · unit (from a **fixed list**: Piece, g, kg, ml, l, Pack) · **at least one of** expiry date / production date (both allowed).

- [ ] AC-05: WHEN the user adds an item with a valid name, amount, unit, and at least one date (expiry or production) THE system SHALL persist it and return it in that storage's item list.
- [ ] AC-06: WHEN the user adds an item with an empty name, an amount ≤ 0, more than one decimal place, a unit outside the fixed list, or neither expiry nor production date THE system SHALL reject the request with a validation error.
- [ ] AC-07: WHEN the user edits an item (name, amount, unit, dates) THE system SHALL update it (same validation as AC-06).
- [ ] AC-08: WHEN the user sets an item's amount to 0 THE system SHALL remove the item from the storage.
- [ ] AC-09: WHEN the user deletes an item THE system SHALL remove it regardless of amount.

### Expiry overview

- [ ] AC-10: WHEN the user views a storage THE system SHALL group its items into: **Expired** · **Expiring soon** (≤ 3 days) · **Others** — within each group sorted by expiry date ascending; items without expiry date appear in "Others", sorted last, showing their production date.
- [ ] AC-11: WHEN an item's expiry date is within the next 3 days (including today) THE system SHALL mark it as "expiring soon".
- [ ] AC-12: WHEN an item's expiry date is in the past THE system SHALL mark it as "expired".

---

## UI Requirements (web)

- **Design: modern but minimal** — clean typography, generous whitespace, reduced color palette; color carries meaning (status markers for expired/expiring soon), not decoration. No visual clutter: every element on screen must earn its place.
- The web app is the **first client for fast testing** — functional and responsive, but not mobile-optimized; the planned iPhone app will be the leading mobile client (arc42 §1).
- **Storage view:** item list grouped per AC-10 with clear visual markers for "expired" and "expiring soon".
- **Item form:** one screen — name, amount + unit, expiry and/or production date. Minimal input effort.
- **Languages:** UI fully localized in **German, English, French, Italian**; default from browser locale, manually switchable. The API stays locale-neutral (ISO dates, enum codes — no translated strings from the backend).

---

## Edge Cases

- EC-01: Two items with the same name in one storage are allowed (e.g. two yogurts with different expiry dates) — they are separate items.
- EC-02: Expiry date exactly today → "expiring soon" (not "expired").
- EC-03: Storage with 0 items → empty list, no error.
- EC-04: Amount with more than one decimal place (e.g. 0.25) → validation error (not silent rounding).
- EC-05: Item with only a production date → never marked "expired"/"expiring soon"; listed in "Others".
- EC-06: Deleting a storage with items → items are deleted with it (no orphans).

---

## Out of Scope

- Accounts, authentication, sharing between users (→ follow-up spec + ADR-004). **MVP simplification: all storages are globally visible — accepted for the walking skeleton, revisited with auth.**
- Cross-storage expiry overview ("what expires soon across all storages") — later
- Search / filtering — later
- Configurable "expiring soon" threshold (fixed at 3 days for now) — later
- Storage types/icons, compartments/categories within a storage — later
- Notifications, barcode scanning, product databases — later
- iPhone app — later (API-first keeps it additive, ADR-002)

---

## Technical Constraints (from Architect Agent)

- [ ] Layering per ADR-001 (Api → Application → Domain; Infrastructure implements interfaces).
- [ ] API-first per ADR-002: all ACs exposed via REST endpoints; the Angular UI consumes them without own business rules.
- [ ] Persistence per ADR-003 (PostgreSQL + EF Core).
- [ ] API versioning & contract gate per ADR-006 (added post-freeze as technical constraint, retro decision 2026-07-18): endpoints under `/api/v1/…`; OpenAPI contract committed as `backend/openapi/v1.yaml`; drift + breaking-change gate (oasdiff) **to be wired into CI as part of this implementation** (not yet active).
- [ ] "Expiring soon" threshold (3 days) and the unit list are named domain constants — not hard-coded in the UI.
- [ ] i18n: translation files per language (de, en, fr, it) in the frontend; no user-facing strings hard-coded in components.
- [ ] ADR required: no — covered by ADR-001/002/003.

---

## Verification

Tests derived black-box from these ACs by the QA persona (never from the code).
`Domain` = StoreIt.Domain.Tests · `Service` = StoreIt.Api.Service.Tests · `E2E` = frontend/e2e · `FE` = frontend vitest specs.

| AC / EC | Covered by | Status |
|---------|-----------|--------|
| AC-01 create storage | Domain `StorageTests.Create_*` · Service `CreateStorage_*` · E2E create flow | ✅ |
| AC-01a status counts | Service `GetStorages_StorageWithMixedItems_*`, `CreateStorage_FreshStorage_HasZeroStatusCounts` · FE chip rendering | ✅ |
| AC-02 empty name rejected | Domain `Create_WithEmptyName_*` · Service `CreateStorage_WithEmptyName_*` | ✅ |
| AC-03 rename | Domain `Rename_*` · Service `RenameStorage_*` · FE inline-rename | ✅ |
| AC-04 delete incl. items | Service `DeleteStorage_StorageWithItems_*`, `DeleteStorage_UnknownStorage_*` · FE/E2E delete-with-confirm | ✅ |
| AC-05 add item | Domain `AddItem_*` · Service `AddItem_*` · E2E add flow | ✅ |
| AC-06 item validation | Domain `AddItem_With{EmptyName,NonPositiveAmount,MoreThanOneDecimalPlace,NeitherDate}_*` · Service `AddItem_*` incl. unit-outside-list · E2E no-date | ✅ |
| AC-07 edit item | Domain `UpdateItem_WithValidData_*` · Service `UpdateItem_*` · FE inline-edit | ✅ |
| AC-08 amount 0 removes | Domain `UpdateItem_WithAmountZero_RemovesItemAndReturnsFalse` · Service `UpdateItem_WithAmountZero_*` | ✅ |
| AC-09 delete item | Domain `RemoveItem_*` · Service `DeleteItem_*` · FE delete | ✅ |
| AC-10 sorted grouping | Domain `GetItemsSortedByExpiry_*` · Service `GetItems_MixedExpiryDates_*` · FE/E2E group rendering | ✅ |
| AC-11 expiring soon | Domain `ExpiryRulesTests`, `ItemTests.GetExpiryStatus_*` · Service `GetItems_*ExpiryStatus*` | ✅ |
| AC-12 expired | Domain `ExpiryRulesTests`, `ItemTests.GetExpiryStatus_ExpiryDateInThePast_*` · E2E expired group | ✅ |
| EC-01 duplicate names | Domain `AddItem_WithSameNameTwice_*` · Service `AddItem_WithSameNameTwice_*` | ✅ |
| EC-02 expiry today = soon | Domain `ExpiryRulesTests`, `GetExpiryStatus_ExpiryDateToday_*` | ✅ |
| EC-03 empty storage | Domain `GetItemsSortedByExpiry_EmptyStorage_*` · Service `GetItems_StorageWithoutItems_*` | ✅ |
| EC-04 no silent rounding | Domain `AddItem_WithMoreThanOneDecimalPlace_*` (0.25/1.001/99.99) | ✅ |
| EC-05 production-date only | Domain `GetExpiryStatus_OnlyProductionDate_*` · FE production-date rendering | ✅ |
| EC-06 no orphaned items | Service `DeleteStorage_StorageWithItems_*` (cascade) | ✅ |

**Totals:** 90 backend tests (45 domain · 9 architecture · 36 service) + 20 frontend vitest + 3 Playwright E2E — all green. Backend domain line coverage 97.8%, frontend 87%.

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ frozen | 2026-07-13 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
