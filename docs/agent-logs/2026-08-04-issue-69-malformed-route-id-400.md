# Agent Run Log: Explicit 400 for malformed GUID route params (API-wide)

> **Date:** 2026-08-04
> **Spec:** none — tech-debt issue [#69](https://github.com/maststeiner/store-it/issues/69); the issue body carries the frozen decision and the implementation plan (deferred from the PR #34 G2 review)
> **Persona(s):** developer (implementation) · qa (service tests from the issue's acceptance statement)
> **Model:** Claude Opus 5
> **Branch / PR:** `feature/malformed-guid-400` → `develop` (PR pending)

---

## Task

Implement the deferred option from issue #69: a non-GUID path value (e.g. `/api/v1/storages/abc`)
must answer an explicit **400 ProblemDetails** instead of the 404 the `:guid` route constraint
produced by simply not matching — **API-wide**, on every endpoint carrying a `storageId`/`itemId`.

## Plan

1. Drop the `:guid` route constraint from all route templates (4 × `storageId`, 2 × `itemId`,
   plus the `/{storageId}/items` group).
2. Bind ids as `string`, parse them explicitly with `Guid.TryParse` in one shared helper, and
   answer `TypedResults.Problem` (400, `errorCode` = `request.invalidId`) on failure.
3. Declare the 400 response on every affected operation so the published contract states it.
4. Add malformed-route service tests for every affected endpoint (both ids where two exist).
5. Regenerate the committed OpenAPI contract + the frontend client; keep both drift gates green.

## Key Decisions

- **Explicit `Guid.TryParse` in the handler, not framework binding.** Leaving the parameter typed
  as `Guid` without the constraint would rely on minimal-API binding failure, whose behaviour
  depends on `RouteHandlerOptions.ThrowOnBadRequest` (true only in Development): 400 with a
  ProblemDetails body locally, 400 with an empty body in production. Parsing explicitly makes the
  response identical in every environment.
- **One shared `TryParseRouteId` helper** returning the ready-made `ProblemHttpResult` — keeps the
  seven handlers to a two-line guard each and the error contract in exactly one place.
- **`errorCode` = `request.invalidId`** (locale-neutral, arc42 §8), distinct from the existing
  `request.invalid` used for malformed request bodies, so clients can tell the two apart. The
  detail names the offending parameter (`'storageId' must be a GUID.`) and never echoes the raw
  value back.
- **Parse before the use case runs**, so a malformed id wins over a would-be 404 for an unknown
  storage/item — one error class, one status code.
- **`format: uuid` preserved in the contract** via a small `RouteIdFormatTransformer`
  (`IOpenApiOperationTransformer`, registered next to the existing schema transformers). Binding as
  `string` would otherwise have downgraded every id parameter to a bare `string` in the published
  contract — a documentation/codegen regression for the Angular client and the later iPhone app.
  The contract diff is therefore purely additive: the new 400 responses, nothing removed.
- **No spec written (G1).** Issue #69 is a tech-debt item whose body already fixes scope, approach
  and acceptance ("400 ProblemDetails for a malformed id on every affected endpoint"); it is treated
  as the frozen input. Flagged for the orchestrator rather than silently assumed.

## Human Interventions

<!-- Every challenge, correction, or re-run by the orchestrator -->

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Orchestrator asked to start issue #69 in its own worktree | KAIFe isolation rule (worktree + feature branch + PR) |

## Outcome

- **Result:** implemented, local gates green — pending review (G2) and CI (G3)
  - `dotnet test`: Domain 46 ✓ · Architecture 9 ✓ · Api.Service 58 ✓ (12 new: 10 malformed-route
    cases across all 7 id-carrying endpoints, 2 contract assertions)
  - OpenAPI drift: contract regenerated, diff additive only (`400` on 7 operations)
  - Frontend client: `npm run generate:api` produces no diff (error responses are not part of the
    generated client surface)
  - Not run locally: Stryker mutation gate, Sonar, E2E — left to CI
- **Deviations from spec:** none. Beyond the issue's list: the `format: uuid` transformer (see
  Key Decisions) and the `LeaksNoStorageData` assertion on the 400 path.
- **Harness follow-up:** none.
