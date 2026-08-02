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
- [x] **Coverage threshold:** 70%, enforced in the gate configs (frontend `vitest-base.config.ts` · backend coverlet) and fixed after the SPEC-001 pilot (2026-07-27).
- [x] **Architecture conformance:** .NET architecture tests (`Category=ArchitectureTests`), rules from ADR-001.
- [x] Stack commands added to the **Auto** permission tier in `.claude/settings.json`.

## 3. Pipeline (`.github/workflows/ci.yml`)

- [x] Jobs wired for .NET + Angular (go green once `backend/`/`frontend/` scaffolds land).
- [x] **[platform]** CI jobs are required status checks on `main` **and** `develop` (see §4, done 2026-07-14).
- [x] **[platform]** SonarCloud onboarding done 2026-07-16: organization `maststeiner`, monorepo projects `maststeiner_store-it-backend` + `maststeiner_store-it-frontend` (CI-based analysis), `SONAR_TOKEN` secret set. First fully green run same day (initial findings fixed).
- [x] **[platform]** Sonar quality gates added to required status checks on `main`/`develop` (done 2026-07-16 after PR #2 merge; the required-check set has since grown as jobs matured — see #15).
- [x] **[platform]** Conversation resolution required before merging (2026-07-18) — unresolved review threads technically block the merge.
- [x] Wire vitest lcov coverage into the frontend scan (done: `vitest-base.config.ts` emits `lcov`, `ci.yml` passes `sonar.javascript.lcov.reportPaths=coverage/lcov.info`).
- [x] **[platform]** Sonar branch model (fixed 2026-07-27): this org's SonarCloud plan persists analysis for **only the main branch + PRs** — long-lived side branches (e.g. `develop`) are rejected (`Organization is not allowed to access data from non main branches`). Because all integration happens on `develop`, the **Sonar main branch is set to `develop`** for both projects (Project → Administration → Branches and Pull Requests → rename main to `develop`). The `git main` branch is therefore *not* Sonar-analyzed; the `3 · *quality gate` jobs skip `push`-to-`main` via `if:` (PRs + `develop` pushes only). Symptom before the fix: frontend coverage read 0% because Sonar's `main` was frozen at the pre-code scaffold (16.07.), 47 commits behind `develop`.
- [x] **Mutation testing** (retro 2026-07-18): Stryker.NET as CI job `1a` (break < 60%) — verifies tests kill mutants; AI-generated tests can look plausible while asserting nothing. Frontend (StrykerJS/vitest) **consciously dropped** (decision 2026-07-20, see `docs/guidelines/test-guidelines.md`): the frontend is logic-thin (template + delegation, server-computed status per ADR-002) while the branch-heavy logic lives in the backend domain, already gated by Stryker.NET. Revisit only if substantial client-side logic appears.
- [x] **Workflow lint** (retro 2026-07-18): actionlint 1.7.12 as CI job `5` — the pipeline itself is enforced infrastructure.
- [x] **API contract gate** (ADR-006, wired 2026-07-19 with SPEC-001): OpenAPI artifact `backend/openapi/StoreIt.Api.json` (build-time generated), CI job `2 · API contract gate` — drift check + oasdiff v1.23.0 breaking check against **the last released SemVer tag** (ADR-007 — while no `v*` tag exists, `/api/v1` is pre-release and breaking changes are allowed). Now a required check on `main` and `develop` (#15).
- [x] **[platform]** Protect release tags: a GitHub **ruleset for `v*` tags** (ADR-007, done 2026-08-02) — `deletion` + `non_fast_forward` rules, enforcement active, so the breaking-change baseline can't be moved or deleted (to re-tag intentionally, disable the ruleset briefly).
- [x] **[platform]** `1a · Backend mutation testing`, `1b · End-to-end (full stack)`, `2 · API contract gate` and `5 · Workflow lint (actionlint)` added to required status checks on `main` **and** `develop` (2026-08-02, #15) — all green across many PRs, so the expected-check pattern is satisfied.
- [x] Trivy (security scan + SBOM) works as-is; SBOM per run as Actions artifact.
- [x] **License policy** (2026-07-16): project licensed under **MIT**. Dependencies: permissive licenses only (MIT, Apache-2.0, BSD, ISC); copyleft/special clauses (GPL, AGPL, LGPL, SSPL) are blocked — enforced twice: Trivy license scan (repo-wide) + dependency-review-action (PR diff).
- [x] **[platform]** `2 · Dependency & license review (PR diff)` added to required status checks on `main` **and** `develop` (2026-08-02, #15).
- [x] **[platform]** Dependabot alerts enabled (continuous CVE monitoring + email notification — closes the gap between PR scans; enabled 2026-07-13 via API).
- [ ] **[platform]** Optional: Dependabot security updates (automatic fix PRs) and/or a scheduled Trivy scan on develop/main.

## 4. Branch protection **[platform]**

Branching model: `main` (releases) ← `develop` (integration) ← `feature/<name>` — see `CLAUDE.md`.

> **Resolved 2026-07-14:** repo made public (decision Marcel) — branch protection and
> platform auto-merge available on GitHub Free.

- [x] Protect `main` **and** `develop`: changes only via PR + required CI status checks (all CI gates), enforced incl. admins, no force pushes/deletions.
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
- [x] First spec frozen by a human (Gate 1) — `SPEC-001` frozen 2026-07-13 (Gate G1), with formal amendments recorded in the spec.
- [x] WIP limit documented (`CLAUDE.md`: max. 3).
- [x] Agent run log convention: one log per agent task in `docs/agent-logs/`.

## Next steps (in order)

Initial setup is **complete**: SPEC-001 was frozen (Gate G1, 2026-07-13), the `backend/` and `frontend/` scaffolds landed and CI is green, branch protection + CodeRabbit + SonarCloud are configured (platform items above), and SPEC-001 shipped through the full KAIFe flow. Ongoing work — further specs, tech-debt, and the platform-hardening items still unchecked above — is tracked as GitHub issues.
