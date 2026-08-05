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
| 2 | *"deine checkboxen abhaken wenn du es gemacht hast, und sonst nachholen"* | The PR was opened with most G2/G3 boxes unticked and merely annotated. The agent had left work implied-but-undone: the CodeRabbit result was never checked, no AC→test mapping existed, and the two deferred items sat in prose instead of `tech-debt` issues as the checklist requires. Caught up: CodeRabbit verified (0 findings), verification table added, issues #74 + #75 filed, boxes ticked. |
| 3 | Orchestrator ticked both human-attestation boxes on the PR while the agent was working | Gate G2 human review. Note: the agent's first attempt to rewrite the PR body would have silently reverted those two ticks — it only failed on an unrelated GraphQL error. The retry diffed the live body first and preserved the human ticks plus CodeRabbit's appended release notes. Rewriting a PR body is a read-modify-write on a document a human also edits; treat it as such. |

## Verification

| Check | Result |
|-------|--------|
| `dotnet build -c Release` (analyzers as errors) | succeeded, 0 warnings / 0 errors |
| `dotnet csharpier check .` | 48 files, clean |
| `dotnet test -c Release` | 113 passed / 0 failed (Domain 46, Architecture 9, Api.Service 58) |
| Coverage | 99.42 % line / 95 % branch total (unchanged) |
| OpenAPI contract drift | none — `openapi/StoreIt.Api.json` unchanged |
| S3776 reproduced locally | **yes** — `SonarAnalyzer.CSharp` 10.15 pulled in temporarily reported the identical message (`18 to the 15 allowed`) at `StorageEndpoints.cs(23,41)` on the pre-fix file, and reported nothing on the post-fix file in the same setup. Scaffolding (package refs + `.editorconfig` severity override) reverted; not part of the diff. |
| SonarCloud PR analysis (authoritative) | quality gate passed, 0 open issues on PR #73 for both backend and frontend. The `develop` finding stays OPEN until the post-merge analysis of `develop` runs. |
| CI | all 13 jobs green, including `3 · Backend quality gate` (`sonar.qualitygate.wait=true`) |
| Automated AI review (G2) | CodeRabbit auto-reviewed, profile `assertive`: 4/4 pre-merge checks passed, zero findings, no inline comments |
| Acceptance criteria | no SPEC-001 AC added, removed, or reinterpreted — route registrations were relocated. All nine registrations are exercised over real HTTP by the pre-existing AC-derived service tests, including the malformed-id paths (`/storages/abc`, `/storages/abc/items`, `/storages/{id}/items/abc`) that cover the guards being moved. AC→test mapping in the PR description. |

## Outcome

- **Result:** ready for merge — all 13 CI jobs green, CodeRabbit clean, SonarCloud PR
  analysis at 0 open issues, human attestation given by the orchestrator. Merge is a
  human's call (G3).
- **Deviations from spec:** none — no SPEC-001 acceptance criterion is touched.
- **Deferred, now tracked as issues** (repo rule: deferred findings become `tech-debt`
  issues, never prose):
  - **#74** — run `SonarAnalyzer.CSharp` at build time so S3776-class findings fail gate 1
    instead of gate 3. Kept separate because enabling the full rule set will surface an
    unknown backlog in existing code that needs triage before the rules break the build.
  - **#75** — collapse the duplicated two-id guards in `updateItem`/`deleteItem`. Would drop
    `MapItemRoutes` from 12 to 8 and remove real duplication, but it changes handler bodies,
    which this PR deliberately did not.
- **Harness follow-up:** #74 is the harness sharpening for this class of finding. Second,
  smaller lesson recorded under intervention 3: rewriting a PR body is a read-modify-write
  on a document humans also edit — diff the live version first.
