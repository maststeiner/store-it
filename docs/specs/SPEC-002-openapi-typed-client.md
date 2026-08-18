# Spec: Typed frontend API client generated from the OpenAPI contract

> **Status:** Frozen (Gate 1)
> **Sprint:** 2026-S17
> **Author:** Analyst Agent (orchestrated by Marcel Steiner)
> **Frozen:** 2026-07-29
> **Source:** Issue #7 (tech-debt) — CodeRabbit review on PR #5

---

## User Story

As a **frontend developer** I want the **API types and client to be generated from the
backend OpenAPI contract** so that **the UI can never silently drift from the API shape
(enums, DTOs, return types) the backend actually exposes**.

---

## Background & Motivation

`frontend/src/app/core/models.ts` hand-duplicates the `Unit` enum and the DTO shapes
(`StorageSummary`, `StorageItem`, `ItemRequest`, `ExpiryStatus`). The hand-written
`frontend/src/app/core/storage-api.ts` re-declares every endpoint's URL and return type.
This duplicates the source of truth in `backend/openapi/StoreIt.Api.json` (generated at
build time per **ADR-006**) and risks drift.

Drift already exists: `storage-api.ts` types `addItem()` as `Observable<StorageItem>`,
but the contract's `POST …/items` returns `201` with a bare `uuid` (string). It is
harmless at runtime today only because the component ignores the return value and
re-fetches — exactly the class of silent mismatch this story removes.

The contract also carries a .NET serialization artifact: because minimal APIs use
`JsonSerializerDefaults.Web` (`JsonNumberHandling.AllowReadingFromString`), `decimal`
and `int` are emitted as `type: [number, string]` / `[integer, string]` with a string
`pattern`. A naïve generator would therefore type `amount` and the counts as
`number | string`, which is wrong for every consumer (web today, iPhone app later).

---

## Scope & Delivery

Cross-cutting change, delivered as **two sequential PRs** under this one spec:

- **PR A — Backend contract cleanup** *(prerequisite):* normalise numeric schemas so the
  committed contract is clean. Goes through the oasdiff contract gate (ADR-006).
- **PR B — Frontend typed client:** generate the client from the clean contract, migrate
  the app onto it, remove the hand-written duplication, and add the CI drift gate.

PR B branches from / rebases onto PR A (needs the clean contract as generator input).

---

## Acceptance Criteria (EARS Notation)

### Backend contract (PR A)

- [ ] AC-01: WHEN the OpenAPI contract is generated THE system SHALL emit `amount` as a
  plain `number` (not `number | string`) and `itemCount` / `expiredCount` /
  `expiringSoonCount` as a plain `integer` (not `integer | string`), with no string
  `pattern` on those numeric schemas.
- [ ] AC-02: WHEN the numeric-schema normalisation is applied THE system SHALL NOT change
  runtime request/response behaviour (input remains accepted as before; only the
  documented contract is tightened).
- [ ] AC-03: WHEN the contract gate (oasdiff) runs against the change THE system SHALL
  either report no breaking change, or the tightening SHALL be explicitly acknowledged in
  the PR per ADR-006 (no unreviewed contract drift).

### Frontend generation (PR B)

- [ ] AC-04: WHEN `npm run generate:api` is run THE system SHALL regenerate the typed
  client from `backend/openapi/StoreIt.Api.json` into a dedicated generated directory,
  producing an injectable Angular service per tag (`StoragesService`, `ItemsService`) and
  models for every schema.
- [ ] AC-05: WHEN the `Unit` enum is generated THE system SHALL make its members available
  as a runtime value (real TS enum), so the item form's unit dropdown is populated from
  the generated source with no hand-maintained list.
- [ ] AC-06: WHEN a component calls the API THE system SHALL use the generated services and
  generated types (`StorageResponse`, `ItemResponse`, `ItemRequest`, `Unit`,
  `ExpiryStatus`); the hand-written shapes in `models.ts` and the HTTP calls in
  `storage-api.ts` SHALL be removed.

