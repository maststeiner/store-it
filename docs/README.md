# Documentation Index

The documentation is organized in **three layers** with one-way references
(process → personas → project):

1. **Process layer** — [`process/`](process/): the AI-Dev Process,
   project-independent and reusable by copying the subtree. Main file:
   [`process/PROCESS.md`](process/PROCESS.md). The personas in
   [`process/personas/`](process/personas/) are canonical; `.claude/agents/` holds
   installed copies.
2. **Project interface** — the files [`process/PROJECT-INTERFACE.md`](process/PROJECT-INTERFACE.md)
   requires the project to provide, at conventional paths.
3. **Project layer** — everything below that is specific to store-it.

## Contents

| Path | Layer | Content |
|------|-------|---------|
| [`process/`](process/) | Process | AI-Dev Process: process, project interface, Definition of Done, changelog, personas, templates |
| [`project/tech-stack.md`](project/tech-stack.md) | Project | Single source of truth for the technology stack |
| [`guidelines/`](guidelines/) | Project | [Coding](guidelines/coding-guidelines.md) and [test](guidelines/test-guidelines.md) guidelines (incl. calibrated gate thresholds) |
| [`architecture/`](architecture/) | Project | arc42 doc ([`ARCHITECTURE.md`](architecture/ARCHITECTURE.md)) + ADRs |
| [`specs/`](specs/) | Project | Spec instances — the frozen spec is the Gate G1 artifact |
| [`agent-logs/`](agent-logs/) | Project | One run log per agent task — the accountability record (never rewritten) |
| [`metrics.md`](metrics.md) | Project | Pilot metrics: flow + quality data points and trend (process metrics concept) |
| [`security/`](security/) | Project | Threat model, incl. AI-development-specific risks |
| [`brand/`](brand/) | Project | Brand assets (logo lockups, marks, favicon source) |
| [`SETUP.md`](SETUP.md) | Project | Project setup checklist (incl. platform-side items) |

Repo-specific operational rules (default branch, WIP value, hook activation) live in
the root [`CLAUDE.md`](../CLAUDE.md).
