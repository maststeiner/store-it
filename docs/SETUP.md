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
- [ ] **[platform]** Mark CI jobs as required status checks on `main` (build validation).
- [ ] **[platform]** SonarCloud: create project, add `SONAR_TOKEN` secret, uncomment the `quality` job.
- [x] Trivy (security scan + SBOM) works as-is; SBOM per run as Actions artifact.
- [x] **[platform]** Dependabot alerts enabled (continuous CVE monitoring + email notification — closes the gap between PR scans; enabled 2026-07-13 via API).
- [ ] **[platform]** Optional: Dependabot security updates (automatic fix PRs) and/or a scheduled Trivy scan on develop/main.

## 4. Branch protection **[platform]**

Branching model: `main` (releases) ← `develop` (integration) ← `feature/<name>` — see `CLAUDE.md`.

- [ ] Protect `main` **and** `develop`: changes only via PR.
- [ ] Require **≥ 1 human review** (Gate G2/G3 — non-negotiable).
- [ ] Require CI status checks (Gate G3 machine part).
- [ ] Enforce the **WIP limit** (max. 3 open agent PRs — start value, calibrate, then enforce technically).

## 5. AI review & tooling **[platform]**

- [ ] Activate CodeRabbit (or equivalent) on the GitHub repo as first filter before human review (Gate G2).
- [x] PR template is GitHub-native (`.github/pull_request_template.md`).

## 6. Claude Code hooks (optional, recommended)

- [ ] Format-on-edit hook (weigh cost: `dotnet format` on every edit is slow — consider pre-commit instead).
- [ ] Pre-commit hook that blocks commits when tests fail.

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
