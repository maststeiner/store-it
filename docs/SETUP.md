# Project Setup Checklist

> Work through this checklist when starting a new project from this template.
> Items marked **[platform]** live outside the repository and are easy to forget — do not skip them.

---

## 1. Repository basics

- [ ] Set project name and description (README, repository settings).
- [ ] Fill in the metadata block in `CLAUDE.md`: `last_updated`, `owner` (AI Steward), `scope`, `stack`.
- [ ] Set owners in `docs/guidelines/coding-guidelines.md` and `docs/guidelines/test-guidelines.md`.
- [ ] Extend `.gitignore` with your stack's build artifacts.

## 2. Choose your stack

The template is technology-independent. Decide and document (as ADRs where non-trivial):

- [ ] **Language / runtime** → record in `docs/architecture/ARCHITECTURE.md` section 2 (constraints).
- [ ] **Formatter / linter** → wire into the pipeline `Format` stage and the developer persona workflow.
- [ ] **Unit test framework + coverage tool** → wire into the pipeline `BuildAndTest` stage.
- [ ] **Coverage threshold** → set the pipeline variable (default 70%, calibrate during pilot).
- [ ] **Architecture conformance tooling** (dependency/layering rules as code) → wire into the pipeline `Architecture` stage.
- [ ] Add stack-specific commands to the **Auto** permission tier in `.claude/settings.json` (e.g. format command, single test runs).

## 3. Pipeline (`azure-pipelines.yml`)

- [ ] Replace all `TODO` placeholder steps with your stack's build, test, coverage, format, and architecture-test commands.
- [ ] **[platform]** Bind the pipeline to PRs as a build validation policy.
- [ ] **[platform]** Configure the SonarQube (or equivalent) service connection; set project key/name.
- [ ] Trivy (security scan + SBOM) is language-neutral and works as-is.

## 4. Branch policies **[platform]**

- [ ] Protect `main`: changes only via PR.
- [ ] Require **≥ 1 human reviewer** (Gate G2/G3 — this is non-negotiable).
- [ ] Require build validation (pipeline green = Gate G3 machine part).
- [ ] Enforce the **WIP limit**: max. 3 open agent branches/PRs at a time (start value — calibrate with the team, then enforce technically via policy or hook).

## 5. AI review & tooling **[platform]**

- [ ] Activate an automated AI review tool (e.g. CodeRabbit) as the first filter before human review (Gate G2).
- [ ] Copy `.github/pull_request_template.md` to your platform's location if not on GitHub (Azure DevOps: `.azuredevops/pull_request_template.md`).

## 6. Claude Code hooks (optional, recommended)

Hooks automate harness rules so they don't depend on agent discipline. Example — run the formatter after every file edit (add to `.claude/settings.json`):

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [{ "type": "command", "command": "<your-format-command>" }]
      }
    ]
  }
}
```

- [ ] Add a format-on-edit hook once the formatter is chosen.
- [ ] Consider a pre-commit hook that blocks commits when tests fail.

## 7. First sprint readiness

- [ ] Architecture documented in `docs/architecture/ARCHITECTURE.md` (at least sections 1–5).
- [ ] Layering rules formulated as ADR and enforced in the `Architecture` pipeline stage.
- [ ] First spec written from `docs/specs/SPEC-TEMPLATE.md` and frozen by a human (Gate 1).
- [ ] Team agreement on WIP limit documented.
- [ ] Agent run log convention understood: one log per agent task in `docs/agent-logs/` (see template).
