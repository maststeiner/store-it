# TeeMplate — KAIFe L4 Project Template

A technology-independent project template implementing the **KAIFe Framework (Level 4)**: an AI-driven, Scrum-based development process where the human is the orchestrator and AI agents do the implementation work — inside hard, non-negotiable quality gates.

## What's inside

| Path | Purpose |
|------|---------|
| `CLAUDE.md` | Orchestration rules for Claude Code (also serves as `AGENTS.md`) |
| `.claude/agents/` | 5 BMAD agent personas: analyst, architect, developer, qa, reviewer |
| `.claude/settings.json` | Permission tiers (auto vs. approval) |
| `docs/specs/` | Spec template — every work item starts here (Gate 1) |
| `docs/architecture/` | arc42 architecture doc + ADR template |
| `docs/guidelines/` | Coding and test guidelines (the "harness") |
| `docs/agent-logs/` | Agent run logs (transparency / compliance, DoD requirement) |
| `docs/SETUP.md` | **Start here** — checklist for turning this template into a project |
| `azure-pipelines.yml` | DoD gates as CI stages (build/test, security+SBOM, quality, architecture, format) |
| `.github/pull_request_template.md` | Gate G2/G3 checklist for every PR |

## The three gates (non-negotiable)

1. **G1 · Spec Freeze** — no implementation without a human-frozen spec
2. **G2 · Review** — no merge without automated + human code review
3. **G3 · DoD/Merge** — pipeline fully green; only a human merges

## Getting started

1. Create a new repository from this template.
2. Work through **`docs/SETUP.md`** — it walks you through choosing your stack, wiring the pipeline placeholders, and configuring platform policies.
3. Write your first spec from `docs/specs/SPEC-TEMPLATE.md` and freeze it (Gate 1).
4. Orchestrate: **Run → Inspect → Challenge → Refine → Re-run.**

## Principles (from KAIFe)

- **Behavior over output** — agents are roles with behavior, boundaries, and responsibility
- **Discipline beats complexity** — simple, composable patterns + hard quality gates
- **The human stays accountable** — AI is a multiplier, every agent output needs human verification
