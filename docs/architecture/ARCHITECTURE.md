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
| Owner / Orchestrator | Marcel Steiner | AI-Dev Process works end-to-end on a real product |
| End users | Households, flat-shares | Simple, fast, trustworthy pantry overview |

---

## 2. Architecture Constraints

### Technical Constraints

| Constraint | Background |
|------------|------------|
| .NET (C#) backend | Chosen stack (pilot alignment); versions and support horizon in [`docs/project/tech-stack.md`](../project/tech-stack.md) |
| Angular (TypeScript) frontend | Chosen stack; toolchain versions in [`docs/project/tech-stack.md`](../project/tech-stack.md) |
| Cloud-native / Kubernetes | Target runtime; containerized services, 12-factor principles |
| GitHub + GitHub Actions | Repo + CI/CD platform (deviates from the pilot default Azure DevOps — private project) |
| Claude Code | AI orchestration tool (AI-Dev Process) |

### Organizational Constraints

| Constraint | Background |
|------------|------------|
| AI-Dev Process | Spec-driven, three human gates, agent personas — see `docs/process/PROCESS.md` |
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

External actors: end users via web (later iOS). The only external system integration is the identity providers (Microsoft/Google OIDC) for sign-in (ADR-004); no other third-party integrations in the MVP.

### Technical Context

| Interface | Technology | Direction |
|-----------|------------|-----------|
| Web UI ↔ API | HTTPS / REST / JSON | bidirectional |
| API ↔ Database | PostgreSQL via EF Core (ADR-003) | outbound |
| Auth | OIDC to external providers (Microsoft/Google), BFF session — [ADR-004](ADR-004-identity-auth.md) | inbound (login) |

---

## 4. Solution Strategy

| Goal / Constraint | Approach |
|-------------------|----------|
| Multi-client (web + iOS) | API-first REST backend; clients are thin consumers (ADR-002) |
| Evolvability + AI-agent workability | Monorepo `backend/` + `frontend/` with enforced layering (ADR-001) |
| Kubernetes target | Twelve-Factor App: containerized from the start, config via environment, stateless processes, logs to stdout, health endpoints |
| Maintainable core under AI velocity | Clean Architecture: framework-free Domain, use cases in Application, frameworks at the edges (details in coding guidelines) |
| Quality despite AI velocity | DoD gates in CI (build/test/coverage, Trivy+SBOM, quality, architecture conformance, format) |

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
| Production | One VM (Oracle Cloud Always Free, Zurich), Docker Compose, Caddy for TLS | [ADR-005](ADR-005-hosting-deployment.md). Images from GHCR on release tags (`release.yml`); topology, host and runbook live in the private repository `store-it-deploy`, pulled by the host every 5 minutes. What the images need: [`docs/operations/runtime-contract.md`](../operations/runtime-contract.md). Kubernetes deferred (revisit triggers in the ADR). |

---

## 8. Cross-cutting Concepts

### Security
- Authentication + authorization required for every **protected** API call (`/api/v1/**`); `/health` and the `/auth/*` endpoints are anonymous by design (SPEC-003 allowlist). A storage is accessible to its owner and to its members (SPEC-007 / ADR-008: flat membership via invitation links). Members read the member list; owner-only are deleting the storage, removing members, the invitation link and the hand-over.
- Identity via external OIDC providers with a BFF session — see [ADR-004](ADR-004-identity-auth.md).

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
- AI-Dev harness: process in `docs/process/`, repo rules in `CLAUDE.md`, personas in `.claude/agents/` (canonical: `docs/process/personas/`), guidelines in `docs/guidelines/`.
- Layering rules exist as architecture tests so agent output is machine-checked (structural debt = 0).

---

## 9. Architecture Decisions (ADRs)

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [ADR-001](ADR-001-monorepo-layering.md) | Monorepo with enforced backend layering | Accepted | 2026-07-09 |
| [ADR-002](ADR-002-api-first.md) | API-first backend for all clients | Accepted | 2026-07-09 |
| [ADR-003](ADR-003-persistence.md) | PostgreSQL + EF Core for persistence | Accepted | 2026-07-09 |
| [ADR-004](ADR-004-identity-auth.md) | Identity / auth solution (direct OIDC federation, BFF) | Accepted | 2026-07-30 |
| [ADR-006](ADR-006-api-versioning-contract-gate.md) | API versioning and contract gate | Accepted | 2026-07-18 |
| [ADR-007](ADR-007-release-versioning.md) | Release process, SemVer tagging, breaking-change baseline | Accepted | 2026-07-31 |
| [ADR-008](ADR-008-storage-sharing-model.md) | Storage sharing: one owner, flat membership, link invitations | Accepted | 2026-09-23 |
| [ADR-005](ADR-005-hosting-deployment.md) | Hosting and deployment — separate deployment repository, one free Oracle Cloud VM, Docker Compose, pull-based updates | Accepted | 2026-09-18 |

---

## 10. Quality Requirements

| Quality Attribute | Scenario | Metric / Threshold |
|-------------------|----------|--------------------|
| Usability | Add an item (web form) | ≤ 15 seconds, one screen |
| Reliability | Expiry list is consistent with stored items | Zero tolerance — covered by service tests |
| Evolvability | New client (iOS) consumes the API | No API changes needed that break the web client |
| Security | User requests a storage they neither own nor belong to, by id | 404 — existence not disclosed (SPEC-003, unchanged by sharing); a member calling an owner-only operation gets 403 `storage.ownerOnly` (SPEC-007) | covered by service tests |

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
| EARS | Easy Approach to Requirements Syntax |
| ADR | Architecture Decision Record |
| Gate | Non-negotiable human checkpoint (G1 Spec Freeze, G2 Review, G3 DoD/Merge) |
| Harness | Context engineering artifacts: process in `docs/process/`, repo rules in `CLAUDE.md`, personas in `.claude/agents/` (canonical: `docs/process/personas/`), guidelines in `docs/guidelines/`, tooling rules |