### Error handling (PR B)

- [ ] AC-07: WHEN an API call fails with an `HttpErrorResponse` THE system SHALL surface it
  to callers as `ApiError(status, errorCode)` — the existing error contract consumed by
  `ErrorMessages` — via a central HTTP interceptor (replacing the per-call `mapError()`).
- [ ] AC-08: WHEN the error body contains no `errorCode` THE interceptor SHALL yield
  `ApiError` with a `null` error code (parity with today's `extractErrorCode`).

### CI / quality gates (PR B)

- [ ] AC-09: WHEN CI runs THE system SHALL regenerate the client and fail the build if the
  committed generated output differs (drift gate, analogous to the backend contract gate).
- [ ] AC-10: WHEN linting, coverage, and SonarCloud analysis run THE system SHALL exclude
  the generated directory (generated code is not authored, linted, or coverage-measured).

---

## Edge Cases

- EC-01: Enum value added/removed backend-side → regeneration changes the generated enum;
  drift gate (AC-09) fails until the frontend is regenerated and committed.
- EC-02: `POST …/items` returns a bare `uuid`; the generated `ItemsService` types it as
  `Observable<string>`. Components already ignore the value and re-fetch — no behavioural
  change, drift removed.
- EC-03: Generated `enumStyle` must produce runtime enums; if the tool emits union types
  instead, AC-05 (runtime dropdown source) is not met.

---

## Out of Scope

- Replacing the domain `ApiError` model or the `ErrorMessages` → i18n mapping.
- Runtime request validation / schema (e.g. zod) — types only, plus the Angular client.
- Any change to endpoints, routes, or the API surface itself.
- Changing `JsonNumberHandling` runtime behaviour (AC-02 keeps runtime as-is).

---

## Technical Constraints (from Architect Agent)

<!-- To be confirmed/refined by Architect Agent after Gate 1 -->

- [ ] Generator: `ng-openapi-gen` (Node-only, Angular-native; no Java toolchain added),
  `enumStyle: alias` + `enumArray: true` → union type `Unit` **plus** a runtime `UnitArray`
  sidecar that satisfies AC-05 (dropdown source) with zero runtime overhead.
- [ ] Generated client committed to the repo + CI drift gate (consistent with ADR-006's
  committed-contract approach); generated dir excluded from ESLint / coverage / Sonar.
- [ ] Backend normalisation via an `IOpenApiSchemaTransformer` in `StoreIt.Api`
  (contract-only; no change to `ConfigureHttpJsonOptions` runtime behaviour).
- [ ] Endpoints get stable `operationId`s via `.WithName(...)` (PR A) so generated method
  names are clean (`getStorages`, `addItem`, …) rather than path-derived. Non-breaking
  contract addition.
- [ ] Layering unchanged: generated client lives in the frontend presentation/data layer;
  the domain `ApiError` + interceptor remain hand-written.
- [ ] ADR required: **no new ADR** — this realises ADR-006's contract-first direction.
  The PR A tightening is recorded against ADR-006 / the contract gate.

---

## Verification

<!-- Filled in by QA Agent -->

| AC | Test | Status |
|----|------|--------|
| AC-01 | Backend contract/schema test asserts numeric types | ⬜ |
| AC-02 | Existing API request/response tests stay green | ⬜ |
| AC-03 | Contract gate (oasdiff) result on PR A | ⬜ |
| AC-04 | `generate:api` produces services + models | ⬜ |
| AC-05 | Unit dropdown populated from generated enum | ⬜ |
| AC-06 | Component specs green against generated client | ⬜ |
| AC-07 | Interceptor unit test — maps to `ApiError` | ⬜ |
| AC-08 | Interceptor unit test — null `errorCode` | ⬜ |
| AC-09 | CI drift-gate job | ⬜ |
| AC-10 | Lint/coverage/Sonar exclusions in effect | ⬜ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ | 2026-07-29 | Marcel Steiner (PO) |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
