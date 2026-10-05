# Agent Run Log: SPEC-008 operator usage dashboard, part 1

> **Date:** 2026-10-05
> **Spec:** [docs/specs/SPEC-008-operator-usage-dashboard.md](../specs/SPEC-008-operator-usage-dashboard.md) — frozen at G1 the same day (Marcel Steiner)
> **Persona(s):** developer (spec drafted in the same session, frozen by Marcel before any code)
> **Model:** Claude Fable 5.1 (claude-fable-5-1)
> **Branch / PR:** `feature/admin-dashboard` → PR #204 (issue #203)

---

## Task

An in-app, operator-only view of adoption: how many users, storages and items exist and how
the item features are used — from existing data only (no migration), visible to nobody else.

## Plan

1. Backend: query port + use case (Application), EF read side (Infrastructure), options +
   policy + endpoint + `isAdmin` (Api); tests first where the arithmetic lives.
2. Regenerate contract and typed client.
3. Web: `isAdmin` on the session user, `adminGuard`, lazy `/admin` page, session-menu entry.
4. Docs: runtime contract, compose stack, `.env.example`, spec verification table.

## Key Decisions

- **Followed the frozen spec; two details were decided while implementing:**
  - The ownership query filter on `Storage` (SPEC-003) would have hidden every storage but the
    operator's own. `UsageStatisticsQuery` uses `IgnoreQueryFilters()` on every storage-rooted
    query — the one place in the code base that legitimately reads across users, and it
    returns shapes (`StorageShape`, `ItemShape`), never entities.
  - "Shared" means *has at least one member*: the owner is not in `Members` (SPEC-007 model),
    so `Members.Count > 0` is exactly "someone besides the owner".
- **Median = lower median** on the sorted item counts (even count → the smaller middle). Stated
  in code; the spec says "median" without a convention.
- **Expiry buckets are computed in Application with `ExpiryRules`**, not in SQL: the query
  returns `ExpiryDate` per item and the use case applies the rule with the app's
  `TimeProvider` date — the same code path the storage views use (D4). Fine at pilot scale;
  if the item count ever makes this heavy, the query can pre-aggregate with the threshold.
- **Admin check is a claim read** (`AdminAccess.IsAdmin`), shared by the `Admin` policy
  (`AdminRequirementHandler`) and `/auth/me`, so both cannot disagree. Options bound from
  `Admin:Emails`; comparison trimmed, case-insensitive; e-mail taken from `ClaimTypes.Email`
  or the raw `email` claim, as `/auth/me` already does.
- **CA1848** turned the startup warning into a source-generated `LoggerMessage`
  (`AdminStartupLog`); **S2365** turned the option's parsed list into a method.
- **The domain refused my first seed data**: an item needs an expiry *or* a production date
  (`item.dates.missing`). The scenario now has two production-date-only items; the spec's
  AC-07 ("items in every expiry state") is still met.
- **Web client:** `AuthUser.isAdmin` is required (the API always sends it); existing specs
  that build `AuthUser` literals were extended. Numbers are formatted with
  `Intl.NumberFormat(language)` because the app registers no Angular locale data.
- **Not done on purpose:** nav-bar link (the spec puts the entry in the session menu), charts,
  caching, anything per user (part 2 / out of scope).

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "Tagesgenauigkeit reicht, mach einen Spec-Vorschlag für Teil 1. Keine Anpassungen am Datenmodell" | Scoped part 1 to existing data |
| 2 | "E-Mail-Allowlist passt, Spec so einfrieren" | G1 — D1 decided |
| 3 | "leg los mit der Implementierung" | Go for the implementation |

## Outcome

- **Result:** backend 219/219 tests (19 new: use-case arithmetic, access rules, seeded
  scenario, contract), CSharpier clean, 0 warnings; web 169/169 (9 new), lint, Prettier and
  `ng build` clean; contract and client regenerated and committed. Spec verification table
  filled; AC-09 waits for the CI contract gate.
- **Deviations from spec:** none. Two interpretations recorded above (lower median,
  "shared" = has members).
- **Harness follow-up:** (1) `Admin__Emails` must be set in the production deployment after
  merge, otherwise the operator sees `403` and no menu entry; (2) part 2 (activity: day-granular
  `LastSeenAt`, `CreatedAt`/`UpdatedAt`, weekly series, `/privacy` sentence) is a new issue.
