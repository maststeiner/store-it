# KAIFe L4 — Development Process

> **Process version:** 1.0 (see [`CHANGELOG.md`](CHANGELOG.md))
> **Framework:** KAIFe — KMS Agile Intelligence Framework, Level 4
> **Scope:** project-independent. Project-specific facts live at the paths declared in [`PROJECT-INTERFACE.md`](PROJECT-INTERFACE.md).

The human is the orchestrator, AI is the multiplier. Agents produce; humans direct,
gate, and stay accountable.

---

## The Three Gates (non-negotiable)

- **G1 · Spec Freeze:** No agent starts implementation without a human-frozen spec (`docs/specs/`, from [`templates/SPEC-TEMPLATE.md`](templates/SPEC-TEMPLATE.md)). A frozen spec body is never edited — post-freeze changes are recorded as numbered amendments. For tech-debt and harness work without a spec, the issue body is the frozen G1 input.
- **G2 · Review:** No PR is merged without an automated AI review pass **plus** human code review. The human attestation in the PR checklist is ticked by a person, never by AI.
- **G3 · DoD/Merge:** The [Definition of Done](DEFINITION-OF-DONE.md) is met, the CI pipeline is fully green, and only a human merges.

## Personas

Five personas carry the work. Canonical definitions live in [`personas/`](personas/);
the project installs byte-identical copies in `.claude/agents/` so they are dispatchable
as subagents. Always activate the appropriate persona:

| Persona | Task |
|---------|------|
| [`analyst`](personas/analyst.md) | Requirements → testable user stories (EARS notation) |
| [`architect`](personas/architect.md) | Design structure, enforce layering boundaries, write ADRs |
| [`developer`](personas/developer.md) | Implement *within* the architecture constraints |
| [`qa`](personas/qa.md) | Derive tests from acceptance criteria (never from code) |
| [`reviewer`](personas/reviewer.md) | Adversarial review: security, duplicates, tech debt |

Each persona has **hard limits that are never crossed** — not even when explicitly asked.

## Orchestrator Loop

For every task: **Run → Inspect → Challenge → Refine → Re-run**

When output systematically deviates from the goal: don't only fix the output — sharpen
the harness (the project's guidelines, or the persona definitions in this subtree). The
fix then applies to all future runs. Harness changes to personas are made in
[`personas/`](personas/) and re-copied to `.claude/agents/`; each substantive process
change gets a [`CHANGELOG.md`](CHANGELOG.md) entry.

## Branching Model

| Branch | Purpose | Rules |
|--------|---------|-------|
| `main` | Releases only | Only receives merges from the integration branch (release PRs); never worked on directly |
| `develop` | Integration | Target branch for all feature PRs |
| `feature/<name>` | One feature / work item | Branched from `develop`, merged back via PR (Gates G2/G3) |

**Keeping branches up to date:** always `git rebase develop` + `git push --force-with-lease` — never merge commits into a branch.

## Isolation & WIP

Every subagent works in its own **git worktree + feature branch + PR** (targeting
`develop`). No direct work on `main` or `develop`.

The project sets a **WIP limit** on open agent branches/PRs (merge-conflict prevention
and review-capacity protection; recommended start: 3, calibrate). The calibrated value
lives in the project's `CLAUDE.md`.

## Artifacts

| Artifact | Template | Rule |
|----------|----------|------|
| Spec | [`templates/SPEC-TEMPLATE.md`](templates/SPEC-TEMPLATE.md) | Acceptance criteria in EARS notation; freeze = G1; amendments instead of edits |
| Agent run log | [`templates/AGENT-LOG-TEMPLATE.md`](templates/AGENT-LOG-TEMPLATE.md) | One log per agent task in `docs/agent-logs/` — the accountability record |
| ADR | [`templates/ADR-TEMPLATE.md`](templates/ADR-TEMPLATE.md) | One decision per ADR; an agent drafts as Proposed, only a human accepts; immutable once accepted |
| Pull request | [`templates/pull-request-template.md`](templates/pull-request-template.md) | Instantiated as `.github/pull_request_template.md`, extended with project-specific checks |

## Commit Conventions

Conventional Commits are mandatory. Types, scopes, and enforcement are defined in the
project's `docs/guidelines/coding-guidelines.md` (project interface).

## Data & Compliance (KAIFe §7)

- **No real or sensitive data in prompts, fixtures, test data, or logs** — synthetic
  data only.
- **Agent run logs are the accountability record** — how AI produced each change
  (KAIFe Principle 3); supports EU-AI-Act audit readiness. (Article 50 governs
  user-facing disclosure of AI-generated content and does not itself mandate internal
  logs; mandatory logging applies to high-risk systems under Article 12.)
- Secrets come from the environment (12-factor), never committed.

## Permission Tiers (concept)

Agent tool permissions are split into two tiers; the concrete allowlist is configured
in the project's `.claude/settings.json`:

- **Auto:** formatting, running single local tests, read/write files in the project folder
- **Approval:** package installs, `git push`, schema/migration changes, infrastructure changes

## Metrics (KAIFe §8, concept)

The pilot question is always: does the process deliver a **flow *and* quality** gain?
Velocity/story points are not the lead metric — with AI, effort decouples from
complexity. Track lightweight, from data the gates already produce:

- **Flow:** cycle time (spec freeze → merge), throughput, WIP, and above all **review
  load** — the human review/verification capacity is the real bottleneck.
- **Quality:** coverage, test effectiveness (e.g. mutation score), duplication,
  structural/architecture debt, vulnerabilities, open tech debt.
- **Cost:** token/run cost per increment (noted in the agent run log).

The project instantiates this as `docs/metrics.md` (project interface), naming where
each figure is read from.

## Installing This Process

See the reuse steps in [`README.md`](README.md) and the required project files in
[`PROJECT-INTERFACE.md`](PROJECT-INTERFACE.md).
