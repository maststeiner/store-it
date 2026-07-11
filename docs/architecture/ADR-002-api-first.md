# ADR-002: API-first backend for all clients

> **Status:** Accepted
> **Date:** 2026-07-09
> **Deciders:** Marcel Steiner

---

## Context

store-it starts with an Angular web app; an iPhone app is planned. Both need the same functionality (manage storages and items, sharing, expiry overview).

## Decision

The backend exposes **one REST/JSON API that is the single entry point for every client**. Every feature is designed and implemented at the API level first; clients are thin consumers without business rules.

## Rationale

- The iPhone app becomes an additive client — no backend rework.
- Service tests (contract/API level, see `docs/guidelines/test-guidelines.md`) verify behavior once, for all clients.
- Clear Gate-G2 review surface: the API contract is the spec's technical mirror.

## Consequences

**Positive:**
- Client-agnostic backend; contract-level testing; parallel client development possible.

**Negative / Trade-offs:**
- No UI-specific shortcuts (e.g. server-side rendering conveniences).
- API versioning discipline needed once the iPhone app ships (breaking changes hit two clients).
