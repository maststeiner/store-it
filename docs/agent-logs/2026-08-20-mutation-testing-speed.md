# Agent Run Log: the mutation job's "changed code only" mode was a full run

> **Date:** 2026-08-20
> **Spec:** none — CI performance, no product behaviour. Tech-debt class of change.
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `ci/mutation-speed`

---

## Task

Job `1a · Backend mutation testing` is the longest job in the pipeline by a wide margin. Find out
where the time goes and cut it.

## Measurements

Durations from the Actions API, mutant numbers from the job logs and the uploaded markdown report.

| Run | Duration | Mutants |
|---|---|---|
| Nightly full run on `develop` (2026-08-20 03:41) | **37:04** | 628 created |
| PR run, mode `incremental`, `fix/sonar-findings` | **37:20** | **628 created** |
| Mode `skip` (no `backend/` change) | ~10 s | – |

Setup (checkout → "628 mutants created") is ~3.5 minutes; the remaining ~34 minutes are the mutant
runs. Of the 628, 139 are ignored and 18 fail to compile, so roughly 470 mutants are actually
executed — about 4.3 seconds each.

Where the mutants live, from the markdown report:

| Project | Mutants | Share | Notable scores |
|---|---:|---:|---|
| **StoreIt.Api** | **386** | **61.5 %** | `AuthenticationSetup.cs` 19.70 %, `SecuritySchemeTransformer.cs` 12.12 %, `AuthEndpoints.cs` 37.29 % |
| StoreIt.Domain | 125 | 19.9 % | `Item.cs` 86.11 %, `User.cs` 87.88 %, `ExpiryRules.cs` 100 % |
| StoreIt.Infrastructure | 64 | 10.2 % | `StorageConfiguration.cs` 61.90 % |
| StoreIt.Application | 53 | 8.4 % | `StorageUseCases.cs` 92.31 %, `ItemUseCases.cs` 100 % |

## Diagnosis

**The "changed code only" mode never was incremental.** The PR run created the same 628 mutants,
tested the same set and reported the same score (60.93 %) as the full run, in the same 37 minutes.
The reason is one line in its log:

```
[INF] Changed test file /home/runner/work/store-it/store-it/backend/Dockerfile
```

Stryker classifies every changed file that does not belong to a mutated project as a changed
**test** file, and a changed test file invalidates *all* mutants. PR #92 touched
`backend/Dockerfile`, which sits above `src/`, so the `--since` filter collapsed into a full run.
The same happens for `Directory.Packages.props`, any `.yml`, any `.md` next to backend code — in
other words for most backend PRs, and for every Renovate dependency bump.

Two smaller things fell out of the measurement:

- **Half the runner sits idle.** Stryker defaults `concurrency` to logical processors / 2, which is
  2 workers on a 4-vCPU GitHub runner.
- **The score is 60.93 % against a break threshold of 60.** Six more surviving mutants and the
  build goes red — and the number is dragged down almost entirely by the Api wiring files above.

A third suspicion did **not** survive checking: the cleartext table in the CI log made it look like
`Program.cs` was being mutated despite `"mutate": ["!**/Program.cs"]`. The markdown report shows the
truth — 33 ignored, 0 survived. The exclusion works; the truncated log table was misread.

## Change

Two changes, both narrow:

1. **`since.ignore-changes-in`** in `backend/stryker-config.json` — the documented Stryker option for
   exactly this problem. It lists the file kinds that cannot change what a mutant does: docs,
   workflows, container and tool config, and the central package versions. `.csproj` and `.sln` are
   deliberately *not* ignored, because a project file can add a reference and change behaviour.
   `since.enabled` stays `false` so the nightly backstop and local runs remain full runs; CI turns
   the feature on per pull request with `--since:origin/<base>`.
2. **`--concurrency 4`** on both Stryker steps in CI, so the job uses the whole runner.

Stryker documents `ignore-changes-in` as an accuracy-for-speed trade-off, and that is what it is:
a dependency bump no longer invalidates the mutation result. The nightly full run on `develop` is
the backstop that catches what the shortcut lets through — it is the reason this trade is safe.

**Not in this PR, deliberately:** restricting `mutate` to Domain and Application. It would remove
61.5 % of the mutants and, with them, the need to drive the Testcontainers-backed
`StoreIt.Api.Service.Tests` for every mutant — and it would move the score from 60.93 % to roughly
84 %. But that is a question about *what deserves mutation testing*, not about performance, and it
belongs in its own PR with its own argument.

## Verification

| Check | Result |
|-------|--------|
| `stryker-config.json` parses | yes; no unsupported keys added (Stryker has no comment field, so the reasoning lives in the workflow comment and here) |
| `ci.yml` parses | yes; `actionlint` runs as job 5 in CI |
| **The fix itself** | **this PR proves or disproves it on its own run**: it touches `backend/stryker-config.json`, so the scope step picks `incremental`. If job 1a comes back in a couple of minutes with far fewer mutants, `ignore-changes-in` works. If it comes back at 37 minutes, the command line does not override `since.enabled: false` and the config needs splitting into two files |

## Open verification

The concurrency change is a guess with a sound basis, not a measurement: 4 workers on 4 vCPUs
should cut the mutant phase materially, but the Api mutants drive Testcontainers, and four parallel
Postgres containers may push back. The nightly run on `develop` after this merges is the number to
compare against tonight's 37:04.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | *"kannst du parallell noch analysieren, wie wir den backend mutation build schneller machen könnten, der geht immer am längsten"* | Trigger for this run |
| 2 | Chose measures 1 and 3 from the analysis and left the `mutate` scoping out | Performance fix and test-strategy decision kept apart |
| 3 | Was told that the third proposed measure (a broken `Program.cs` exclusion) did not exist — I had misread a truncated log table | Recorded rather than quietly dropped: the finding was reported before it was verified against the machine-readable report |

## Outcome

- **Result:** ready for review
- **Deviations from spec:** n/a (no spec)
- **Harness follow-up:** the cleartext reporter's table is column-truncated in CI logs and easy to
  misread. When a mutation number matters, read `StrykerOutput/**/reports/mutation-report.md` from
  the job artifact, not the log table.
