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
- **Test counts / coverage / mutation:** [CI run 29864654653](https://github.com/maststeiner/store-it/actions/runs/29864654653) — backend 94 tests (Domain 46 · Service 39 · Architecture 9) / line coverage 99.2%, frontend 20 tests / 86.7%, mutation score 77.5%, architecture debt 0
- **Defects caught before merge:** 2 by the isolated QA persona + 4 by automated review (see the PR #5 review history and `docs/agent-logs/`)

**Data point #2 — SPEC-003** (accounts & storage ownership; federated auth). Recorded retroactively on 2026-08-15:

- **Spec:** [`docs/specs/SPEC-003-accounts-and-storage-ownership.md`](specs/SPEC-003-accounts-and-storage-ownership.md) — frozen 2026-07-30
- **PR:** #76, merged 2026-08-08 → cycle time ≈ 9 days (freeze → merge)
- **Test counts / coverage / mutation:** [CI run 31251325271](https://github.com/maststeiner/store-it/actions/runs/31251325271) — backend 150 tests (Domain 62 · Service 79 · Architecture 9) / line coverage 95.0%, frontend 58 tests / 92.4%, mutation score 60.9%, architecture debt 0
- **Defects caught before merge:** 1 must-fix from the whole-branch review (HTTPS metadata gating) and 1 runtime defect found in local verification — with no OIDC secrets configured the handler threw on every request and `/health` answered 500; fixed in-branch with the regression test `NoOidcConfigTests`. Automated review contributed 56 inline comments, human review 28 (comment counts, not defect-classified). See [`docs/agent-logs/2026-08-05-spec-003-implementation.md`](agent-logs/2026-08-05-spec-003-implementation.md).

**Data point #3 — SPEC-004** (env-driven config + container stack). Recorded retroactively on 2026-08-15:

- **Spec:** [`docs/specs/SPEC-004-env-config-and-container-stack.md`](specs/SPEC-004-env-config-and-container-stack.md) — frozen 2026-08-09
- **PR:** #83, merged 2026-08-15 → cycle time ≈ 6 days (freeze → merge)
- **Test counts / coverage / mutation:** [CI run 31876478530](https://github.com/maststeiner/store-it/actions/runs/31876478530) — backend 153 tests (Domain 62 · Service 82 · Architecture 9) / line coverage 94.9%, frontend 76 tests / 92.6%, mutation score 60.9%, architecture debt 0
- **Defects caught before merge:** 24 review findings in total — 12 on the spec before implementation (10 valid → amendments A1–A5, 1 false positive rebutted, 1 partially addressed) and 12 on the implementation across five review rounds, all fixed. Exactly 1 was a behaviour defect: `.env.example` shipped `POSTGRES_PASSWORD=storeit`, so the spec's "absent must fail loudly" guard could never fire. The other 10 were documentation that contradicted correct behaviour. Automated review contributed 22 inline comments, human review 10. See [`docs/agent-logs/2026-08-09-container-stack.md`](agent-logs/2026-08-09-container-stack.md).

### Trend

| | SPEC-001 (#5) | SPEC-003 (#76) | SPEC-004 (#83) |
|---|---|---|---|
| Cycle time, freeze → merge | 8 d | 9 d | 6 d |
| Backend tests | 94 | 150 | 153 |
| Backend line coverage | 99.2% | 95.0% | 94.9% |
| Frontend tests | 20 | 58 | 76 |
| Frontend line coverage | 86.7% | 92.4% | 92.6% |
| Mutation score (gate ≥ 60%) | 77.5% | 60.9% | 60.9% |
| Architecture debt | 0 | 0 | 0 |

Two readings the table invites, stated so they are not read into it by accident. **Backend coverage
falls while the codebase grows** — SPEC-003 added authentication and infrastructure code whose
branches (`StoreIt.Infrastructure` at 16.7% branch coverage) are thin, and that has not been paid
back since. **The mutation score sits on the gate**, at 60.9% against a break-below of 60%: it fell
from 77.5% when the auth surface arrived and has stayed there. Neither is a target that was hit;
both are margins that have narrowed.

Numbers above are a human-readable snapshot; the CI run and PR are the authoritative source. Record each further feature the same way (spec link, PR link, CI-run numbers) to build the trend vs. classically developed items.

**How the figures are read from a run.** Backend line coverage is the total of the
`StoreIt.Api.Service.Tests` coverlet report, which spans all four modules; the `StoreIt.Domain.Tests`
run prints a second, Domain-only total (97.9% for SPEC-001) that is easy to mistake for it. Frontend
coverage is the `All files` line-percent column of the vitest coverage table. The mutation score is
Stryker's `The final mutation score is …` line in job 1a. Architecture debt is 0 whenever job 4 is
green.
