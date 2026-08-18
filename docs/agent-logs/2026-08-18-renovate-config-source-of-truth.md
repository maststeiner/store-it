# Agent Run Log: Renovate ran a config from 2026-07-13 — the default branch is the source of truth

> **Date:** 2026-08-18
> **Spec:** none — repository/platform configuration, no product behaviour. Tech-debt class
> of change; the orchestrator's observation ("Renovate erstellt die PRs nicht automatisch,
> ich muss es auf der Homepage manuell machen") is the frozen input.
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `ci/renovate-config-source-of-truth`
> **Supersedes the open verification in:** `2026-08-06-renovate-pending-approvals.md`

---

## Task

Second look at the same symptom as 2026-08-06: Renovate opens no PRs by itself. That run
diagnosed the schedule, made the dashboard and approval keys explicit, and left two open
verifications. Twelve days later neither had come true — so the question was no longer
"what does the config say" but "which config is actually running".

## Findings (verified via the GitHub API, not assumed)

| Observation | Evidence |
|---|---|
| Repository **default branch was `main`** | `GET /repos/maststeiner/store-it` → `default_branch: main` |
| `main`'s **last commit is 2026-07-13**, message *"Add Renovate config: weekly updates on develop"* | `GET /repos/.../commits/main` |
| `develop` is **210 commits ahead** of `main`, 0 behind | `GET /repos/.../compare/main...develop` |
| `renovate.json` on `main` is the **original July config**: `schedule:weekly`, no `timezone`, no `dependencyDashboard*`, no `allowedVersions` holds | `GET /repos/.../contents/renovate.json?ref=main` |
| `useBaseBranchConfig` is set **nowhere** in the repo (default `none`) | grep across the tree |
| Still **no Dependency Dashboard issue** — no bot-authored issue at all | `GET /repos/.../issues?state=all&per_page=100` |
| **No `renovate/*` branch**, no open Renovate PR; last bot PR is #61 (2026-08-02) | `GET /repos/.../branches`, `GET /repos/.../pulls?state=open` |
| PR #77 (the 2026-08-06 fix) merged **into `develop`** on 2026-08-07 — never into `main` | `GET /repos/.../pulls/77` → `merged: true`, `base.ref: develop` |
| A Monday window did pass (2026-08-17, `schedule:weekly` = `* 0-3 * * 1` UTC) and produced nothing | weekday check + branch/PR state above |

## Diagnosis

**Renovate reads `renovate.json` from the repository's default branch, and only from there.**
`baseBranches: ["develop"]` steers where update PRs *target*; it does not move where the
config is *read from*. Reading config from a base branch is a separate opt-in,
`useBaseBranchConfig: "merge"`, which was never set.

