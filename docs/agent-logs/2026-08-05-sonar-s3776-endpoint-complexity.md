# Agent Run Log: Fix SonarCloud S3776 (cognitive complexity) in StorageEndpoints

> **Date:** 2026-08-05
> **Spec:** none — tech-debt fix of a SonarCloud finding; the finding itself is the
> frozen input (repo convention: tech-debt items need no `docs/specs/` entry).
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `refactor/storage-endpoints-complexity`

---

## Task

Fix the single open maintainability violation on SonarCloud:
`csharpsquid:S3776` — *"Refactor this method to reduce its Cognitive Complexity from
18 to the 15 allowed"* — in `backend/src/StoreIt.Api/StorageEndpoints.cs:23`
(`MapStorageEndpointsV1`), impact MAINTAINABILITY / HIGH, effort 6 min.

## Plan

1. Read the finding from the SonarCloud API (rule, file, line, secondary locations)
   instead of guessing which rule/project was meant.
2. Understand where the 18 points come from, using Sonar's own secondary locations.
3. Split the mapping method along a structural boundary that already exists in the
   code, without touching any handler body or route contract.
4. Verify: build (analyzers-as-errors), CSharpier, all three test projects, no
   OpenAPI drift — plus reproduce the rule locally to prove the fix, not assume it.

## Key Decisions

- **Cause:** all nine endpoints were mapped in one method. Each handler's
  `if (!TryParseRouteId(...))` guard sits inside a lambda, so Sonar charges it
  **+2 (1 for the `if`, 1 for nesting)** — its issue payload listed exactly nine such
  locations → 18. The individual handlers are trivial; the *aggregation* was the smell.
- **Fix:** split by route group, the boundary the code already draws with `MapGroup`:
  `MapStorageEndpointsV1` now only creates the `/api/v1/storages` group and delegates to
  `MapStorageRoutes` (collection + single storage) and `MapItemRoutes` (nested items).
  Resulting complexity: 0 / 6 / 12 — all under 15.
- **Rejected:** collapsing the two-id guards in `updateItem`/`deleteItem` into one
  `TryParseRouteIds` helper. It would remove real duplication and buy more headroom
  (items would drop to 8), but it changes handler bodies and thus the diff's risk
  profile. Kept out of this fix; noted below as optional follow-up.
- **No behaviour change:** no route, operationId, status code, handler body, or
  `ProducesProblem` annotation was touched. `backend/openapi/StoreIt.Api.json` is
  regenerated on build and stayed byte-identical — the contract gate confirms it.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Orchestrator allowed `sonarcloud.io` through the sandbox network policy | The agent refused to guess the violation; SonarCloud was blocked by default-deny, so the finding was fetched from the API first |

## Verification

| Check | Result |
|-------|--------|
| `dotnet build -c Release` (analyzers as errors) | succeeded, 0 warnings / 0 errors |
| `dotnet csharpier check .` | 48 files, clean |
| `dotnet test -c Release` | 113 passed / 0 failed (Domain 46, Architecture 9, Api.Service 58) |
| Coverage | 99.42 % line / 95 % branch total (unchanged) |
| OpenAPI contract drift | none — `openapi/StoreIt.Api.json` unchanged |
| S3776 reproduced locally | **yes** — `SonarAnalyzer.CSharp` 10.15 pulled in temporarily reported the identical message (`18 to the 15 allowed`) at `StorageEndpoints.cs(23,41)` on the pre-fix file, and reported nothing on the post-fix file in the same setup. Scaffolding (package refs + `.editorconfig` severity override) reverted; not part of the diff. |

## Outcome

- **Result:** ready for review
- **Deviations from spec:** n/a (no spec — tech-debt fix)
- **Harness follow-up:** optional, not done here — adding `SonarAnalyzer.CSharp` as a
  permanent analyzer package would catch S3776-class findings at build time (gate 1)
  instead of after the push (gate 3). It would first surface whatever else the rule set
  flags across the solution, so it belongs in its own tech-debt issue rather than in
  this fix.
