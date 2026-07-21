# Metrics — store-it (KAIFe §8)

> **Purpose:** the pilot must show a **Flow *and* Quality** gain over the classic
> baseline (KAIFe §10 L4 exit criterion). Velocity/story points are **not** the
> lead metric — with AI, effort decouples from complexity. Lightweight by design:
> most data already exists (CI, SonarCloud, GitHub); this file says what to watch
> and where to read it. Reviewed in the retro / harness-review session (§5).

## Flow (lead) — the real bottleneck is review/verification capacity

| Metric | How to read it |
|--------|----------------|
| **Cycle time** spec-freeze → merge | freeze date from the spec's Gate-Status table → PR `mergedAt` (`gh pr view <n> --json mergedAt`) |
| **Throughput** | merged feature PRs per sprint (`gh pr list --state merged --base develop --json number,mergedAt`) |
| **WIP** | open feature PRs at a time (target ≤ 3, CLAUDE.md) |
| **Review load** (watch!) | per PR: review rounds and commits after the first review — count review-fix commits (`gh pr view <n> --json commits`) and resolved review threads (GraphQL `reviewThreads`) |

## Quality — already produced by the gates, just collect it

| Metric | Source |
|--------|--------|
| Line coverage (target ≥ 70%) | backend coverlet · frontend vitest (CI job 1 / 3) |
| **Mutation score** | Stryker.NET (CI job 1a) |
| Duplication, code smells, new-code coverage | SonarCloud (backend + frontend projects) |
| Vulnerabilities / secrets / license issues | Trivy + dependency-review (CI job 2) |
| **Structural (architecture) debt** — target 0 | NetArchTest + ProjectsRuler (CI job 4) |
| Dependency inventory | SBOM artifact (CycloneDX, per run) |
| Open tech debt | GitHub issues labelled `tech-debt` |

## Cost

| Metric | Source |
|--------|--------|
| Token / run cost per increment | Claude Code usage (note per feature in the agent run log) |

## Culture

Developer satisfaction / retention (§9) — **not applicable to the solo pilot**;
capture once a team works in KAIFe.

## Baseline

**Data point #1 — SPEC-001** (first full KAIFe feature). Provenance for reproducibility:

- **Spec:** [`docs/specs/SPEC-001-manage-storage-items.md`](specs/SPEC-001-manage-storage-items.md) — frozen 2026-07-13 (Gate-Status table)
- **PR:** #5, merged 2026-07-21 → cycle time ≈ 8 days (freeze → merge)
- **Test counts / coverage / mutation:** read from the green CI run on PR #5's merge commit (jobs 1, 1a, 3) — backend 94 tests / line coverage ~98%, frontend 20 tests / ~87%, mutation gate ≥ 60%, architecture debt 0
- **Defects caught before merge:** 2 by the isolated QA persona + 4 by automated review (see the PR #5 review history and `docs/agent-logs/`)

Numbers above are a human-readable snapshot; the CI run and PR are the authoritative source. Record each further feature the same way (spec link, PR link, CI-run numbers) to build the trend vs. classically developed items.
