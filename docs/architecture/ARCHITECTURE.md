# Architecture Documentation — store-it

> **Template:** arc42 (arc42.org)
> **Owner:** Marcel Steiner (Architecture Stewardship)
> **Last updated:** 2026-07-09
> **Status:** Draft

---

## 1. Introduction and Goals

### Requirements Overview

store-it is a digital pantry management application:

1. **Track storage contents** — users record what is in a storage (pantry, freezer, …).
2. **Add / remove items** — with name, quantity, and expiry date.
3. **Expiry transparency** — see at a glance what expires soon.
4. **Shared storages** — multiple accounts access the same storage (family, flat-share).
5. **Multi-client** — Angular web app first (fast testing vehicle); the iPhone app follows and becomes the **leading client**, on the same API.
6. **Multilingual** — UI in German, English, French, Italian.

### Quality Goals

| Priority | Quality Attribute | Motivation |
|----------|-------------------|------------|
| 1 | Usability | "Jedermensch" audience — adding an item must be faster than a paper list |
| 2 | Reliability of expiry data | The core value promise; wrong expiry info destroys trust |
| 3 | Evolvability | iPhone app and further clients planned — API-first, clean contracts |
| 4 | Security | Multi-account sharing → authentication, authorization per storage |

### Stakeholders

| Role | Name / Team | Expectations |
|------|-------------|--------------|
| Owner / Orchestrator | Marcel Steiner | KAIFe L4 process works end-to-end on a real product |
| End users | Households, flat-shares | Simple, fast, trustworthy pantry overview |

---

## 2. Architecture Constraints

### Technical Constraints

