# Agent Run Log: xunit v2 → xunit.v3 on Microsoft.Testing.Platform (spike → PR)

> **Date:** 2026-10-05
> **Spec:** none — tech-debt issue #102; the issue body is the frozen G1 input (established convention for review-derived items)
> **Persona(s):** developer
> **Model:** Claude Fable 5.1 (claude-fable-5-1)
> **Branch / PR:** `feature/xunit-v3` → PR (linked from #102)

---

## Task

#102 kept the backend on the deprecated xunit 2.9.3 because Stryker.NET could not mutate xunit.v3
projects (Microsoft Testing Platform). Stryker 5.0.0 (2026-09-11, already in `dotnet-tools.json`)
ships an MTP test runner. Marcel asked for a spike: migrate, prove `dotnet test` + coverage gate +
Stryker still work, then PR.

## Plan

1. Package swap and the mechanical code changes; build.
2. Find the execution mode in which all three consumers work: `dotnet test` with the 70 % coverage
   gate (job 1), Sonar's opencover report (job 3), Stryker (job 1a).
3. Adjust CI commands and docs; verify locally; open PR.

## Key Decisions

- **Code changes are small:** four fixtures move from `Task InitializeAsync()/IAsyncLifetime.DisposeAsync()`
  to the v3 `ValueTask` signatures (the fixtures derive from `WebApplicationFactory`, so
  `DisposeAsync` becomes an `override`); one alias for `TestResult`, which now exists in both
  `NetArchTest.Rules` and `Xunit`.
- **Analyzer rule xUnit1051 switched off for the test tree** (`tests/Directory.Build.props`): it asks for
  `TestContext.Current.CancellationToken` on roughly 60 HttpClient/EF calls in the integration tests.
  Threading the token adds nothing to these tests and buries their intent; documented at the switch.
- **VSTest mode is not an option on the .NET 10 SDK.** Experiment 2 (keep `dotnet test` on the VSTest
  adapter, coverlet.msbuild untouched) fails at build time: Microsoft.Testing.Platform's targets reject
  the VSTest target for any MTP application on SDK ≥ 10, with or without `UseMicrosoftTestingPlatformRunner`.
  And Stryker's VsTest runner against xunit.v3 reports 0 % (all mutants survive) — the known #3117.
- **`dotnet test` switches to MTP via `global.json`**, not `dotnet.config`: the 10.0.3xx SDK reads
  `{ "test": { "runner": "Microsoft.Testing.Platform" } }` from global.json (verified in the SDK source,
  `TestCommandDefinition.Create`). A `dotnet.config` was silently ignored. No `sdk` pin is added.
- **Coverage moves to `coverlet.MTP`** (coverlet.msbuild and coverlet.collector are VSTest-only, stated
  in coverlet's own docs). Filters live in `testconfig.json` next to each behaviour test project
  (opencover format, `[*]Microsoft.*,[*]System.*` excluded as before, test assemblies excluded); the
  threshold is a CLI flag in CI (`--coverlet-threshold 70 --coverlet-threshold-type line`). Verified
  negatively: threshold 100 fails the run (exit 14).
- **Architecture tests have no coverlet reference** instead of `CollectCoverage=false`: `--coverlet`
  would be an unknown option for them, so job 1 and the Sonar job run the test projects explicitly
  (three `dotnet test --project` calls instead of one solution-wide call). The plain `dotnet test`
  from `backend/` still runs all 205 tests for developers.
- **Microsoft.NET.Test.Sdk and xunit.runner.visualstudio removed.** Not needed on MTP (IDEs talk MTP
  natively). Side effect that cost one round: Test.Sdk had been setting `OutputType=Exe` implicitly,
  xunit.v3 requires it — now explicit in the three csproj files.
- **Why the first MTP Stryker run reported 0 %:** with Test.Sdk and coverlet.msbuild still referenced,
  Stryker's MTP runner found and ran all tests but no mutant was ever active. After removing both
  (and making the projects proper MTP executables) the debug run on `ExpiryRules.cs` killed 11/11
  testable mutants. Stryker 5's per-test coverage capture runs once per mutated assembly (four
  passes here); the cost shows up in the full nightly run, not on `--since` PRs.
- **Sonar report path** changes to `TestResults/coverage.opencover.*.xml` (coverlet.MTP timestamps
  the file names and writes to `--results-directory`).

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "Wiedervorlagen #102, #62, #63 prüfen" | Review of the parked items after the Dependabot thread |
| 2 | "ja, spike starten" | Go for #102 once Stryker 5 was confirmed as the unblock |

## Outcome

- **Result:** locally green — build 0 warnings, `dotnet test` solution-wide 205/205, Domain 70/70 and
  Api.Service 126/126 with coverlet.MTP (Api.Service line coverage 97.99 % across all four modules),
  architecture tests 9/9 with `--filter`, CSharpier clean, Stryker MTP debug run 100 % on the probed
  file. CI commands in `ci.yml` (job 1, Sonar backend, architecture gate) and `stryker-config.json`
  (`test-runner: mtp`) updated; docs (SETUP, metrics, tech-stack, test-guidelines) say coverlet.MTP.
- **Deviations from spec:** none (no spec — tech-debt convention).
- **Harness follow-up:** (1) the first full nightly Stryker run on `develop` after merge is the real
  performance test of the MTP runner; if the four per-assembly coverage passes make it too slow,
  `coverage-analysis: perTestInIsolation` vs `perTest` is the knob. (2) Renovate now tracks `xunit.v3`
  and `coverlet.MTP`; the NuGet deprecation entry in dashboard #93 disappears. (3) xUnit1051 can be
  revisited if cancellation ever matters in the integration tests.
