# Agent Run Log: SPEC-011 tags on items

> **Date:** 2026-10-08
> **Spec:** [docs/specs/SPEC-011-item-tags.md](../specs/SPEC-011-item-tags.md) — frozen at G1 the same day (Marcel Steiner, "passt so")
> **Persona(s):** analyst → architect → developer (spec drafted after a clarification round, frozen by Marcel before any code)
> **Model:** Claude Fable 5.1 (claude-fable-5-1)
> **Branch / PR:** `feature/item-tags` → PR (issue #220)

---

## Task

Tags on items ("Dosen", "homemade"), assigned on the item, suggested from the storage's existing
tags, filterable by a click on a chip — with no tag management: a tag exists while an item
carries it. Marcel asked for **no assumptions**; eight questions were answered before the draft
(storage scope shared with members, creation on assignment, suggestions, naming rules, no
rename, chip-click filter, no colours, cascade semantics).

## Plan

1. Clarify, then spec with the answers as D1–D7 and six draft decisions D8–D13; G1.
2. Domain first (`Tag`, aggregate rules, prune), then persistence + migration, use cases,
   endpoint, contract/client; backend tests at every layer that holds a rule.
3. Web: a reusable `TagInput`, chips + filter on the storage page combined with the SPEC-010
   search; i18n; tests from the ACs.

## Key Decisions

- **Followed the frozen spec (D1–D13). Details decided while implementing:**
  - **`Tag` rows per storage with a unique `(storage_id, NormalizedName)` index** rather than
    free-text tags on items: the canonical spelling (D4) and the "first assignment wins" rule
    need one row per identity; the unique index is the backstop against a race between two
    members creating the same tag. From the user's side nothing is "managed" — rows appear
    and vanish with the items (D2), which is exactly Marcel's closing question.
  - **Pruning is in the aggregate** (`PruneUnusedTags` after update, amount-0 removal and
    delete), not in the use cases: the rule is a domain invariant ("a tag no item carries does
    not exist"), so it lives where `Items` and `Tags` are both visible.
  - **Ordinal, case-insensitive sort** for the tag lists — deterministic across machines;
    `Kase` therefore sorts before `Käse` (noted in a domain test).
  - **`ItemRequest.Tags` is optional on the wire** (`null` = none, EC-07) so the pre-existing
    test helpers and any older request keep working; the response always carries the array.
  - **The tag field has its own *Add* button** (spec amendment A1, Marcel during G2): the
    first version committed text left in the field on blur, which made the item's *Add* button
    look as if it also added the tag. Now only the button, Enter or `,` add; blur only closes
    the suggestions; Enter never submits the surrounding form. Escape closes the suggestion list only when it is
    open, otherwise it bubbles to the page (the add form's Escape, SPEC-010 D6).
  - **The result line is shared**: SPEC-010's "n of m items" now reacts to either filter
    (`filterActive`), and the no-match text names the tag when no text query is set.
- **Access rule reused**: `GET …/tags` sits under the storages group, so the EF access filter and
  the route-id guard give 404 / 400 exactly like the item routes — one new mapping, no new policy.

## Verification

| Check | Result |
|---|---|
| `dotnet build` (Release) · CSharpier | clean |
| `dotnet test` | 265 passed (Domain 83 incl. 13 new, Architecture, Api.Service incl. 11 new with Testcontainers) |
| `dotnet ef migrations add ItemTags` | `tags` + `item_tags`, unique index, cascades — reviewed |
| `ng test` | 221 passed / 23 files (18 new) |
| `ng lint` · `prettier --check .` · production build | clean |
| E2E (CI job 1b) | first run red: the new *Add tag* button also matched the item flow's `/add/i` locator (strict-mode violation) — locator made exact, and the flow now adds a tag and filters by its chip |
| OpenAPI contract | additive: `tags` on `ItemRequest`/`ItemResponse`, `TagResponse`, `getTags`; client regenerated |

Two domain-test expectations were wrong on the first run, not the code: the ordinal sort order
of `Kase`/`Käse`, and a decomposed `é` (acute) where the test meant `è` (grave).

**Not verified visually in a browser** (no signed-in session in the sandbox): the chip layout in
long rows, the suggestion dropdown over the form, and the feel of the comma/Enter commit are for
the human G3 test. **Migration**: the release that carries this PR runs `migrate` before
`backend` as always; nothing else to configure.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "Bitte keine Annahmen treffen, sondern nachfragen" — eight questions answered before the draft | G1 input |
| 2 | Asked whether an item can carry several tags (yes, up to ten; only the filter is single-tag) | clarification before G1 |
| 3 | Froze the spec with D1–D13 unchanged ("passt so") | G1 |
| 4 | Asked for a dedicated *Add tag* button after the tag field — sharing the item's *Add* was confusing | G2 → amendment A1 |

## Outcome

- **Result:** PR open, awaiting G2/G3
- **Deviations from spec:** none in scope or acceptance criteria; amendment A1 (explicit add
  button) recorded
- **Harness follow-up:** none
