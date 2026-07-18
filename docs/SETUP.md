# Project Setup Checklist — store-it

> Template checklist, adapted for store-it (GitHub + GitHub Actions).
> Items marked **[platform]** live outside the repository — do not skip them.

---

## 1. Repository basics

- [x] Set project name and description (README).
- [x] Fill in the metadata block in `CLAUDE.md`.
- [x] Set owners in the guidelines.
- [x] Extend `.gitignore` (.NET + Node/Angular).

## 2. Stack (decided)

- [x] **Language / runtime:** .NET (C#) backend · Angular (TypeScript) frontend → arc42 section 2.
- [x] **Formatter / linter:** `dotnet format` (backend) · Prettier + ESLint (frontend).
- [x] **Test framework + coverage:** xUnit + coverlet (backend) · Angular default (frontend).
- [x] **Coverage threshold:** 70% (CI variable, calibrate during pilot).
- [x] **Architecture conformance:** .NET architecture tests (`Category=ArchitectureTests`), rules from ADR-001.
- [x] Stack commands added to the **Auto** permission tier in `.claude/settings.json`.

## 3. Pipeline (`.github/workflows/ci.yml`)

- [x] Jobs wired for .NET + Angular (go green once `backend/`/`frontend/` scaffolds land).
- [x] **[platform]** CI jobs are required status checks on `main` **and** `develop` (see §4, done 2026-07-14).
- [x] **[platform]** SonarCloud onboarding done 2026-07-16: organization `maststeiner`, monorepo projects `maststeiner_store-it-backend` + `maststeiner_store-it-frontend` (CI-based analysis), `SONAR_TOKEN` secret set. First fully green run same day (initial findings fixed).
- [x] **[platform]** Sonar quality gates added to required status checks on `main`/`develop` (done 2026-07-16 after PR #2 merge — 8 required checks total).
- [ ] Wire vitest lcov coverage into the frontend scan (TODO in ci.yml).
- [x] **Mutation testing** (retro 2026-07-18): Stryker.NET as CI job `1a` (break < 60%) — verifies tests kill mutants; AI-generated tests can look plausible while asserting nothing. Frontend (StrykerJS/vitest) deferred until real components exist (SPEC-001).
- [x] **Workflow lint** (retro 2026-07-18): actionlint 1.7.12 as CI job `5` — the pipeline itself is enforced infrastructure.
- [ ] **API contract gate** (ADR-006, retro 2026-07-18): lands with SPEC-001 — OpenAPI artifact `backend/openapi/v1.yaml`, drift check + oasdiff breaking check in CI.
- [ ] **[platform]** After first green runs: consider `1a · Backend mutation testing` and `5 · Workflow lint (actionlint)` as required checks (expected-check pattern: never before they ran on open PRs).
- [x] Trivy (security scan + SBOM) works as-is; SBOM per run as Actions artifact.
- [x] **License policy** (2026-07-16): project licensed under **MIT**. Dependencies: permissive licenses only (MIT, Apache-2.0, BSD, ISC); copyleft/special clauses (GPL, AGPL, LGPL, SSPL) are blocked — enforced twice: Trivy license scan (repo-wide) + dependency-review-action (PR diff).
- [ ] **[platform]** After first green run: consider adding `2 · Dependency & license review (PR diff)` to required status checks.
- [x] **[platform]** Dependabot alerts enabled (continuous CVE monitoring + email notification — closes the gap between PR scans; enabled 2026-07-13 via API).
- [ ] **[platform]** Optional: Dependabot security updates (automatic fix PRs) and/or a scheduled Trivy scan on develop/main.

## 4. Branch protection **[platform]**

Branching model: `main` (releases) ← `develop` (integration) ← `feature/<name>` — see `CLAUDE.md`.

> **Resolved 2026-07-14:** repo made public (decision Marcel) — branch protection and
> platform auto-merge available on GitHub Free.

- [x] Protect `main` **and** `develop`: changes only via PR + required CI status checks (all 6 gates), enforced incl. admins, no force pushes/deletions.
- [x] Required approvals: 0 (solo developer — GitHub forbids self-approval; Gate G2 review stays process discipline).
- [ ] **Later (decision 2026-07-14):** technically enforce Gate G2 — agent PRs via separate machine account/GitHub App, then `required_approving_review_count: 1` (Marcel approves as non-author). Raised by CodeRabbit review on PR #1.
- [ ] Enforce the **WIP limit** (max. 3 open agent PRs — start value, calibrate, then enforce technically).

## 4a. Renovate (dependency updates)

- [x] `renovate.json` on `main`: weekly, PRs target `develop` only, prConcurrentLimit 3 (aligned with WIP limit).
- [x] Automerge policy: minor/patch auto-merge after green gates (documented G3 exception); major → human review (label `major-update`).
- [x] **[platform]** Renovate GitHub App installed (2026-07-14) — dependency dashboard + update PRs against develop.
- [x] Platform auto-merge enabled (repo setting) — Renovate can use GitHub auto-merge.

## 5. AI review & tooling **[platform]**

- [x] **[platform]** CodeRabbit installed (2026-07-14, free for public repos) — `.coderabbit.yaml`: assertive profile, auto-review on PRs to develop/main, layering/test/i18n path instructions.
- [x] PR template is GitHub-native (`.github/pull_request_template.md`).

## 6. Claude Code hooks (optional, recommended)

- [x] Format-on-edit hook (retro 2026-07-18): `.claude/hooks/format-changed-file.sh` — file-scoped CSharpier/Prettier on every Edit/Write, silent, CI format gate stays the authority.
- [x] Commit conventions (2026-07-18): Conventional Commits enforced via `.githooks/commit-msg` (activated locally: `git config core.hooksPath .githooks`) — deliberate decision against a CI gate (low friction; hook is bypassable with --no-verify).
- [ ] Pre-commit hook that blocks commits when tests fail (deferred — evaluate friction first).

## 7. First sprint readiness

- [x] Architecture documented (arc42 sections 1–5 drafted; 5.2/6 refine with first features).
- [x] Layering rules as ADR-001, wired into the CI `architecture` job.
- [ ] First spec frozen by a human (Gate 1) — `SPEC-001` is in Draft.
- [x] WIP limit documented (`CLAUDE.md`: max. 3).
- [x] Agent run log convention: one log per agent task in `docs/agent-logs/`.

## Next steps (in order)

1. Freeze SPEC-001 (Gate 1) — human decision.
2. Scaffold `backend/` (.NET solution with Api/Application/Domain/Infrastructure + architecture tests) and `frontend/` (Angular workspace) so CI goes green.
3. Configure branch protection + CodeRabbit + SonarCloud (platform items above).
4. Implement SPEC-001 through the full KAIFe flow (worktree → branch → PR → gates).
