# Spec: Manage storages and their items (MVP core)

> **Status:** Frozen (Gate 1)
> **Sprint:** 2026-S15
> **Author:** Analyst Agent (orchestrated by Marcel Steiner)
> **Last updated:** 2026-07-13

---

## User Story

As a **household member** I want to **track what is in my storages (pantry, freezer, …) including expiry dates** so that **I always know what I have and what needs to be used soon**.

---

## Acceptance Criteria (EARS Notation)

### Storages

A storage is a named object holding a list of items. Multiple storages can exist.

- [ ] AC-01: WHEN the user creates a storage with a name THE system SHALL persist it and return it in the storage list.
- [ ] AC-02: WHEN the user creates a storage with an empty name THE system SHALL reject the request with a validation error.
- [ ] AC-03: WHEN the user renames a storage THE system SHALL update the name (same validation as AC-02).
- [ ] AC-04: WHEN the user deletes a storage THE system SHALL remove it including all of its items. (UI asks for confirmation, see UI section.)

### Items

An item has: name · amount (decimal, max. **one decimal place**, > 0) · unit (from a **fixed list**: Stück, g, kg, ml, l, Packung) · **at least one of** expiry date / production date (both allowed).

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
- [ ] API versioning & contract gate per ADR-006 (added post-freeze as technical constraint, retro decision 2026-07-18): endpoints under `/api/v1/…`; OpenAPI contract committed as `backend/openapi/v1.yaml`; drift + breaking-change gate (oasdiff) wired into CI with this implementation.
- [ ] "Expiring soon" threshold (3 days) and the unit list are named domain constants — not hard-coded in the UI.
- [ ] i18n: translation files per language (de, en, fr, it) in the frontend; no user-facing strings hard-coded in components.
- [ ] ADR required: no — covered by ADR-001/002/003.

---

## Verification

<!-- Filled in by QA Agent -->

| AC | Test | Status |
|----|------|--------|
| AC-01…AC-12 | TODO after implementation start | ⬜ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ frozen | 2026-07-13 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
