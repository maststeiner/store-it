# Agent Run Log: Renovate was in Silent mode — the portal can switch delivery off

> **Date:** 2026-08-19
> **Spec:** none — platform configuration, no product behaviour. Tech-debt class of change.
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `docs/renovate-silent-mode`
> **Closes the open verification in:** `2026-08-18-renovate-config-source-of-truth.md`

---

## Task

Yesterday's run switched the default branch to `develop` so Renovate would finally read the
intended config, and left one falsifiable prediction: *a Dependency Dashboard issue appears at the
next run, whenever that is.* A day later it had not. So: why did no PRs arrive overnight?

## Findings

Everything below was read off the GitHub API or the Renovate source, not assumed.

| Observation | Evidence |
|---|---|
| **No Dependency Dashboard issue — ever**, under either config | `GET /repos/.../issues?state=all` — no bot-authored issue at all |
| Yet `:dependencyDashboard` **is** part of `config:recommended` | `lib/config/presets/internal/config.preset.ts` in `renovatebot/renovate` |
| Repo status is `activated` (10 Renovate PRs merged) ⇒ Mend schedules jobs **4-hourly** | `GET /repos/.../pulls?state=all` + `docs/usage/mend-hosted/job-scheduling.md` |
| So ~5–6 jobs since the default-branch switch (2026-08-18 20:00 UTC) — and **no branch, no PR, no issue, no `renovate[bot]` event** | `GET /repos/.../branches`, `/pulls`, `/events` (event window reaches back to 2026-08-09) |
| The only Renovate artefacts ever: **26 PRs within 4 minutes** on 2026-08-02 | `GET /repos/.../pulls?state=all` |
| …which no scheduled run can produce: `prHourlyLimit` defaults to **2** | `lib/config/options/index.ts` |
| **Security counter-check:** 4 new high-severity Dependabot alerts at 2026-08-19 01:47 UTC, no PR — although `vulnerabilityAlerts` is exempt from window, limit and approval | `GET /repos/.../dependabot/alerts`, option defaults in `lib/config/options/index.ts` |
| The repository config itself is fine | `renovate-config-validator` clean; live copy sits on the new default branch |

## Diagnosis

**Mend Renovate Cloud ran this repository in Silent mode (`dryRun=lookup`).** Confirmed in the
Developer Portal, panel *Repo Engine Settings*: `Dependency Updates (Renovate): Silent`
(plan Community/Free, Renovate 44.33.2). The portal's own setting description says what that means:

> *"renovate will run on all installed repositories, but issues and the dependency dashboard will
> not be created in the repo, dependency updates will only be available in the repo page of the
> Developer Portal, and Remediate will not run."*

That is the whole symptom, including the parts the config explanation never covered: the lookups
succeed (so the portal lists updates), nothing is delivered to the repository, and PRs have to be
created by hand from the portal — which is exactly what the 26 PRs in 4 minutes on 2026-08-02 were.

Silent mode is the documented default when the app is installed for **"All repositories"**, chosen
by Mend so that a bulk install does not onboard hundreds of repositories at once.

**The part worth remembering: no repository config can undo this.** `dryRun` is an admin option, not
a `renovate.json` key. Two rounds of config work (2026-08-06 and 2026-08-18) were each correct and
each necessary — the config really was stale, and the default branch really was the wrong source —
but neither could have fixed the symptom, because the delivery switch sits one layer above the
repository.

## Change

- **Portal (not in this repository):** *Dependency Updates (Renovate)* switched from **Silent** to
  **Interactive**, *Automated PRs* verified on, and a run triggered by hand.
- **This PR** records the finding where the next person will look: `docs/SETUP.md` §4a gets the
  portal switches as the *first* thing to check when Renovate goes quiet, and `renovate.json`'s
  `description` block says outright that nothing in that file can make Renovate deliver.

## Verification

| Check | Result |
|-------|--------|
| Dependency Dashboard | **Issue #93, created 2026-08-19 20:30:46 UTC** — the first one this repository has ever had |
| Config from `develop` is not just read but applied | the dashboard offers `microsoft.openapi to 2.12.0`, **not** v3, and no `typescript` v7 — the `allowedVersions` holds (#62/#63) are in force, which also closes the third open verification from 2026-08-18 |
| Updates queued | 12 entries under *Awaiting Schedule*; branch creation waits for 00:00–03:59 `Europe/Zurich`, 3 at a time |
| `renovate.json` still valid after the edit | `renovate-config-validator` clean |

## Open verification

1. **Normal PRs arrive tonight** in the window, max. 3 at once.
2. **Vulnerability PRs do not work yet.** The dashboard has no vulnerability section, and the four
   high-severity alerts from 2026-08-19 01:47 UTC (`js-yaml`, `ip-address`, `fast-uri` — all
   transitive npm *dev* dependencies) produced nothing, although `vulnerabilityAlerts` defaults to
   `prCreation: 'immediate'` with `rangeStrategy: 'update-lockfile'`, which can remediate transitive
   dependencies through the lockfile. Most likely the app lacks the **Dependabot alerts: read**
   permission. If normal PRs arrive tonight and security PRs do not, that is the confirmation.

## Found on the way, not fixed here

Both are dashboard output, both deserve a tech-debt issue rather than a drive-by fix:

- **Config migration proposed.** Renovate offers a *Config Migration* PR — worth taking once, so the
  file stops carrying deprecated syntax.
- **`xunit` is flagged deprecated**, with no automatic replacement (xunit v3 ships as `xunit.v3`).
  That is a test-stack migration, not a dependency bump.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | *"unterdessen analysieren, warum die renovate prs in der nacht nicht erstellt wurden"* | Trigger for this run |
| 2 | Opened `developer.mend.io` in the sandbox network policy when the analysis hit the portal wall | Turned out necessary but not sufficient: the portal API answers `401 Missing Auth Header` without a session, even for public repositories — so portal facts still come from the orchestrator, not from the agent |
| 3 | Read the *Repo Engine Settings* panel and reported `Dependency Updates (Renovate): Silent`, then switched to Interactive and triggered a run | Turned the hypothesis into a finding, and the finding into a fix — both steps are portal-side and cannot be done from here |

## Outcome

- **Result:** ready for review
- **Deviations from spec:** n/a (no spec)
- **Harness follow-up:** yesterday's lesson was *"a merged PR is not a deployed config — check the
  default branch"*. Today extends it: **check that the vendor is delivering at all before reading
  any config.** Three sessions went into a bot that was, by an admin setting, told to stay quiet.
  A hosted bot has a switch above your repository, and its silence looks exactly like a
  misconfiguration.
