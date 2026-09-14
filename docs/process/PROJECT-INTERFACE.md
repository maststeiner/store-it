# Project Interface

The process in this subtree is project-independent. Everything project-specific lives
in the project itself, at the **conventional paths** declared here. This file is the
contract between the two layers: process and persona files may reference project
content **only** through these paths.

A project adopting this process must provide:

| Conventional path | Purpose | Primary consumers |
|-------------------|---------|-------------------|
| `CLAUDE.md` (repo root) | Repo-specific operational rules: pointer to this process, calibrated values (e.g. the WIP limit), branching specifics, local hook activation | Orchestrator, all personas |
| `docs/project/tech-stack.md` | Single source of truth for the technology stack (languages, frameworks, formatters, test frameworks, repo layout) | architect, developer, qa |
| `docs/guidelines/coding-guidelines.md` | Coding rules: principles, naming, error handling, commit conventions | developer, reviewer |
| `docs/guidelines/test-guidelines.md` | Test structure, naming, coverage/effectiveness gates and their calibrated thresholds, test data strategy | qa, developer, reviewer |
| `docs/architecture/ARCHITECTURE.md` | Architecture documentation (arc42), including the ADR register | architect, developer, reviewer |
| `docs/architecture/ADR-*.md` | Architecture decision records (from `templates/ADR-TEMPLATE.md`) | architect, reviewer |
| `docs/specs/` | Spec instances (from `templates/SPEC-TEMPLATE.md`) — the frozen spec is the G1 artifact | analyst (authors), all personas (consume) |
| `docs/agent-logs/` | One run log per agent task (from `templates/AGENT-LOG-TEMPLATE.md`) — the accountability record | Orchestrator |
| `docs/metrics.md` | Project instance of the process metrics concept (flow + quality) | Orchestrator |
| `.claude/settings.json` | Concrete permission-tier allowlist (the *concept* is defined in [`PROCESS.md`](PROCESS.md)) | All personas |
| `.claude/agents/` | Installed, byte-identical copies of [`personas/`](personas/); the project's CI verifies the identity (`diff -r`) as part of Gate G3 | Claude Code (subagent dispatch) |
| `.github/pull_request_template.md` | Instantiated copy of `templates/pull-request-template.md`, extended with project-specific checklist items | Reviewer, human at G2/G3 |

## Rules

- **Direction:** process → interface paths only. A process or persona file must never
  name a project fact directly (stack, thresholds, product names) — it points at the
  interface path that owns the fact.
- **Missing files:** if a project does not yet provide one of these files, creating it
  is part of adopting the process (see the installation steps in [`README.md`](README.md)).
- **Renaming:** these paths are convention. A project that must deviate documents the
  mapping in its `CLAUDE.md` — but the default is: don't deviate.
