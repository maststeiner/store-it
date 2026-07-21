# Agent Run Log: SPEC-001 Architect Plan

> **Date:** 2026-07-18
> **Spec:** [SPEC-001](../specs/SPEC-001-manage-storage-items.md) (frozen 2026-07-13, G1)
> **Persona(s):** architect
> **Model:** Claude Fable 5 (Claude Code)
> **Branch / PR:** `feature/spec-001-manage-storage-items` (PR follows)

---

## Task

Design the implementation of SPEC-001 (manage storages and items) within ADR-001/002/003/006: REST API, persistence, contract gate, frontend structure, test strategy.

## Plan

### API design (`/api/v1`, ADR-006)

| Endpoint | AC | Semantics |
|----------|----|-----------|
| `GET /api/v1/storages` | AC-01 | List storages (id, name, itemCount) |
| `POST /api/v1/storages` | AC-01/02 | Create; 400 ProblemDetails on empty name |
| `PUT /api/v1/storages/{id}` | AC-03 | Rename; same validation |
| `DELETE /api/v1/storages/{id}` | AC-04 | Delete incl. items (confirmation is UI concern) |
| `GET /api/v1/storages/{id}/items` | AC-10..12 | Items **sorted per AC-10**, each with server-computed `expiryStatus` (Ok/ExpiringSoon/Expired) |
| `POST /api/v1/storages/{id}/items` | AC-05/06 | Add item; validation per AC-06 |
| `PUT /api/v1/storages/{id}/items/{itemId}` | AC-07/08 | Edit; **amount = 0 ⇒ item is removed** (AC-08) |
| `DELETE /api/v1/storages/{id}/items/{itemId}` | AC-09 | Delete |

- Errors: RFC-7807 ProblemDetails with **locale-neutral error codes** (i18n happens in clients).
- Status computation and sorting are **server-side domain logic** (ADR-002: no business rules in clients). The client groups by the delivered `expiryStatus` field — pure presentation.

### Domain / Application / Infrastructure

- **Domain:** `Storage` aggregate root (Id, Name, Items) + `Item` (Name, Amount [decimal, 1 decimal place], Unit, ExpiryDate?, ProductionDate?). Invariants as factory/validation methods reusing existing `ExpiryRules`/`Unit`. Domain stays framework-free.
- **Application:** one use-case class per operation (no MediatR — simplicity per guidelines); `IStorageRepository` port (per aggregate).
- **Infrastructure:** EF Core `StoreItDbContext` + Npgsql (ADR-003); mapping via configuration classes (no attributes on entities); migrations in Infrastructure, applied as separate process (12-factor admin process, dev via `dotnet ef database update`); connection string from environment.
- **Local dev:** `compose.yaml` with PostgreSQL (podman-compatible).

### OpenAPI contract (ADR-006)

- .NET built-in OpenAPI generation (`Microsoft.AspNetCore.OpenApi` + `Microsoft.Extensions.ApiDescription.Server`) emits the spec at build time → committed as `backend/openapi/StoreIt.Api.json`.
- CI job: drift check (regenerate + compare) and `oasdiff breaking` against the develop baseline. Not a required check until first green runs.

### ⚠️ ADR-001 amendment required (human decision)

DI composition needs `Program.cs` to register Infrastructure services — but ADR-001 forbids **Api → Infrastructure** entirely. Standard Clean Architecture answer: the **composition root** (outermost point) is the one place allowed to reference Infrastructure. Proposal:
- ADR-001 amendment: "Api MUST NOT depend on Infrastructure, **except the composition root (`Program`)** for DI registration."
- Enforcement stays two-layered: NetArchTest rule excludes only `Program`; ProjectsRuler reference rule gets an Allowed exception with this description.
- Per architect hard limits, this layering change **requires explicit human approval**.

### Frontend (Angular)

- Routes: `/storages` (list + create/rename/delete with confirm dialog), `/storages/:id` (detail: grouped item list per `expiryStatus`, item form: name, amount+unit, expiry/production date).
- **i18n:** runtime language switching de/en/fr/it per spec (Angular's built-in `@angular/localize` would need one build per locale). _Implementation note: the frontend ultimately used a minimal in-house translate service instead of ngx-translate — see the frontend run log (2026-07-20)._
- Design: modern-but-minimal per spec §UI; status colors as the only semantic colors.
- API client: thin typed service, no business rules (ADR-002).

### Test strategy

- **Unit (Domain/Application):** from ACs/ECs — validation rules, amount-zero removal, sorting/status.
- **Service (contract):** WebApplicationFactory against **Testcontainers PostgreSQL** (dev/prod parity — no in-memory substitute per guidelines) — every endpoint per AC incl. 400/404 paths.
- **E2E (Playwright, new):** three flows — create storage, add item, expiry grouping visible.
- **StrykerJS** for the frontend once components exist (backend Stryker already gated).

## Key Decisions

1. Composition-root exception in ADR-001 — **pending Marcel's approval**
2. Server-side status + sorting, flat list with `expiryStatus`; grouping = client presentation
3. `PUT` item with amount 0 removes the item (AC-08 semantics on the edit path)
4. ngx-translate over @angular/localize (runtime switching required)
5. Plain use-case classes, no MediatR (simplicity over ceremony)
6. Contract as committed `StoreIt.Api.json` (build-time generated), oasdiff gate per ADR-006

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Plan approved by Marcel (2026-07-19), incl. composition-root exception in ADR-001 | KAIFe: Run → Inspect → Challenge; layering change requires human approval |
| 2 | UI design direction to be refined in parallel (examples from Marcel → HTML mockup → spec update) | Orchestrator decision |

## Outcome

- **Result:** plan approved — implementation started (developer/qa personas)
- **Deviations from spec:** none
- **Harness follow-up:** ADR-001 amended (composition-root exception), architecture gates adjusted accordingly
