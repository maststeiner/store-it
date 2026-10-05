# Spec: Operator usage dashboard (read-only, existing data)

> **Status:** Frozen (Gate 1) — approved by Marcel Steiner, 2026-10-05 (issue #203)
> **Sprint:** 2026-S41
> **Author:** Claude Fable 5.1 (developer agent), from Marcel Steiner's request (issue #203, 2026-10-05)
> **Last updated:** 2026-10-05 (implementation on PR #204, verification table filled)

---

## User Story

As the **operator of store-it** I want to **see how many people use the app, how many storages
and items exist and how the item features are used** so that **I can judge adoption without
opening a database shell — and nobody else can see those numbers.**

---

## Context & Relationship to Existing Work

- SPEC-003 introduced accounts keyed by (Issuer, Subject) with `CreatedAt`; SPEC-007 added
  members and invitations to storages. Items carry amount, unit, expiry and production date
  (SPEC-001) but **no timestamps**; storages have none either, and users have no "last seen".
- Direction (Marcel, 2026-10-05): **part 1 touches no data model.** The dashboard shows what
  the schema can already answer. Activity ("how active are they") needs new data and is
  **part 2** (day-granular `LastSeenAt` on users, `CreatedAt`/`UpdatedAt` on storages and
  items — day granularity confirmed as sufficient).
- There is no role model today; the BFF reads claims only (SPEC-003 "pure claim read"). The
  admin check must stay a claim check, not a database lookup per request.
- The web client is the only client (SPEC-002 typed client); the new endpoint and the new
  field are additive, so the ADR-007 contract gate sees no breaking change.
- Privacy: the dashboard shows **aggregates only** — no names, e-mails or per-user rows. No
  new personal data is processed, so `/privacy` needs no change in part 1.

## Chosen Approach (summary)

One authenticated, admin-only endpoint returns a single JSON document of counts. Admins are
identified by an **e-mail allowlist in configuration** (12-factor, like the connection string),
matched against the e-mail claim of the signed-in principal. The web client gets a route
`/admin` with the numbers as plain stat tiles, reachable from the session menu only for
admins; the backend is the real gate.

### Decisions taken (confirmed at G1, 2026-10-05)

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | Admin identity = configuration key `Admin:Emails` (env `Admin__Emails`, comma-separated), compared case-insensitively and trimmed with the `email` claim of the cookie principal. No e-mail claim → never admin. | Operationally the simplest: Marcel knows his sign-in address, nobody needs a user id from the database. The address is asserted by Microsoft/Google, not by the user. The stricter alternative (`issuer|subject` pairs) was considered and declined at G1 (Marcel Steiner, 2026-10-05: "E-Mail-Allowlist passt"); it stays the fallback if an operator account without an e-mail claim ever appears (EC-01). |
| D2 | Authorization policy `Admin` (`RequireAssertion` over the allowlist), applied to a new endpoint group `/api/v1/admin` → `401` without session, `403` for non-admins. The allowlist is read once at startup through options binding; an empty list means "no admin exists" (the group answers `403` for everyone) and the startup check logs a warning, not an error. | Pure claim read keeps the per-request cost at zero and matches the BFF design. An empty allowlist must not break a deployment that simply has no operator view yet. |
| D3 | Endpoint `GET /api/v1/admin/statistics`, operationId `getUsageStatistics`, `200` with `UsageStatisticsResponse` (one flat DTO of integers plus `generatedAt`). | One round trip, trivially cacheable later; the typed client is regenerated from the contract. |
| D4 | Metrics (all computed on request, set-based in the database, no caching): see AC-03…AC-05. "Today" for expiry status comes from the app's `TimeProvider` (as the item endpoints do) and `ExpiryRules` decides *expired* / *expiring soon*; the client does not reimplement the rule (SPEC-001 constraint). | Reuses the one place the expiry rule lives; the numbers match what users see. |
| D5 | `/auth/me` (`UserProfileResponse`) gains `isAdmin: boolean`, additive. The client uses it for the menu entry and an `adminGuard` on `/admin`. | Comfort only; the backend decides. Additive field → no contract break. |
| D6 | Web: route `/admin`, page with stat tiles grouped *Users / Storages / Items*, a "generated at" line and a refresh button. No charts in part 1. | Numbers first; a weekly series (new users per ISO week is already computable) is a candidate for part 2 together with activity. |
| D7 | Architecture: `GetUsageStatisticsUseCase` in Application returns a `UsageStatistics` record; the aggregate queries live behind a new read-side interface `IUsageStatisticsQuery` in Application, implemented in Infrastructure with EF (`CountAsync`/`GroupBy`), no SQL views, no new migration. | Layering per ADR-001 (checked by the architecture tests); a query interface instead of stretching the aggregate repositories with reporting concerns. |

---

## Acceptance Criteria (EARS Notation)

### Access

- [ ] AC-01: WHEN a signed-in user whose e-mail claim is on `Admin:Emails` calls
      `GET /api/v1/admin/statistics` THE system SHALL answer `200` with `UsageStatisticsResponse`.
- [ ] AC-02 (Error): WHEN the endpoint is called without a session THE system SHALL answer `401`;
      WHEN called by a signed-in user not on the allowlist (including users without an e-mail
      claim) THE system SHALL answer `403`; WHEN `Admin:Emails` is empty THE system SHALL answer
      `403` for everyone and SHALL log one warning at startup.

### Numbers

- [ ] AC-03 (Users): THE response SHALL contain `users.total`, `users.newLast7Days`,
      `users.newLast30Days` (by `CreatedAt`, UTC, inclusive of today), `users.byIssuer`
      (count per issuer key, e.g. Microsoft / Google) and `users.withoutAnyStorage` (neither
      owner nor member of any storage).
- [ ] AC-04 (Storages): THE response SHALL contain `storages.total`, `storages.shared`
      (storages with at least one member who is not the owner), `storages.empty` (no items),
      `storages.itemsPerStorageMedian`, `storages.itemsPerStorageMax` and
      `invitations.pending` (open invitations).
- [ ] AC-05 (Items): THE response SHALL contain `items.total`, `items.withExpiryDate`,
      `items.withProductionDate`, `items.expired`, `items.expiringSoon` (both per `ExpiryRules`
      with the app's current date) and `items.byUnit` (count per `Unit`).
- [ ] AC-06: THE response SHALL carry `generatedAt` (UTC) and SHALL contain **no** names,
      e-mail addresses, ids or other per-user values.
- [ ] AC-07: THE numbers SHALL be consistent with the data: verified by an integration test that
      seeds two users, three storages (one shared, one empty) and items in every expiry state
      and asserts every field.

### Session & contract

- [ ] AC-08: WHEN `/auth/me` is called THE system SHALL include `isAdmin` (true only under the
      AC-01 condition); the field SHALL be additive in the OpenAPI contract.
- [ ] AC-09: The OpenAPI contract SHALL contain `getUsageStatistics` under a new tag `Admin`,
      and the breaking-change gate SHALL report no breaking change.

### Web client

- [ ] AC-10: WHEN the session user has `isAdmin = true` THE client SHALL show a menu item
      *Statistics* in the session menu (above *Sign out*) that navigates to `/admin`; WHEN
      `isAdmin` is false THE menu item SHALL not be rendered.
- [ ] AC-11: WHEN a non-admin opens `/admin` directly THE client SHALL redirect to `/storages`
      (route guard); WHEN the API answers `403` anyway THE client SHALL show the existing error
      handling, not a blank page.
- [ ] AC-12: WHEN `/admin` loads THE client SHALL call `getUsageStatistics` once and render the
      AC-03…AC-05 values as labelled tiles in three groups, plus the `generatedAt` time and a
      *Refresh* action that re-queries.
- [ ] AC-13: All new user-facing strings SHALL exist in de / en / fr / it (i18n completeness
      test); numbers SHALL be formatted with the active locale.

---

## Edge Cases

- EC-01: **Admin without e-mail from the provider** (SPEC-003 EC-02): cannot be admin under D1 —
  documented; the stricter D1 alternative would cover it.
- EC-02: **Same e-mail from two providers** (Microsoft and Google accounts of the same person):
  both principals are admins. Acceptable — both are the operator's own accounts.
- EC-03: **Empty database** (fresh deployment): all counts `0`, median `0`, `byIssuer` and
  `byUnit` empty maps; the page renders zeros, not an error.
- EC-04: **Items whose storage owner was deleted** cannot exist (cascade, SPEC-006); no orphan
  handling needed.
- EC-05: **Dev login** (`Development` only) creates users with issuer `dev`; they appear under
  `byIssuer` like any other issuer. Fine for development, irrelevant in prod.
- EC-06: **Clock**: `expired` / `expiringSoon` use the server's current UTC date via
  `TimeProvider`; a user in another time zone may see an item as "expiring soon" one day
  earlier or later than the dashboard counts it. Accepted — same behaviour as the list views.

---

## UI Requirements (web)

- Route `/admin` (lazy-loaded page), guarded by `authGuard` + `adminGuard`.
- Session menu: `menuitem` "Statistics" (`admin.menu.statistics`), rendered only when
  `isAdmin`; placed above *Sign out*, before the destructive *Delete account* block.
- Page: title `admin.statistics.title`; three groups *Users / Storages / Items*; each tile has a
  label key and a locale-formatted number; `byIssuer` and `byUnit` as small two-column lists;
  footer line `admin.statistics.generatedAt` with the timestamp; button `actions.refresh`.
- Loading and error states via the existing patterns (skeleton/`role="status"`, API error
  interceptor). No chart library.

---

## Out of Scope (part 2 candidates)

- **Activity**: active users in 7/30 days (`LastSeenAt`, day granularity), items/storages
  created or changed per period (`CreatedAt`/`UpdatedAt`) — needs a migration and a `/privacy`
  sentence; explicitly deferred by Marcel.
- **Time series / charts** (e.g. new users per ISO week).
- **Per-user or per-storage drill-downs** — would turn aggregates into personal data.
- **Role management UI** — the allowlist is configuration, by design.
- **Caching / snapshots / history** of the numbers.

---

## Technical Constraints (from Architect Agent)

- [x] Layering: `GetUsageStatisticsUseCase(IUsageStatisticsQuery, TimeProvider)` in
      `StoreIt.Application` (`UsageStatisticsUseCases.cs`); `IUsageStatisticsQuery` in Application,
      `UsageStatisticsQuery` (EF, `AsNoTracking`, `IgnoreQueryFilters`) in Infrastructure; endpoint in
      `StoreIt.Api/AdminEndpoints.cs`; `AdminOptions` / `AdminAccess` / `AdminRequirementHandler` in
      `StoreIt.Api/AdminAccess.cs`, policy registered in `AuthenticationSetup`; no EF types outside
      Infrastructure (ADR-001, architecture tests green).
- [x] Configuration: `Admin:Emails` bound via options; an empty list logs one startup warning
      (`AdminStartupLog`); documented in `docs/operations/runtime-contract.md`, passed through in
      `compose.stack.yaml`, listed in `.env.example`. **The production deployment (deploy repo) still
      needs `Admin__Emails` set — operator step after merge.**
- [x] Contract: `backend/openapi/StoreIt.Api.json` regenerated (`getUsageStatistics`, tag `Admin`;
      `isAdmin` on `UserProfileResponse`), web client regenerated (`src/app/api/fn/admin`,
      `AdminService`), both committed.
- [x] Tests: `GetUsageStatisticsUseCaseTests` (scripted query port, no database),
      `AdminEndpointsTests` (401/403/200, case-insensitive allowlist, empty allowlist, `isAdmin`),
      `AdminStatisticsScenarioTests` (AC-07, own database), `OpenApiContractTests`; web:
      `admin.guard.spec.ts`, `session-menu.spec.ts` (SPEC-008 block), `admin-statistics-page.spec.ts`,
      `i18n.spec.ts`.
- [x] Dependencies: none new.
- [x] ADR required: no.

---

## Verification

| AC | Test | Status |
|----|------|--------|
| AC-01 | `AdminEndpointsTests.Statistics_OnAllowlist_Returns200_CaseInsensitive` (3 cases) | ✅ |
| AC-02 | `AdminEndpointsTests.Statistics_Anonymous_Returns401`, `…_SignedInButNotOnAllowlist_Returns403`, `…_SignedInWithoutEmailClaim_Returns403`, `…_EmptyAllowlist_Returns403EvenForTheOperator` (the startup warning is a log line, not asserted) | ✅ |
| AC-03 | `AdminStatisticsScenarioTests.Statistics_SeededWorld_EveryFieldMatches` (users block); `GetUsageStatisticsUseCaseTests.Execute_Windows_StartAtMidnightUtcOfTheFirstCalendarDay`, `…_Users_PassesThroughCountsAndIssuers` | ✅ |
| AC-04 | scenario test (storages block); `GetUsageStatisticsUseCaseTests.Execute_Storages_CountsSharedAndEmptyAndUsesLowerMedian` | ✅ |
| AC-05 | scenario test (items block); `GetUsageStatisticsUseCaseTests.Execute_Items_BucketsByExpiryRulesAndGroupsByUnit` | ✅ |
| AC-06 | scenario test (`generatedAt` present, no name / subject / address in the raw body); `…_EmptyDatabase_ReturnsZerosAndEmptyMaps` (EC-03) | ✅ |
| AC-07 | `AdminStatisticsScenarioTests.Statistics_SeededWorld_EveryFieldMatches` — 3 users, 3 storages (1 shared, 1 empty), 6 items in every expiry state, 1 open link | ✅ |
| AC-08 | `AdminEndpointsTests.Me_ReportsIsAdminOnlyForTheAllowlist` | ✅ |
| AC-09 | `OpenApiContractTests` (`getUsageStatistics`), CI job *API contract gate* (additive) | ⏳ CI |
| AC-10 | `session-menu.spec.ts` → "SPEC-008 statistics entry" (offered above *Sign out* for the operator, emits, absent for others) | ✅ |
| AC-11 | `admin.guard.spec.ts` (redirect to `/storages`, session loaded first); `admin-statistics-page.spec.ts` → "shows the error state when the API refuses" | ✅ |
| AC-12 | `admin-statistics-page.spec.ts` → one call, three groups, 15 tiles, providers/units lists, refresh re-queries | ✅ |
| AC-13 | `i18n.spec.ts` (de/en/fr/it key parity); number formatting via `Intl.NumberFormat(language)` asserted as `1,234` in the page spec | ✅ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ | 2026-10-05 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
