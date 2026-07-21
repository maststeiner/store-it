# Metrics — store-it (KAIFe §8)

> **Purpose:** the pilot must show a **Flow *and* Quality** gain over the classic
> baseline (KAIFe §10 L4 exit criterion). Velocity/story points are **not** the
> lead metric — with AI, effort decouples from complexity. Lightweight by design:
> most data already exists (CI, SonarCloud, GitHub); this file says what to watch
> and where to read it. Reviewed in the retro / harness-review session (§5).

## Flow (lead) — the real bottleneck is review/verification capacity

| Metric | How to read it |
|--------|----------------|
| **Cycle time** spec-freeze → merge | spec `Frozen` date → PR `mergedAt` (`gh pr view <n> --json mergedAt`) |
| **Throughput** | merged feature PRs per sprint (`gh pr list --state merged --base develop`) |
| **WIP** | open feature PRs at a time (target ≤ 3, CLAUDE.md) |
| **Review load** (watch!) | review rounds per PR + commits-after-first-review; how much CodeRabbit/human review triggered rework |

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

- **SPEC-001** (first full KAIFe feature) is data point #1: cycle time spec-freeze
  2026-07-13 → merge 2026-07-21; 94 backend + 20 frontend + 3 E2E tests green;
  backend line coverage ~98%, frontend ~87%; mutation gate ≥ 60%; architecture
  debt 0; 2 QA-found + 4 review-found defects fixed before merge.
- Record each further feature the same way to build the trend vs. classically
  developed items.
