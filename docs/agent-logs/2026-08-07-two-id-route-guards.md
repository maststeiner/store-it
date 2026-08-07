# Agent Run Log: Collapse the duplicated two-id route guards (issue #75)

> **Date:** 2026-08-07
> **Spec:** none — tech-debt issue #75; the issue body, including its Acceptance section, is
> the frozen input (repo convention for tech-debt items).
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `refactor/two-id-route-guards`

---

## Task

`updateItem` and `deleteItem` each parsed two route ids with the same guard written twice —
about 24 lines of near-identical code per handler. Collapse them into one helper without
changing any answer.

## Change

`TryParseRouteIds(storageId, itemId, out …, out …, out problem)` wraps the two
`TryParseRouteId` calls with `&&`, so the first malformed id wins and the handler keeps a
single four-line guard. Net effect on `StorageEndpoints.cs`: 30 lines removed, 27 added,
with the duplication gone.

The **order is part of the contract**, not an implementation detail: storage is checked
before item, so when both ids are malformed the answer names `storageId`. That was the
behaviour before (the old code guarded `storageId` first) and it is what issue #69 demands —
the same client error must answer the same way everywhere.

A small property worth knowing: only `parsedItemId` is pre-assigned to `Guid.Empty`, so the
storage-first order is **compiler-enforced**. Reversing the two calls without also touching
the pre-assignment fails to compile (`CS0177: out parameter 'parsedStorageId' must be
assigned`). Discovered by accident while trying to prove the new tests bite — a reversed
implementation cannot silently slip through.

## Verification

The issue's Acceptance section asked for two things: unchanged 400 answers for a malformed
`storageId` **and** for a malformed `itemId`, *including which parameter name appears in
`detail`*, with the existing service tests untouched as the regression net. Checked before
touching anything: `AssertInvalidRouteIdAsync(parameterName)` asserts status 400,
`application/problem+json`, the `request.invalidId` error code **and** that `detail` contains
the parameter name — with dedicated tests for a malformed storage id and a malformed item id
on both endpoints. So the net does cover the risk. Those six tests are unchanged and green.

**One gap found and closed.** Nothing pinned down *which* name is reported when **both** ids
are malformed — precisely the case a combined helper could get wrong, and the only thing
holding it was the `&&` order. Two tests added (`UpdateItem_BothIdsMalformed_…`,
`DeleteItem_BothIdsMalformed_…`). They are derived from the acceptance criterion (the
parameter name in `detail`), not from the implementation.

Those two tests were then checked for sharpness rather than assumed: with the helper's order
reversed (and both out-parameters pre-assigned so it compiles), **both fail** with
`Assert.Contains() Failure: Sub-string not found`. Restored, all green.

| Check | Result |
|-------|--------|
| `dotnet build -c Release` | 0 warnings / 0 errors — including the Sonar complexity rules now enforced as errors (#74) |
| `dotnet test -c Release` | 115/115 pass (Domain 46, Architecture 9, Api.Service 60 — was 58, +2 new) |
| The six pre-existing malformed-id tests | unchanged, green |
| New tests bite? | yes — reversed order ⇒ both fail; restored ⇒ green |
| `dotnet csharpier check .` | 48 files clean |
| OpenAPI contract | untouched (only `StorageEndpoints.cs` and the test file changed) → no drift |
| `dotnet stryker` | 81.63 %, break threshold 60 → passes (81.33 % before, so the added guard logic is covered at least as well as the code it replaced) |

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | *"issue 75 umsetzen"* | Task |

## Outcome

- **Result:** ready for review
- **Deviations from spec:** none. Both acceptance bullets are met; the second one is met more
  strictly than asked, since the both-malformed case is now covered by tests instead of only
  by the guard order.
- **Harness follow-up:** none. Note that `MapItemRoutes`-style grouping is no longer a
  complexity risk after #74/#78 (one method per endpoint), so this change is pure
  duplication removal rather than budget relief — the reason it was worth doing on its own
  is that the guard was written twice, not that anything was near a limit.
