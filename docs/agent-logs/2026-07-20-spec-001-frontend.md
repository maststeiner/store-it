# Agent Run Log: SPEC-001 Frontend — Angular views + i18n

> **Date:** 2026-07-19 / 2026-07-20
> **Spec:** [SPEC-001](../specs/SPEC-001-manage-storage-items.md) (incl. AC-01a addendum)
> **Persona(s):** developer (frontend)
> **Model:** Claude Fable 5 / Opus 4.8 (Claude Code)
> **Branch / PR:** `feature/spec-001-manage-storage-items`

---

## Task

Implement the SPEC-001 web frontend (Angular 22) faithfully to the PO-approved frost-glass mockup: storage overview, storage detail with status-grouped items, add/edit forms, i18n de/en/fr/it — no business rules in the client (ADR-002).

## Plan

Standalone components (list + detail), typed API client, in-house i18n layer, presentation-only grouping from the server-delivered `expiryStatus`.

## Key Decisions

- **i18n deviation:** a minimal in-house `TranslateService` + impure `TranslatePipe` with per-language JSON under `public/assets/i18n/`, instead of ngx-translate. Rationale: fewer dependencies, runtime language switch (spec requirement), `setTranslation()` makes it trivially testable. Accepted by orchestrator.
- **Presentation-only status:** grouping (Expired / ExpiringSoon / Others) and card chips derive solely from server-computed fields (`expiryStatus`, `expiredCount`, `expiringSoonCount`) — no client-side date logic (ADR-002).
- **Coverage gate activated:** line-coverage threshold 70% enforced via `vitest-base.config.ts` (`npm run test:coverage`), wired into CI — closes the long-standing frontend-gate TODO now that real components exist. Consistent with the backend coverlet threshold.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Two subagents died mid-run (org spend limit, then a 600s stall) | Infrastructure, not the work — partial output was sound |
| 2 | Orchestrator finished the missing `storage-detail-page.html` inline and wrote the component/service test suite | Recover from agent deaths without redoing good work |

## Outcome

- **Result:** feature complete. Storage overview (cards + status chips + inline create/rename + delete-with-confirmation), storage detail (breadcrumb, rename/delete, add-item form, grouped item list, per-item inline edit/delete), i18n de/en/fr/it with browser-default + header switcher persisted to localStorage.
- **Tests:** 20 vitest specs (list, detail, api client, error mapping, app shell) — all green; line coverage 87% (> 70% gate).
- **Gates:** lint, prettier, build, test:coverage — all green locally.
- **Deviations from spec:** none functional; i18n implementation choice documented above.
- **Deferred:** logo/app icon (task #8); the ❄ placeholder stays until then.
