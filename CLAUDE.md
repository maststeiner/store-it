# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.
This file also serves as `AGENTS.md` for other AI tools.

## KAIFe L4 — Orchestration Rules

This repository follows the **KAIFe Framework (L4)**: KMS Agile Intelligence Framework. The human is the orchestrator, AI is the multiplier.

**Three non-negotiable gates:**
- **G1 · Spec Freeze:** No agent starts implementation without a human-frozen spec (`docs/specs/`).
- **G2 · Review:** No PR is merged without an automated AI review pass + human code review.
- **G3 · DoD/Merge:** The CI pipeline must be fully green. Only a human merges to `main`.

## Agent Personas

Five BMAD personas are available in `.claude/agents/`. Always activate the appropriate persona:

| Persona | Task |
|---------|------|
| `analyst` | Requirements → testable user stories (EARS notation) |
| `architect` | Design structure, enforce layering boundaries, write ADRs |
| `developer` | Implement *within* the architecture constraints |
| `qa` | Derive tests from acceptance criteria (never from code) |
| `reviewer` | Adversarial review: security, duplicates, tech debt |

Each persona has **hard limits that are never crossed** — not even when explicitly asked.

## Orchestrator Loop

For every task: **Run → Inspect → Challenge → Refine → Re-run**

When output systematically deviates from the goal: don't only fix the code — sharpen the harness (guidelines in `docs/guidelines/` or the persona definition in `.claude/agents/`). The fix then applies to all future runs.

## Branching Model

| Branch | Purpose | Rules |
|--------|---------|-------|
| `main` | Releases only | Only receives merges from `develop` (release PRs); never worked on directly |
| `develop` | Integration | Target branch for all feature PRs |
| `feature/<feature-name>` | One feature / work item | Branched from `develop`, merged back via PR (Gates G2/G3) |

**Keeping branches up to date:** always `git rebase develop` + `git push --force-with-lease` — never merge commits into a branch.

## Isolation

Every subagent works in its own **git worktree + feature branch + PR** (targeting `develop`). No direct work on `main` or `develop`.

WIP limit: max. **3 open agent branches/PRs at a time** (merge conflict prevention). Calibrate during the pilot.

## Project Structure

| Path | Content |
|------|---------|
| `backend/` | .NET solution — API-first REST backend (consumed by Angular and later the iPhone app) |
| `frontend/` | Angular app |

## Commit Conventions

**Conventional Commits** (Angular style), enforced locally by the `commit-msg` hook (`.githooks/`, activate once per clone: `git config core.hooksPath .githooks`):

```
type(scope): subject          # imperative, lowercase, no trailing period
```

- **Types:** `feat` · `fix` · `docs` · `style` · `refactor` · `perf` · `test` · `build` · `ci` · `chore` · `revert`
- **Scopes (suggested):** `backend`, `frontend`, `docs`, `ci`, `harness`, `deps` — omit when the change is repo-wide
- **Body:** explains the *why*; wrap at ~72 chars

## Source of Truth

| Path | Content |
|------|---------|
| `docs/specs/` | Specs + acceptance criteria (basis for G1 and tests) |
| `docs/architecture/` | arc42 architecture doc (`ARCHITECTURE.md`), ADRs (basis for the architecture conformance gate) |
| `docs/guidelines/` | Coding and test guidelines (basis for agent work) |
| `docs/agent-logs/` | One run log per agent task (transparency / compliance, DoD requirement) |

Keep this file short. Detailed content belongs in `docs/guidelines/`.

## Permission Tiers

Configured in `.claude/settings.json` (add stack-specific commands during setup, see `docs/SETUP.md`):
- **Auto:** formatting, running single local tests, read/write files in the project folder
- **Approval:** package installs, `git push`, schema/migration changes, infrastructure changes

## Metadata
```
last_updated: 2026-07-09
owner: Marcel Steiner (AI Steward)
scope: store-it — digital pantry management
stack: .NET (C#) backend · Angular frontend · Kubernetes · GitHub Actions
```
