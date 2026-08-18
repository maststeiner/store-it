# ADR-001: Monorepo with enforced backend layering

> **Status:** Accepted
> **Date:** 2026-07-09
> **Deciders:** Marcel Steiner

---

## Context

store-it consists of a .NET backend and an Angular frontend, with an iPhone app planned. We need to decide how to organize the codebase and how to keep AI-agent-generated code structurally sound.

## Decision

1. **One repository (monorepo)** with `backend/` and `frontend/` top-level folders (later `mobile/`).
2. **Backend follows a strict layered structure** per Clean Architecture (Robert C. Martin): `Api` → `Application` → `Domain`, with `Infrastructure` implementing interfaces defined by `Application`/`Domain` — source dependencies point inward only.

## Rationale

- Cross-cutting changes (API contract + client) land in **one PR** — matches the KAIFe flow (one spec → one branch → one PR).
- One harness (`CLAUDE.md`, guidelines, personas) governs all parts; no drift between repos.
- One CI pipeline enforces all DoD gates for every change.
- Alternative (separate repos) rejected: coordination overhead and duplicated harness for a solo orchestrator outweigh independent versioning benefits.

## Consequences

**Positive:**
- Atomic contract changes; single source of truth for docs and process.

**Negative / Trade-offs:**
- CI must scope jobs per folder (path filters) as the repo grows.
- Repo permissions cannot differ between frontend and backend.

## Layering Rules (for the Architecture Conformance Gate)

```
Domain        MUST NOT depend on any other backend layer.
Application   MAY depend on Domain only.
Api           MAY depend on Application and Domain; MUST NOT depend on Infrastructure —
              EXCEPT the composition root (Program), which registers Infrastructure
              services for dependency injection.
Infrastructure MAY depend on Application and Domain.
frontend/     MUST NOT contain business rules (API is the only source of domain logic).
```

> **Amendment 2026-07-19 (approved by Marcel):** composition-root exception added.
> Standard Clean Architecture practice — the outermost entry point wires the object
> graph. Enforcement stays two-layered: the type-level gate (NetArchTest) excludes
> only `Program`; the reference-level rule (ProjectsRuler) allows the project
> reference with this documented justification.