| Constraint | Background |
|------------|------------|
| .NET (C#) backend | Chosen stack (KAIFe pilot alignment) |
| Angular (TypeScript) frontend | Chosen stack |
| Cloud-native / Kubernetes | Target runtime; containerized services, 12-factor principles |
| GitHub + GitHub Actions | Repo + CI/CD platform (deviates from KAIFe pilot default Azure DevOps — private project) |
| Claude Code | AI orchestration tool (KAIFe L4) |

### Organizational Constraints

| Constraint | Background |
|------------|------------|
| KAIFe L4 process | Spec-driven, three human gates, agent personas — see `CLAUDE.md` |
| Solo orchestrator | One human covers all stewardship hats |

### Conventions

| Convention | Background |
|------------|------------|
| arc42 | Architecture documentation structure |
| EARS notation | Acceptance criteria format (see `docs/specs/`) |
| SOLID principles | See `docs/guidelines/coding-guidelines.md` |
| Clean Architecture (R. C. Martin) | Dependency rule governs backend layering (ADR-001); see coding guidelines |
| Twelve-Factor App | Cloud-native baseline for the k8s target; see coding guidelines |
| API-first | Every feature is exposed via the REST API before any client consumes it (ADR-002) |

---

## 3. System Scope and Context

### Business Context

```
[User (browser)] ──uses──> [Angular Web App] ──REST/JSON──> [store-it API]
[User (iPhone, planned)] ──uses──> [iOS App] ──REST/JSON──> [store-it API]
[store-it API] ──persists──> [Database]
```

External actors: end users via web (later iOS). No third-party system integrations in the MVP.

### Technical Context

| Interface | Technology | Direction |
|-----------|------------|-----------|
| Web UI ↔ API | HTTPS / REST / JSON | bidirectional |
| API ↔ Database | PostgreSQL via EF Core (ADR-003) | outbound |
| Auth | TODO: choose identity provider / scheme (see open ADRs) | — |

---

## 4. Solution Strategy

| Goal / Constraint | Approach |
|-------------------|----------|
| Multi-client (web + iOS) | API-first REST backend; clients are thin consumers (ADR-002) |
| Evolvability + AI-agent workability | Monorepo `backend/` + `frontend/` with enforced layering (ADR-001) |
| Kubernetes target | Twelve-Factor App: containerized from the start, config via environment, stateless processes, logs to stdout, health endpoints |
| Maintainable core under AI velocity | Clean Architecture: framework-free Domain, use cases in Application, frameworks at the edges (details in coding guidelines) |
| Quality despite AI velocity | KAIFe DoD gates in CI (build/test/coverage, Trivy+SBOM, quality, architecture conformance, format) |

---

## 5. Building Block View

### Level 1 — Whitebox: Overall System

```
store-it
├── frontend/   Angular SPA
└── backend/    .NET API
    ├── Api            (HTTP layer: controllers/endpoints, request/response DTOs)
    ├── Application    (use cases, orchestration, validation)
    ├── Domain         (entities, domain rules — no outward dependencies)
    └── Infrastructure (persistence, external services)
```

| Building Block | Responsibility |
|----------------|----------------|
| `frontend/` | UI, client-side state, calls the API — no business rules |
| `backend/Api` | HTTP contract, authentication middleware, serialization |
| `backend/Application` | Use cases (add item, remove item, list storage, share storage) |
| `backend/Domain` | Core model: Storage, Item, Membership, expiry rules |
| `backend/Infrastructure` | Database access, identity integration |

Layering rules: see ADR-001 (enforced via the architecture conformance gate).

### Level 2 — Blackbox Descriptions

TODO — refine once the first feature slices exist.

---

## 6. Runtime View

### Scenario 1: Add item to a storage

```
User → Angular: fill item form (name, quantity, expiry)
Angular → API: POST /storages/{id}/items
API → Application: AddItem use case (validate, authorize membership)
Application → Domain/Infrastructure: create + persist item
API → Angular: 201 Created (item DTO) → UI updates list
```

TODO — add scenarios for expiry overview and storage sharing when specced.

---

## 7. Deployment View

| Environment | Infrastructure | Notes |
|-------------|----------------|-------|
| Development | Local (dotnet run / ng serve) | |
| CI | GitHub Actions | DoD gates, see `.github/workflows/ci.yml` |
| Production | Kubernetes | TODO: cluster/provider, ingress, DB hosting — decide via ADR |

---

## 8. Cross-cutting Concepts

### Security
- Authentication + authorization required for every API call; access to a storage requires membership.
- TODO: identity solution (ADR pending).

### Error Handling & Logging
- Problem-details style API errors; no internal details leaked to clients.
- Structured logging; correlation IDs per request (k8s-friendly).

### UI Design Principle
- **Modern but minimal:** clean typography, generous whitespace, reduced palette; color carries meaning (status), not decoration. Applies to all clients (web, later iOS).

### Internationalization
- UI fully localized: **de, en, fr, it** (Swiss market); default from browser locale, manual override.
- The API is locale-neutral: ISO-8601 dates, enum codes instead of translated strings — translation happens exclusively in the clients.

### Testability
- Test pyramid: unit → service (contract/API) → integration — see `docs/guidelines/test-guidelines.md`.

### AI Agent Integration
- KAIFe harness: `CLAUDE.md`, personas in `.claude/agents/`, guidelines in `docs/guidelines/`.
- Layering rules exist as architecture tests so agent output is machine-checked (structural debt = 0).

---

## 9. Architecture Decisions (ADRs)

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [ADR-001](ADR-001-monorepo-layering.md) | Monorepo with enforced backend layering | Accepted | 2026-07-09 |
| [ADR-002](ADR-002-api-first.md) | API-first backend for all clients | Accepted | 2026-07-09 |
| [ADR-003](ADR-003-persistence.md) | PostgreSQL + EF Core for persistence | Accepted | 2026-07-09 |
| ADR-004 | Identity / auth solution | TODO | |
| ADR-005 | Kubernetes hosting & deployment strategy | TODO | |

---

## 10. Quality Requirements

| Quality Attribute | Scenario | Metric / Threshold |
|-------------------|----------|--------------------|
| Usability | Add an item (web form) | ≤ 15 seconds, one screen |
| Reliability | Expiry list is consistent with stored items | Zero tolerance — covered by service tests |
| Evolvability | New client (iOS) consumes the API | No API changes needed that break the web client |
| Security | User without membership calls a storage endpoint | 403, covered by service tests |

---

## 11. Risks and Technical Debt

| ID | Risk / Debt | Probability | Impact | Mitigation |
|----|-------------|-------------|--------|------------|
| R1 | AI-generated code erodes layering | medium | high | Architecture conformance gate in CI (0 violations) |
| R2 | Solo orchestrator = review bottleneck | high | medium | WIP limit (3), small specs, automated review as first filter |
| R3 | Auth/sharing complexity underestimated | medium | high | Dedicated spec + ADR-004 before implementation |

---

## 12. Glossary

| Term | Definition |
|------|------------|
| Storage | A managed container (pantry, freezer, shelf) holding items |
| Item | An entry in a storage: name, quantity, expiry date |
| Membership | The link between an account and a storage (sharing) |
| KAIFe | KMS Agile Intelligence Framework |
| EARS | Easy Approach to Requirements Syntax |
| ADR | Architecture Decision Record |
| Gate | Non-negotiable human checkpoint (G1 Spec Freeze, G2 Review, G3 DoD/Merge) |
| Harness | Context engineering artifacts (CLAUDE.md, guidelines, tooling rules) |
