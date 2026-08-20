# Agent Run Log: a four-hour window against a four-hour job cadence

> **Date:** 2026-08-20
> **Spec:** none — platform configuration, no product behaviour. Tech-debt class of change.
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `ci/renovate-schedule-window`
> **Follows:** `2026-08-19-renovate-silent-mode.md`

---

## Task

Silent mode was switched off yesterday evening and the Dependency Dashboard appeared. The
prediction was: PRs arrive in tonight's window. They did not. Establish why.

## Findings

| Observation | Evidence |
|---|---|
| **Renovate is running now** — the dashboard content changes | Issue #93 `updated_at` 2026-08-20 14:53 UTC, and `microsoft.openapi` moved 2.12.0 → **2.12.1** since yesterday |
| **Nothing was created**: no `renovate/*` branch, no PR, nothing automerged | `GET /branches`, `GET /pulls?state=all`, `GET /commits?sha=develop&since=…` |
| All 12 updates sit under ***Awaiting Schedule*** | dashboard body — Renovate's own words for "I ran, but branch creation is not allowed right now" |
| The window was **four hours**: `schedule:daily` = cron `* 0-3 * * *` | `lib/config/presets/internal/schedule.preset.ts` upstream |
| Mend schedules an `activated` repository **every four hours** | `docs/usage/mend-hosted/job-scheduling.md` upstream |
| The observed run was 14:53 UTC = **16:53 local** | consistent with a phase (…04:53, 08:53, 12:53, 16:53) that never touches 00:00–03:59 |

## Diagnosis

**A four-hour window against a four-hour job cadence is a coin flip on the phase — and this one
loses.** Not a one-off: if the app's runs never land between 00:00 and 03:59 local, the window is
missed *every* night, and the dashboard keeps re-listing the same updates as *Awaiting Schedule*
forever. The config was correct in every other respect, which is what made it look like a config
problem for a third time.

The second candidate — the portal's *Automated PRs* switch still being off — was ruled out by
experiment rather than by reading: ticking one checkbox on the dashboard (`unschedule-branch`)
produced **PR #95 within a minute**. Checkbox-driven updates ignore the schedule, so delivery works
and *Automated PRs* is on. Only the window is wrong.

## Change

`renovate.json`: the `schedule:daily` preset is replaced by an explicit **eight-hour** window,
`* 22-23,0-5 * * *` with `timezone: Europe/Zurich` — 22:00–05:59.

Any four-hourly phase hits an eight-hour window at least twice, so the window can no longer be
missed. The intent from 2026-08-06 is unchanged: batch dependency churn into the night rather than
scatter it through the working day. `prConcurrentLimit: 3` still caps what arrives, and security
updates remain exempt from the window entirely.

Rejected alternatives:

- **`schedule:nonOfficeHours`** (a documented preset, weeknights 22:00–04:59 plus all weekend) would
  also fix the phase problem, but it opens the whole weekend to PR creation. "At night" is the
  stated intent; this keeps it.
- **Dropping the schedule** removes the failure mode by removing the feature — dependency PRs would
  then land at any hour of the working day.
- **Leaving it and living with manual checkbox ticks** is the status quo that cost three sessions.

The rule is written into both the config's `description` block and `docs/SETUP.md` §4a: **keep the
window at least twice the app's job cadence**, and recognise the symptom — Renovate runs, the
dashboard changes, no branch is ever created.

## Verification

| Check | Result |
|-------|--------|
| Delivery works at all | **PR #95** created within a minute of ticking a dashboard checkbox |
| `renovate.json` after the edit | `renovate-config-validator`: *Config validated successfully* |
| Cron syntax for a window crossing midnight | `22-23,0-5` — the same split the upstream `nonOfficeHours` preset uses (`0-4,22-23`) |

## Open verification

**Tonight is the test.** Expected tomorrow morning: three open Renovate PRs against `develop` (the
`prConcurrentLimit`), the rest following as those merge. If the dashboard again shows everything
under *Awaiting Schedule* with no branches, the cause is not the window and the next place to look
is the job log in the portal — specifically whether the runs report the schedule as not matching.

Still open from yesterday, unchanged: **no vulnerability PRs**. The four high-severity alerts of
2026-08-19 (`js-yaml`, `ip-address`, `fast-uri`, all transitive npm dev dependencies) produced
nothing, although `vulnerabilityAlerts` ignores the window entirely — so tonight's result does not
affect this one. Most likely the app lacks the **Dependabot alerts: read** permission.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | *"aus meiner sicht ist renovate nicht gelaufen oder hat keine prs erstellt"* | Trigger for this run — and half right in a useful way: it ran, it just could not deliver |
| 2 | Chose the checkbox experiment before the config change, over asking the portal first or changing the window blind | Two minutes of evidence beat a plausible fix; had *Automated PRs* been off, widening the window would have changed nothing and looked like a failed diagnosis |

## Outcome

- **Result:** ready for review
- **Deviations from spec:** n/a (no spec)
- **Harness follow-up:** three sessions on this bot produced three lessons that stack:
  a merged PR is not a deployed config (2026-08-18), a vendor can switch delivery off above your
  repository (2026-08-19), and **a schedule window only means something relative to how often the
  runner actually runs** (today). Each looked like the previous one's fix had failed.
