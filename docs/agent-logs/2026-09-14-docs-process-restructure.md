# Agent Run Log: Factor the process out of CLAUDE.md into a reusable docs/process subtree

> **Date:** 2026-09-14 (work started 2026-08-30)
> **Spec:** none — harness/tech-debt issue #138; the issue body is the frozen G1 input (convention, now written into PROCESS.md G1)
> **Persona(s):** architect (structure of the subtree, interface contract), developer (file moves, dedupe, CI job), reviewer (consistency checks)
> **Model:** Claude Fable 5 (extraction, 2026-08-30) · Claude Fable 5.1 (review round and finish, 2026-09-14)
> **Branch / PR:** `feature/docs-process-restructure` → PR (linked from #138)

---

## Task

Make the development process reusable: move everything project-independent out of
`CLAUDE.md` and `.claude/agents/` into `docs/process/`, give stack facts and the
Definition of Done a single home, slim `CLAUDE.md` to repo-specific rules, and index `docs/`.

## Plan

1. Charter for `docs/process/` (zero project content, one-way references, rules vs. values, copy-based reuse).
2. `docs/project/tech-stack.md` as the single source for stack facts.
3. `PROJECT-INTERFACE.md` — the conventional paths the process may reference.
4. Personas: canonical files in `docs/process/personas/` with subagent frontmatter; `.claude/agents/` as byte-identical copies.
5. `PROCESS.md` (versioned), explicit `DEFINITION-OF-DONE.md`, `CHANGELOG.md`.
6. Move spec/agent-log/ADR templates into the subtree; add a generic PR-template master.
7. Slim `CLAUDE.md`; dedupe peripheral references; add `docs/README.md`.
8. Review round with the orchestrator before the PR; rebase, push, PR.

## Key Decisions

- **Copy, not reference, as the reuse model.** Each adopting project owns its copy of
  `docs/process/` and records the origin version in `CLAUDE.md`; drift is diffed against
  `CHANGELOG.md`. A shared submodule would couple release cadences of unrelated projects.
- **Rules live in the process, values in the project.** The coverage threshold left the qa
  persona (owned by `docs/guidelines/test-guidelines.md`); the WIP limit is a recommendation
  in `PROCESS.md` and a project value in `CLAUDE.md`.
- **Historical documents are not rewritten.** Frozen specs, accepted ADRs, and older agent
  logs keep their original wording (including the old framework name) — immutability is a
  process rule, not a cleanup backlog.
- **Persona identity is machine-checked.** The manual "edit in `personas/`, re-copy" rule
  gets a CI job (`diff -r docs/process/personas .claude/agents`) declared as part of the G3
  machine part, so drift fails the pipeline instead of surfacing in a review.
- **No spec, issue #138 as G1 input.** Created at the finish so the issue body records the
  target state and the review inputs; the convention itself is now written into G1.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Asked to review `PROCESS.md` before a PR is opened | Process content is the orchestrator's decision, not the agent's |
| 2 | Human functional testing added to Gate G3 (process, DoD, both PR templates) | Automated checks prove tests, not usefulness — a human confirms the software does what the user needs |
| 3 | Internal framework name removed from all living documents; process renamed **AI-Dev Process** | A company-internal name contradicts the subtree's zero-project-content charter |
| 4 | Workflow / lifecycle section added to `PROCESS.md` | Gates and personas existed, but the sequence between them was implicit |
| 5 | WIP limit made an explicit recommendation the project overrides or replaces; branching marked as a reference model | Keep the process adaptable to projects with a different flow |
| 6 | Persona byte-identity check moved from "follow-up" into this change (CI job) | Cheap now, and the rule is only credible if enforced |
| 7 | Automated review (CodeRabbit) raised 7 minor findings, all taken | Product name in the reusable reviewer persona; stack facts still duplicated in README and ARCHITECTURE constraints; stale Harness glossary entry; `mkdir -p` missing in the install step; layer wording mismatch in the docs index; `Gate 1` vs `G1` in the spec template |

## Outcome

- **Result:** 15 commits on `feature/docs-process-restructure`, rebased on `develop`, PR
  opened against `develop`. Local checks: no stale template paths repo-wide, `diff -r`
  between personas and agents empty, no project facts in `docs/process/`, `ci.yml` parses.
  `actionlint` and the new CI job run in the pipeline.
- **Deviations from spec:** none (no spec — issue #138 is the G1 input; its target state
  was extended by the review inputs above before the PR).
- **Harness follow-up:** the process itself is the harness change (version 1.0). Left open:
  a second project adopting the subtree would be the first real test of the interface
  contract.
