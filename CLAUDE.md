# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.
This file also serves as `AGENTS.md` for other AI tools.

## Process

This repository follows the **KAIFe Framework (L4)** — read
[`docs/process/PROCESS.md`](docs/process/PROCESS.md) first: the three gates (G1 Spec
Freeze · G2 Review · G3 DoD/Merge), the orchestrator loop, branching, isolation, and
artifacts are defined there. The five personas are installed in `.claude/agents/`
(canonical: `docs/process/personas/` — edit there, then re-copy).

## Repo-Specific Rules

- **Default branch is `develop`** (since 2026-08-18): tools that read repo-level config
  do so from the default branch — Renovate reads `renovate.json` there and nowhere else
  (see `docs/agent-logs/2026-08-18-renovate-config-source-of-truth.md`). `main` receives
  release merges only.
- **WIP limit: max. 3** open agent branches/PRs at a time (calibrated value; the rule is
  in the process doc).
- **Commit hook activation** (once per clone): `git config core.hooksPath .githooks` —
  Conventional Commits are enforced locally, details in
  `docs/guidelines/coding-guidelines.md`.
- **Permission tiers** are configured in `.claude/settings.json` (concept in the process
  doc; stack-specific commands per `docs/SETUP.md`).

## Project Structure

| Path | Content |
|------|---------|
| `backend/` | .NET solution — API-first REST backend (consumed by Angular and later the iPhone app) |
| `frontend/` | Angular app |

## Source of Truth

| Path | Content |
|------|---------|
| `docs/process/` | The KAIFe L4 process (project-independent, reusable) |
| `docs/project/tech-stack.md` | Technology stack — the single source for stack facts |
| `docs/specs/` | Specs + acceptance criteria (basis for G1 and tests) |
| `docs/architecture/` | arc42 architecture doc (`ARCHITECTURE.md`), ADRs (basis for the architecture conformance gate) |
| `docs/guidelines/` | Coding and test guidelines (basis for agent work) |
| `docs/agent-logs/` | One run log per agent task (transparency / compliance, DoD requirement) |
| `docs/metrics.md` | Pilot metrics — Flow + Quality tracking (KAIFe §8) |

Keep this file short — process content belongs in `docs/process/`, project detail in the
files above.

## Metadata

```text
last_updated: 2026-08-30
owner: Marcel Steiner (AI Steward)
scope: store-it — digital pantry management
stack: see docs/project/tech-stack.md
process_version: 1.0 (docs/process/CHANGELOG.md)
```
