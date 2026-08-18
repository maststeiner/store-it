# Agent Run Log: `main` was still the scaffold — sync it with `develop`

> **Date:** 2026-08-18
> **Spec:** none — repository/release housekeeping, no product behaviour. Tech-debt class of change.
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `chore/sync-main-with-develop` (this log) + the release PR `develop` → `main`
> **Follows from:** `2026-08-18-renovate-config-source-of-truth.md`

---

## Task

The Renovate investigation exposed that `main` had not moved since 2026-07-13 and is 210
commits behind `develop`. Bring it up to date. Explicit instruction: *"ja, main aufräumen"*.

## Why this is not cosmetic

`main` is protected with **13 required status checks** and `enforce_admins: true`, but the
`ci.yml` on `main` is the scaffold-era one and defines only **6** of them:

| present on `main` | missing on `main` |
|---|---|
| 1 · Backend build & test, 1 · Frontend build & test, 2 · Security scan & SBOM, 4 · Architecture gate, 5 · Backend format check, 5 · Frontend format check | 3 · Backend quality gate, 3 · Frontend quality gate, 2 · Dependency & license review, 1a · Backend mutation testing, 5 · Workflow lint, 1b · End-to-end, 2 · API contract gate |

For a `pull_request` event GitHub runs the workflow from the **head** branch, so any branch
cut from `main` can only ever report those 6 — the other 7 stay "Expected — waiting for
status" forever. **`main` was therefore unfixable by any PR based on `main`.** A release
merge is the only way in, because its head is `develop`, which carries the complete `ci.yml`.
That is also why the Renovate fix had to go through the default-branch switch rather than a
two-line PR to `main`.

## Findings

| Observation | Evidence |
|---|---|
| `main` is 210 commits behind `develop`, 0 ahead — nothing on `main` would be lost | `GET /repos/.../compare/main...develop` → `behind_by: 0` |
| **Zero tags, zero releases** in the repository | `GET /repos/.../tags`, `/releases` |
| `develop` is protected, so `delete_branch_on_merge: true` cannot remove it on merge | `GET /repos/.../branches/develop/protection` |
| Merge commits, squash and rebase are all enabled on the repo | `GET /repos/...` merge settings |
| The contract gate treats **any** `v[0-9]+.[0-9]+.[0-9]+` tag reachable from `main` as the baseline | `.github/workflows/ci.yml`, "Breaking-change check (vs. last released tag)" |

## Decision: sync `main`, deliberately **without** a tag

ADR-007 defines a release as a `develop` → `main` merge *marked by an annotated SemVer tag*,
and reserves `v1.0.0` as the deliberate act that freezes the released `/api/v1` contract.
Reading the CI script rather than the ADR prose shows the freeze is not tied to `v1.0.0` at
all: the baseline selector matches **any** strict `vMAJOR.MINOR.PATCH` tag, so even a
`v0.1.0` would switch the breaking check from "skipped, pre-release" to
`oasdiff breaking … --fail-on ERR` against that contract.

So tagging this merge — with any version — would end the pre-release freedom that ADR-007
point 2 grants on purpose. That is a product decision, not housekeeping, and it belongs to a
human. This merge is therefore an **untagged sync**: `main` gets the current code, workflows
and Renovate config, the repository keeps 0 tags, and `/api/v1` stays unfrozen exactly as
ADR-007 intends.

Worth naming as a deviation: under ADR-007's wording `main` only ever advances via a tagged
release, so an untagged sync is not a case the ADR describes. It does not contradict any of
its decisions — but if the intent is that `main` must never move untagged, then the ADR needs
an amendment and this merge should become a tagged `v0.1.0` release, with the gate
consequence above accepted knowingly.

## Merge instruction (matters, and is easy to get wrong)

**Merge commit — not squash.** Squashing would put a commit on `main` that does not exist in
`develop`'s history; `main` and `develop` would diverge permanently and every future release
merge would start with an artificial conflict. Rebase-merge has the same effect. Since `main`
is 0 commits ahead, a merge commit is trivially clean.

## Verification

| Check | Result |
|-------|--------|
| Nothing on `main` is lost | `behind_by: 0` in the compare above |
| The release PR can actually go green | its 13 checks run from `develop`'s `ci.yml` — unlike anything based on `main` |
| No tag is created by this change | repository stays at 0 tags; the contract gate keeps logging "No release tag yet — skipping" |
| `develop` survives the merge | branch protection blocks `delete_branch_on_merge` |

## Open verification

- After the merge, `main` carries the current `renovate.json`. That copy is **inert** (Renovate
  reads the default branch, now `develop`) — it exists so the two branches stop disagreeing,
  not because it is read.
- The first tagged release remains an open decision. Cutting `v1.0.0` — or any `v*` tag —
  activates the breaking-change gate from that commit onward.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | *"ja, main aufräumen"* | Trigger for this run |
| 2 | Tagging deferred to the human rather than decided by the agent | Any `v*` tag freezes `/api/v1` per the CI's baseline selector; ADR-007 makes that a deliberate act |

## Outcome

- **Result:** ready for review
- **Deviations from spec:** n/a (no spec). Deviation from **ADR-007** stated above: an untagged
  `develop` → `main` merge is not a case the ADR describes.
- **Harness follow-up:** the required-status-check list is repository-wide, but the workflow that
  reports those checks is per-branch. When a branch falls behind far enough that its CI no longer
  defines the required jobs, that branch becomes unmergeable-into without a release. Worth a
  periodic check that `main`'s `ci.yml` still defines every required context.
