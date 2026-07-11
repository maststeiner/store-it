# ADR-003: PostgreSQL + EF Core for persistence

> **Status:** Accepted
> **Date:** 2026-07-09
> **Deciders:** Marcel Steiner

---

## Context

store-it needs persistent storage for storages, items, and (later) accounts/memberships. The runtime target is Kubernetes (cloud-native); the backend is layered .NET (ADR-001) with Infrastructure implementing repository interfaces.

## Decision

**PostgreSQL** as the database, accessed via **EF Core** from the Infrastructure layer.

## Rationale

- PostgreSQL is the de-facto standard relational DB for cloud-native/k8s workloads — managed offerings exist at every provider (relevant for ADR-005).
- EF Core fits the layering: `DbContext`/repositories live in Infrastructure, Domain stays persistence-agnostic.
- Migrations give schema evolution discipline (migration changes are Approval-tier per `CLAUDE.md`).
- Integration tests run against real PostgreSQL via Testcontainers — no in-memory-provider false confidence.
- Alternative SQLite rejected: cheaper start, but a later migration costs more than starting on PostgreSQL; concurrency/JSON support weaker.

## Consequences

**Positive:**
- Production-grade from the start; clean fit with layering and test strategy.

**Negative / Trade-offs:**
- Local development needs a running PostgreSQL (docker/podman compose).
- EF Core convenience can tempt leaking `IQueryable`/entities across layers — Reviewer Agent watches for this; repository interfaces return domain types.
