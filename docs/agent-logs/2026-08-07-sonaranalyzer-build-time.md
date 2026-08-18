# Agent Run Log: Run Sonar rules at build time (issue #74)

> **Date:** 2026-08-07
> **Spec:** none — tech-debt issue #74; the issue body is the frozen input (repo convention).
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `ci/sonaranalyzer-build-time`

---

## Task

Make S3776-class Sonar findings fail Gate 1 (build) instead of Gate 3 (SonarCloud quality
gate), per issue #74. Follow-up instruction during the run: **enforce S138 on both sides**
— locally *and* in SonarCloud.

## What the issue assumed, and what turned out to be true

Issue #74 proposed "add the `SonarAnalyzer.CSharp` package; with
`CodeAnalysisTreatWarningsAsErrors=true` already set, Sonar rules then break the build."
Measured, that premise is wrong in two independent ways:

| Assumption | Measurement |
|---|---|
| The package makes Sonar rules fire | Partly. Some rules ship enabled and report as warnings (S1144, S125 fired immediately). **S3776 stayed silent** on a method with cognitive complexity 18 until an explicit `severity` was set in `.editorconfig`. |
| `CodeAnalysisTreatWarningsAsErrors` escalates them | No. It escalates `CA*` rules only. With the package in and S3776 at `warning`, the build reported `1 Warning(s)` and **succeeded** — Gate 1 would not have failed. |

This also corrects a claim I made while fixing #73: I had attributed the analyzer's silence
there to incremental builds. The real cause was that S3776 needs an explicit severity — in
that session I had added an `.editorconfig` override at the same time and mistook which
change made the rule fire.

So the working mechanism is: **package (rule availability) + explicit `severity = error`
per rule in `.editorconfig` (enablement *and* escalation).**

## Rule selection — measured, not guessed

Ten complexity/"brain-overload" rules were probed against the current code:

- **Nine fire zero times**: S3776, S1541, S107, S134, S1067, S104, S1479, S1448, S2436.
  Enforcing them therefore locks in the status quo rather than grandfathering debt.
- **S138 (method line count) fired twice**: `MapStorageRoutes` (90 lines) and `MapItemRoutes`
  (143 lines) — both methods I introduced in #73.

S138 was initially left out, because SonarCloud never reported those two methods, so
enabling it locally would have made Gate 1 stricter than Gate 3 — the opposite of the
alignment this change is for. **The orchestrator overruled that: enforce it on both sides.**
Consequently:

- `StorageEndpoints` now maps **one endpoint per method** (nine small `MapXxx` methods).
  Grouping endpoints hits one budget or the other — S3776 when guards accumulate, S138 when
  the declarative chains pile up — while one method per endpoint keeps both flat as
  endpoints are added. Handler bodies, routes, operationIds and status codes are untouched.
- SonarCloud's side cannot be done from the agent sandbox (the quality-profile API needs
  auth, and `SONAR_TOKEN` is a CI secret). `docs/SETUP.md` §3 carries it as an open
  `[platform]` item with the exact click path: extend *Sonar way* (built-ins are read-only),
  activate `csharpsquid:S138`, assign the profile to the backend project.

Also verified: SonarCloud runs C# analyzer **10.31 (build 145097)**, so the NuGet package is
pinned to `10.31.0.145097` — the version-drift risk the issue flagged is closed by
construction, and the pin must move together with SonarCloud's.

## Findings fixed on the way

Two rules that ship enabled reported real issues, both in test code (SonarCloud's analysis
does not flag them — why exactly is unclear, and the quality-profile API needs auth, so it
is left unexplained rather than guessed):

- **S1144** — unused private field `DomainNamespace` in `LayeringTests`. Checked first
  whether it signalled a *missing* layering assertion: it does not. All four directions
  (Domain→any, Application→Api/Infra, Api→Infra, Infra→Api) are covered, and the two Domain
  rules reach the assembly through `typeof(Domain.ExpiryRules)`, which the compiler checks,
  rather than by name. Constant removed, with a comment recording why there is none.
- **S125** — "commented out code" in `StorageEndpointsTests`. A **false positive**: the
  comment documents AC-01a but was shaped like assignments (`expiredCount = …;`). Reworded
  as prose, keeping the rule strict and the documentation intact.

Rules that ship enabled stay at `warning` (not escalated to `error`): escalating everything
would mean a Renovate bump of the analyzer can break the build on rules nobody chose. That
line is deliberate and is the orchestrator's to move.

## Verification

| Check | Result |
|-------|--------|
| Acceptance test, S3776 | Pre-#73 file (complexity 18) reintroduced → `error S3776` + **Build FAILED**. Reverted. |
| Acceptance test, S138 | Pre-refactor file (90/143-line methods) reintroduced → `error S138` ×2 + **Build FAILED**. Reverted. |
| `dotnet build -c Release` on the real code | 0 warnings / 0 errors |
| `dotnet test -c Release` | 113/113 pass (Domain 46, Architecture 9, Api.Service 58) |
| `dotnet csharpier check .` | 48 files clean |
| OpenAPI contract | `openapi/StoreIt.Api.json` regenerated on build, byte-identical → no drift |
| Mutation testing (`dotnet stryker`) | 81.33 %, break threshold 60 → passes. Checked deliberately: Stryker compiles mutants with these analyzer settings, so error-severity rules could have turned mutants into compile errors. They did not. |
| Analyzer/gate version alignment | NuGet `10.31.0.145097` == SonarCloud C# `10.31 (145097)` |

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | *"nun issue nummer 74 machen"* | Task |
| 2 | *"s138 auf beiden seiten aktivieren"* | The agent had excluded S138 to avoid making Gate 1 stricter than Gate 3. The orchestrator chose the stricter standard on both sides instead — so the endpoint mapping was refactored to one method per endpoint, and SonarCloud's profile change is documented as an open platform item. |

## Self-inflicted incident (recorded, since the log is the accountability record)

While proving that S138 fails the build, I restored the old file with
`git show HEAD:… > file` and then reverted with `git checkout <file>` — which threw away the
**uncommitted refactor** as well, not just the experiment. Rewritten from scratch, and a
backup was taken before the second attempt. Rule for next time: never use `git checkout` to
undo a regression experiment on top of uncommitted work — copy the file aside first, or
commit before experimenting.

## Outcome

- **Result:** ready for review
- **Deviations from spec:** none (no spec). Issue #74's premise was wrong and is corrected
  in this log and in `docs/SETUP.md`; the goal it asked for is met.
- **Harness follow-up:** the enforced rule list in `.editorconfig` is the extension point —
  add a rule, build, fix what it finds. The one gap that is *not* code and cannot be done
  from here is the SonarCloud profile change for S138, tracked as an open item in
  `docs/SETUP.md` §3.