So the config Renovate had been executing since 2026-07-13 was the one on `main`:
weekly, UTC, no dashboard keys, no holds. Everything merged to `develop` afterwards was
inert — **both** the 2026-08-06 fix (PR #77: nightly window, `timezone`,
`dependencyDashboard: true`, `dependencyDashboardApproval: false`) and the framework holds
(PR #70: `allowedVersions` for Microsoft.OpenApi and TypeScript).

That accounts for every part of the symptom, including the two open verifications from
2026-08-06:

- **The approval gate stayed active.** `dependencyDashboardApproval: false` never reached
  Renovate, so whatever the Mend app side sets remained in force — hence updates sitting as
  "pending approval" in the portal and needing a manual click.
- **The dashboard issue never appeared** because `dependencyDashboard: true` was on the
  unread branch.
- **The window was still weekly and in UTC** — a 4-hour slot on Mondays, not nightly local.
- **The OpenApi/TypeScript holds do not apply**, so those two keep showing up as available
  updates although they are deliberately held (#62/#63).

Note what this corrects in the previous log: the app-side approval gate was recorded there
as *"source unknown — hypothesis, not a finding"*. It still is not directly observable, but
the reason the repo config did not override it now **is** established, and it was not on the
app side at all.

## Change

The fix is a repository setting, not a config edit: **default branch switched from `main` to
`develop`** (`PATCH /repos/maststeiner/store-it {"default_branch":"develop"}`, verified).
`develop`'s `renovate.json` is already correct, so no config change was needed.

Why this over the alternatives:

- **Config PR to `main` — not mergeable.** `main` is protected with 13 required status checks
  and `enforce_admins: true`, but `main`'s `ci.yml` is from the scaffold era and defines only
  6 of them (missing: both quality gates, dependency & license review, mutation testing,
  workflow lint, e2e, API contract gate). A branch off `main` can never report the other 7,
  so such a PR would hang on "Expected — waiting for status" indefinitely.
- **Release `develop` → `main`** would work (the head branch carries the full `ci.yml`) but is
  a 210-commit release, not a config fix — the wrong instrument for this problem.
- **Loosening branch protection** to land a two-line PR opens the gates for the sake of a
  setting change.
- **`useBaseBranchConfig: "merge"`** is the documented in-config answer, but the key itself
  has to sit on the default branch to be read — the same chicken-and-egg. With `develop` as
  default it is unnecessary.

Switching the default also matches the branching model already documented in `CLAUDE.md`
(`main` receives release merges only, `develop` is where work integrates) and makes this
class of drift structurally impossible: there is no longer a second, staler config to read.

`main` still carries the July `renovate.json`. It is now unread and will be overwritten by
the next release merge; deliberately not touched here.

Docs updated accordingly: `docs/SETUP.md` §4a (the "on `main`" claim was itself part of the
trap) and the `CLAUDE.md` branching table plus a short note on *why* the default branch is
`develop`, so the next person does not undo it.

## Verification

| Check | Result |
|-------|--------|
| Default branch after the change | `GET /repos/...` → `default_branch: develop` |
| `develop`'s `renovate.json` is the intended config | nightly + `Europe/Zurich`, `dependencyDashboard: true`, `dependencyDashboardApproval: false`, holds for OpenApi/TypeScript present |
| Local `develop` == remote `develop` | both `7600534` |
| Behaviour proof | **not available in CI** — only the next Renovate run can show it. See below. |

## Open verification

1. **Dependency Dashboard issue appears** at the next Renovate run, whenever that is —
   creating it is not a branch operation and so is not gated by the nightly window. This is
   the first honest signal that Renovate is reading the intended config.
2. **Held-back updates turn into PRs** during the next run inside the window
   (00:00–03:59 `Europe/Zurich`), max. 3 open at a time (`prConcurrentLimit`), the rest
   following as those merge. Security updates remain exempt from window, limit and approval.
3. **The OpenApi/TypeScript holds take effect** — if Microsoft.OpenApi v3 or TypeScript v7
   still show up as proposed updates after a run, the holds are still not being read and the
   diagnosis is incomplete.

If (1) has not happened within a day, the remaining cause is app-side and the answer is in
the Mend run log at `developer.mend.io/github/maststeiner/store-it`, which states a skip
reason per dependency. That page is not reachable from the agent sandbox.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | *"Renovate hat einige Pakete, die aktualisiert werden können. Die PRs werden allerdings nicht automatisch erstellt. Warum? Ich muss es aktuell auf der Homepage manuell machen"* | Trigger for this run |
| 2 | Approved opening a PR against `main` — then the blocker surfaced (7 required checks that `main`'s CI cannot report) and the decision was re-opened rather than worked around | The approved path was not executable; scaling it down silently would have been the agent's call to make, not the orchestrator's |
| 3 | Chose **default branch → `develop`** over a release merge or loosening branch protection | Smallest change that removes the cause instead of the symptom |

## Outcome

- **Result:** ready for review
- **Deviations from spec:** n/a (no spec)
- **Harness follow-up:** the 2026-08-06 run reasoned entirely about *what the config says* and
  never asked *which config is live*. For any hosted bot that reads repo config (Renovate,
  Dependabot, CodeRabbit), check the default branch's copy first — a merged PR is not a
  deployed config.
