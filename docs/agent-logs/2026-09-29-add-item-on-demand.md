# Agent Run Log: add-item form on demand (#182, SPEC-001 A4)

> **Date:** 2026-09-29
> **Spec:** none of its own — enhancement issue [#182](https://github.com/maststeiner/store-it/issues/182) is the frozen G1 input; recorded as SPEC-001 amendment A4 (UI requirement, no AC changes)
> **Persona(s):** developer
> **Model:** Claude Fable 5.1
> **Branch / PR:** `feature/add-item-on-demand` → PR linked from #182

---

## Task

Marcel: the add-item form on the storage detail page is always open — "das ist oft unnötig".
Wanted: a button like on the storage list, the form opens on click and disappears after adding.

## Plan

1. Ask the four open details (placement/label, closing rules, empty storage, process) with a
   proposal each; Marcel accepted all four.
2. Issue #182 with acceptance criteria; SPEC-001 amendment A4.
3. Detail page: `addOpen` signal, *+ New item* button in place of the form, *Cancel* and Escape,
   close after a successful add, focus on the name field; four locales; tests.

## Key Decisions

- **Same pattern as the list page** (`+ New storage` → inline form): one signal, the form
  replaces the button in place; no modal, no animation.
- **Closing rules:** success closes; validation error keeps it open with the message; *Cancel*
  and Escape close and discard. The Escape listener acts only while the form is open so it
  cannot interfere with the confirm dialogs' own Escape handling.
- **Empty storages start closed too** — consistent; the hint text and the button are enough.
- **Existing add tests open the form first** through a small helper, so they keep proving the
  same behaviour; four new tests cover AC-01 – AC-05.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "Ich hätte gerne einen Button, wie bei meinen Lagerorten und der Dialog geht erst auf, wenn ich auf den Button klicke und nach dem Hinzufügen verschwindet er wieder" | The request. |
| 2 | "1-4 sind so in Ordnung" | Placement above the list, closing rules, closed for empty storages, issue + amendment instead of a spec. |

## Verification

frontend 157 vitest green (4 new), lint, prettier, `ng build`; no backend change.

## Outcome

- **Result:** PR opened against `develop`.
- **Deviations from spec:** none.
- **Harness follow-up:** none.
