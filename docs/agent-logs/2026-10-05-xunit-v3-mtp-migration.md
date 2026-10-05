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
- **CI round 1 (MTP, concurrency 4): 68.41 % against 74.33 % on the VsTest nightly, same 682 mutants.**
  The per-file diff of the two reports put the loss almost entirely into once-per-process code:
  `StorageConfiguration` 0/38 killed (was 25/38), `UserConfiguration` 0/10 (7/10), `StoreItDbContext`
  0/6 (4/6), `DomainExceptionHandler` 6/14 (13/14), `CsrfEndpointFilter` 4/9 (5/9).
- **Not the upstream concurrency bug.** stryker-net#3832 (5.0.0 under-reports kills nondeterministically
  at concurrency > 1) looked like a match; CI round 2 at `--concurrency 1` produced the identical
  68.41 % with identical per-file numbers in 1 h 29 instead of 1 h 11. The loss is deterministic.
- **Actual cause: EF Core's process-wide model cache meets a long-lived test process.** EF caches its
  internal service provider (and the compiled model) keyed by service-affecting options only — the
  connection string does not count — so every `WebApplicationFactory` host in one process shares the
  first model built. The VsTest runner marks mutants that run in static/one-time context as "static"
  and gives them fresh sessions; the MTP runner reports "0 static mutations" and keeps its test-server
  processes warm (stryker-net#3742; fix PR #3695 open since July). A mutant in an
  `IEntityTypeConfiguration` is therefore seen by at most the first host of the process.
- **Fix on the test side only:** `EfServiceProviderCaching.DisableEfServiceProviderCaching()` wraps the
  app's `DbContextOptions<StoreItDbContext>` registration with `EnableServiceProviderCaching(false)` in
  all four fixtures. The model is rebuilt per host; the Api.Service suite goes from ~10 s to ~22 s.
  Production code is untouched. The two static `readonly` dictionaries (`DomainExceptionHandler.ByType`,
  `CsrfEndpointFilter.SafeMethods`) stay a known gap of up to 8 mutants until #3695 lands —
  restructuring production code for a mutation tool's limitation is not on the table.
- `coverage-analysis` `all` and `perTestInIsolation` were tried on the way: no gain, 2–5× the time.
  Also learned: single-file `--mutate` runs are not a valid proxy for kill rates under MTP (fresh
  processes make once-per-process mutants look alive); only full runs count.
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
  file; CI rounds 1–2 (MTP) 68.41 % → round 3 with the EF cache fix pending at the time of writing. CI commands in `ci.yml` (job 1, Sonar backend, architecture gate) and `stryker-config.json`
  (`test-runner: mtp`) updated; docs (SETUP, metrics, tech-stack, test-guidelines) say coverlet.MTP.
- **Deviations from spec:** none (no spec — tech-debt convention).
- **Harness follow-up:** (1) the first full nightly Stryker run on `develop` after merge is the real
  performance test of the MTP runner; if the four per-assembly coverage passes make it too slow,
  `coverage-analysis: perTestInIsolation` vs `perTest` is the knob. (2) Renovate now tracks `xunit.v3`
  and `coverlet.MTP`; the NuGet deprecation entry in dashboard #93 disappears. (3) xUnit1051 can be
  revisited if cancellation ever matters in the integration tests.
