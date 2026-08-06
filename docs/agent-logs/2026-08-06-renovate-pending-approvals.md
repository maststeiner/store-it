# Agent Run Log: Renovate creates no PRs — diagnosis and config fix

> **Date:** 2026-08-06
> **Spec:** none — CI/dependency-policy configuration, no product behaviour. Tech-debt
> class of change; the orchestrator's observation ("pending approvals, but no PRs") is the
> frozen input.
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `ci/renovate-explicit-dashboard`

---

## Task

Explain why Renovate showed pending approvals without opening PRs, then make the
behaviour explicit in `renovate.json`. Follow-up instruction during the run: run
nightly instead of weekly.

## Findings (what was verified, not assumed)

| Observation | Evidence |
|---|---|
| Renovate last opened PRs on **2026-08-02, 13:07 UTC** (wave #51–#61); none since | `gh pr list` filtered to the bot |
| No `renovate/*` branch exists on the remote, and no Renovate PR is open | `gh api repos/.../branches` |
| **No Dependency Dashboard issue exists** — not open, not closed; no issue in the repo was authored by the bot | `gh issue list --state all`, issue search |
| The bot is alive: last activity 2026-08-03 20:04 UTC (ignore-notices on #55/#57) | issue search by `commenter:app/renovate` |
| `schedule:weekly` → `schedule:earlyMondays` → cron `* 0-3 * * 1`, **UTC** because no `timezone` was set | read from `lib/config/presets/internal/schedule.preset.ts` in renovatebot/renovate |
| `config:recommended` extends `:dependencyDashboard` (= `dependencyDashboard: true`) but **not** the approval preset | read from `lib/config/presets/internal/config.preset.ts` and `default.preset.ts` |
| No approval gate anywhere in the repo, and no org-level config repo (`maststeiner/.github` does not exist) | grep of `renovate.json`; `gh api repos/maststeiner/.github/...` → 404 |

## Diagnosis

Two independent causes, and only one of them was a defect:

1. **Schedule (working as configured).** Branch and PR creation was limited to Mondays
   00:00–03:59 **UTC**. The question was asked on a Wednesday, so nothing was due until
   the following Monday. Not a fault — but a week of latency, and the window sat at
   02:00–06:00 local time rather than "at night" as intended.
2. **Approval gate with no way to satisfy it (the actual defect).** The repo requests no
   approval, so the "pending approval" state came from the hosted Mend app's own
   inherited config, which is not visible from the repository. Meanwhile no Dependency
   Dashboard issue exists — and the dashboard is exactly where an approval is granted. An
   approval gate plus a missing dashboard is unsatisfiable: those updates wait forever.

Not a defect, deliberately held: #55 (Microsoft.OpenApi v3) and #57 (TypeScript v7) are
pinned out by `allowedVersions` and tracked as #62/#63; #61 (sonarqube-scan-action v8) was
handled manually. If they appear in any pending list, that is correct and should stay.

## Change

`renovate.json` now states the intent explicitly, because repo config wins over the
invisible inherited config:

- `dependencyDashboard: true` — implied by `config:recommended`, now explicit, so a
  missing dashboard becomes a signal instead of an ambiguity.
- `dependencyDashboardApproval: false` — overrides whatever the app side sets. Human
  control stays where it belongs: majors never automerge, and holds use `allowedVersions`
  — PR *creation* is not the control point.
- `timezone: "Europe/Zurich"` — the cron windows are local now.
- `schedule:weekly` → `schedule:daily` (cron `* 0-3 * * *`) — nightly, per the
  orchestrator's follow-up. `prConcurrentLimit: 3` still caps throughput per run.

Each key carries its reasoning in the config's own `description` array, so the next reader
does not have to reconstruct this.

## Verification

| Check | Result |
|-------|--------|
| `renovate-config-validator` | `Config validated successfully against 1 file(s)` |
| JSON well-formed | `python3 -m json.tool` clean |
| Preset resolution | `schedule:daily` → `* 0-3 * * *`, read from the preset source rather than assumed |
| Effective keys | `timezone=Europe/Zurich`, `dependencyDashboard=true`, `dependencyDashboardApproval=false`, `prConcurrentLimit=3`, `baseBranches=[develop]` |
| Behaviour proof | **not available in CI** — only the next Renovate run can show it. See below. |

## Open verification (deliberately stated, not glossed over)

Whether this actually fixes it is only observable after Renovate's next nightly run:

1. A **Dependency Dashboard** issue should appear in the repo.
2. Updates previously stuck in "pending approval" should become branches/PRs, subject to
   `prConcurrentLimit: 3`.

If neither happens, the app-side config is overriding the repo config more aggressively
than expected, and the answer is in the run log at
`developer.mend.io/github/maststeiner/store-it`, which states the skip reason per
dependency ("not within schedule", "rate-limited", "dependency dashboard approval
required"). That page is not reachable from the agent sandbox.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | *"bei renovate gibt es noch pending approvals, warum werden die prs nicht automatisch erstellt?"* | Trigger for the diagnosis |
| 2 | Chose "state the intent explicitly" over "read the Mend log first" | Fixes the ambiguity in the repo regardless of what the app side is set to |
| 3 | *"zusätzlich noch öfters laufen lassen, am besten täglich in der nacht"* | Weekly latency was too slow — schedule changed to `schedule:daily` mid-run, and the timezone pin makes "at night" mean local night |

## Outcome

- **Result:** ready for review
- **Deviations from spec:** n/a (no spec)
- **Harness follow-up:** none. Note for future dependency questions: Renovate preset names
  hide their real cron — `schedule:weekly` is a 4-hour window on Mondays in UTC, not "some
  time each week". Resolve presets against the source before reasoning about them.
