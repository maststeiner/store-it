# Architecture Documentation

> **Template:** arc42 (arc42.org)
> **Owner:** Architecture Stewardship
> **Last updated:** YYYY-MM-DD
> **Status:** Draft

---

## 1. Introduction and Goals

### Requirements Overview
<!-- Top 3-5 functional requirements that significantly influence the architecture -->

### Quality Goals
<!-- Top 3-5 quality attributes (ISO 25010), ordered by priority -->

| Priority | Quality Attribute | Motivation |
|----------|-------------------|------------|
| 1 | | |
| 2 | | |
| 3 | | |

### Stakeholders
| Role | Name / Team | Expectations |
|------|-------------|--------------|
| | | |

---

## 2. Architecture Constraints

### Technical Constraints
| Constraint | Background |
|------------|------------|
| TODO: language / runtime | Defined per project (see `docs/SETUP.md`) |
| Azure DevOps | CI/CD platform |
| Claude Code | AI orchestration tool |

### Organizational Constraints
| Constraint | Background |
|------------|------------|
| | |

### Conventions
| Convention | Background |
|------------|------------|
| arc42 | Architecture documentation structure |
| EARS notation | Acceptance criteria format (see `docs/specs/`) |
| SOLID principles | See `docs/guidelines/coding-guidelines.md` |

---

## 3. System Scope and Context

### Business Context
<!-- What are the external actors (users, external systems) and what data flows between them and the system? -->

```
[External Actor] --[data/event]--> [System] --[data/event]--> [External Actor]
```

### Technical Context
<!-- Which technical interfaces exist? Which protocols/channels are used? -->

| Interface | Technology | Direction |
|-----------|------------|-----------|
| | | |

---

## 4. Solution Strategy

<!-- Key technical decisions and approaches that shape the architecture -->

| Goal / Constraint | Approach | Details |
|-------------------|----------|---------|
| | | |

---

## 5. Building Block View

### Level 1 — Whitebox: Overall System
<!-- Top-level decomposition of the system into building blocks -->

```
[System]
├── [Module A]
├── [Module B]
└── [Module C]
```

| Building Block | Responsibility |
|----------------|----------------|
| | |

### Level 2 — Blackbox Descriptions
<!-- Interface and responsibility of each top-level building block -->

---

## 6. Runtime View

### Scenario 1: [Name]
<!-- Sequence or flow diagram for the most important runtime scenario -->

```
Actor → System → Module A → Module B
```

---

## 7. Deployment View

### Infrastructure
<!-- Where does the system run? Which nodes/environments exist? -->

| Environment | Infrastructure | Notes |
|-------------|----------------|-------|
| Development | Local | |
| CI | Azure Pipelines | |
| Production | TODO | |

---

## 8. Cross-cutting Concepts

### Security
<!-- Authentication, authorization, input validation, secret management -->

### Error Handling & Logging
<!-- Strategy for exceptions, logging levels, correlation IDs -->

### Testability
<!-- Test pyramid, test isolation strategy — see `docs/guidelines/test-guidelines.md` -->

### AI Agent Integration
<!-- How Claude Code agents interact with the codebase; harness constraints -->

---

## 9. Architecture Decisions (ADRs)

Individual decisions are documented using the ADR template (`ADR-TEMPLATE.md`).

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| ADR-001 | | Proposed | YYYY-MM-DD |

---

## 10. Quality Requirements

### Quality Tree
<!-- Refinement of quality goals from section 1 into measurable scenarios -->

| Quality Attribute | Scenario | Metric / Threshold |
|-------------------|----------|--------------------|
| | | |

---

## 11. Risks and Technical Debt

| ID | Risk / Debt | Probability | Impact | Mitigation |
|----|-------------|-------------|--------|------------|
| R1 | | | | |

---

## 12. Glossary

| Term | Definition |
|------|------------|
| KAIFe | KMS Agile Intelligence Framework |
| EARS | Easy Approach to Requirements Syntax |
| ADR | Architecture Decision Record |
| BMAD | Build More Architect Dreams |
| Harness | Context Engineering artifacts (CLAUDE.md, guidelines, tooling rules) |
| Gate | Non-negotiable human checkpoint (G1 Spec Freeze, G2 Review, G3 DoD/Merge) |
