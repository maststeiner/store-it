# The Process Subtree

This directory contains the **AI-Dev Process** — the project-independent part of how this repository is developed. Everything in here is written to be reused, unchanged, in any other project.

## What lives here

| File / directory | Purpose |
|------------------|---------|
| [`PROCESS.md`](PROCESS.md) | The main process document: gates, workflow, orchestrator loop, personas, branching, artifacts |
| [`PROJECT-INTERFACE.md`](PROJECT-INTERFACE.md) | The contract: which files a project must provide at which conventional paths |
| [`DEFINITION-OF-DONE.md`](DEFINITION-OF-DONE.md) | The explicit, generic Definition of Done behind Gate G3 |
| [`CHANGELOG.md`](CHANGELOG.md) | Version history of the process itself |
| [`personas/`](personas/) | Canonical persona definitions (analyst, architect, developer, qa, reviewer) |
| [`templates/`](templates/) | Templates for specs, agent logs, ADRs, and pull requests |

## Rules for this subtree

1. **Zero project-specific content.** No stack names, no thresholds, no dates, no product or repository names. If a fact is true only for one project, it belongs in that project's files (see `PROJECT-INTERFACE.md`), never here.
2. **One-way references.** Files in this subtree may reference project files **only** through the conventional paths declared in `PROJECT-INTERFACE.md` (e.g. "the project's coding guidelines at `docs/guidelines/coding-guidelines.md`"). Project files may reference process files freely; the reverse direction is limited to the declared interface.
3. **Rules here, values in the project.** The process states rules and recommends starting points; calibrated values (a WIP limit, a coverage threshold) live in the project files the interface names.
4. **Changes are versioned.** Every substantive change to this subtree gets an entry in `CHANGELOG.md` and, when meaningful, a version bump in `PROCESS.md`.

## Reuse in a new project

The reuse model is **copy, not reference** — each project owns its copy and can pin or adapt it deliberately:

1. Copy this directory into the new repository: `cp -r docs/process <new-repo>/docs/`
2. Create the project-side files listed in `PROJECT-INTERFACE.md`.
3. Install the personas as Claude Code subagents: `mkdir -p .claude/agents && cp docs/process/personas/*.md .claude/agents/` — and add a CI job that fails when the two directories differ (`diff -r docs/process/personas .claude/agents`).
4. Instantiate `templates/pull-request-template.md` as `.github/pull_request_template.md` and add project-specific checklist items there.
5. Record the origin process version (from `PROCESS.md`) in the project's `CLAUDE.md` metadata, so later diffs against a newer process version are possible.
