# ADR-006: API versioning and contract gate

> **Status:** Accepted
> **Date:** 2026-07-18 (harness retro decision)
> **Deciders:** Marcel Steiner

---

## Context

store-it is API-first (ADR-002); the Angular web app and the planned iPhone app consume the same REST API. Nothing currently prevents a breaking API change from silently reaching clients. Service tests verify behavior, but the contract *shape* is not versioned or diffed.

## Decision

1. **URL path versioning:** all endpoints live under `/api/v1/…`. A breaking change is only allowed as a **new version** (`/api/v2/…` with its own contract file); the old version stays frozen while any client uses it.
2. **The OpenAPI contract is a committed, reviewed artifact:** generated at build time (`Microsoft.Extensions.ApiDescription.Server`), stored in `backend/openapi/` (`StoreIt.Api.json` for the v1 document; additional documents per future version). Contract changes are visible in every PR diff (Gate G2 reviews them explicitly).
3. **Two-stage CI gate** (lands with the first endpoints, SPEC-001):
   - *Drift check:* regenerate the spec and compare with the committed file — fails when the API changes without updating the contract.
   - *Breaking check:* `oasdiff breaking <develop-baseline> <PR-spec> --fail-on ERR` — fails on breaking changes within the same version.

## Rationale

- Protects the iPhone app (and any future client) from silent contract breaks — the exact risk ADR-002 exists to manage.
- Committed contract + PR diff turns API design into a reviewable artifact instead of an implementation side effect.
- oasdiff classifies breaking vs. non-breaking changes; additive evolution stays cheap.
- Consumer-driven contract testing (Pact) deliberately deferred until a second real client exists.

## Consequences

**Positive:** contract visibility in reviews; machine-enforced compatibility; clear versioning policy.

**Negative / Trade-offs:** committed spec must be kept in sync (enforced by the drift check); version freezes require discipline once v2 exists.
