# Agent Run Log: clear the six open SonarCloud findings

> **Date:** 2026-08-18
> **Spec:** none — code hygiene against static-analysis findings. Tech-debt class of change.
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `fix/sonar-findings`

---

## Task

SonarCloud showed open findings on both projects while the quality gate stayed green. Establish
why, then clear them.

## Why the gate was green (this is the part worth remembering)

Six issues, not eight: SonarCloud's Clean Code model gives an issue **impacts on several
software qualities**, so the two `typescript:S7059` findings count under *both* Reliability and
Maintainability. Hence "2 Reliability + 3 Maintainability" in the frontend and 3 in the backend.

All six carry the legacy type `CODE_SMELL`, and `bugs` is 0. The measured
`reliability_rating` was **A** even though two issues have a `RELIABILITY:HIGH` impact — so the
ratings the quality gate checks are computed from the *legacy* type model, not from the new
impacts. Maintainability Rating is a debt-*ratio* measure (≤5% = A), which a handful of smells
never moves in a codebase this size. The gate therefore passed by design, and
`sonar.qualitygate.wait=true` did exactly what it is supposed to.

**Consequence, unchanged by this PR:** nothing in CI blocks on issue *count*. If findings like
these should fail a build, the gate needs a condition on new-code issues — a SonarCloud gate
setting, not a repository change. Deliberately left to a human decision.

## The findings and what was done

| Rule | Location | Impacts | Action |
|---|---|---|---|
| `typescript:S7059` | `frontend/src/app/app.ts:21` | RELIABILITY:HIGH, MAINTAINABILITY:HIGH | startup work moved from the constructor to `ngOnInit` |
| `typescript:S7059` | `frontend/src/app/auth/login-page.ts:21` | RELIABILITY:HIGH, MAINTAINABILITY:HIGH | same |
| `external_roslyn:ASP0025` | `backend/src/StoreIt.Api/AuthenticationSetup.cs:107` | MAINTAINABILITY:MEDIUM | `AddAuthorization(…)` → `AddAuthorizationBuilder().SetFallbackPolicy(…)` |
| `docker:S7031` | `backend/Dockerfile:37` | MAINTAINABILITY:LOW | the two consecutive root-only `RUN`s merged into one layer |
| `typescript:S5906` | `frontend/src/app/auth/login-page.spec.ts:77` | MAINTAINABILITY:LOW | `expect(buttons.length).toBe(2)` → `expect(buttons).toHaveLength(2)` |
| `csharpsquid:S125` | `backend/src/StoreIt.Application/StorageUseCases.cs:57` | MAINTAINABILITY:MEDIUM | **false positive** — see below |

### The two `S7059` are the only ones with substance

Both components started an async operation in the constructor (`void this.auth.initCsrf()`,
`void this.skipWhenAlreadySignedIn()`). The constructor is for dependency injection; an async
call started there escapes Angular's error handling and runs before the component is fully
initialised. Moved to `ngOnInit`, which still runs before the template is rendered — so the
language is resolved by first paint and the sign-in redirect is unchanged in effect.

In `app.ts` the synchronous `language.init()` moved along with it. Splitting startup across
constructor and lifecycle hook would have been worse to read than either arrangement, and it
is one block of startup work.

### `S125` is a false positive, and the fix is a wording change

The flagged line was prose, not code:

```csharp
// Endpoints require authentication (fallback policy), so UserId is present here;
```

A parenthesis plus a trailing semicolon is enough for the commented-out-code heuristic to read
it as a statement. It was reported twice — in SonarCloud *and* as a build warning, since
SonarAnalyzer runs at build time (#78).

The honest resolution is to mark it **False Positive** in SonarCloud, which needs "Administer
Issues" in the UI and could not be done from here. Instead the sentence is rephrased to avoid
the shape, with a short `NOTE` so nobody "improves" it back. **This is worth a second opinion:
if you would rather keep the original wording and mark the issue in SonarCloud, revert that
hunk — the comment was not wrong, only unluckily punctuated.**

## Verification

| Check | Result |
|-------|--------|
| Frontend tests | **99 passed**, 12 files — including the `App` and `LoginPage` specs that exercise the moved startup work |
| Frontend typecheck (`tsc --noEmit`) | clean |
| Frontend lint (`ng lint`) | all files pass |
| Frontend format (Prettier) | clean |
| Backend tests | **153 passed, 0 failed** (Domain 62, Architecture 9, Api.Service 82); coverage 94.24% line / 71.75% branch |
| Backend format (CSharpier) | 70 files checked, clean |
| `S125` build warning | **gone** — `dotnet build` went from 3 warnings to 2 |
| Sonar re-analysis | **0 open issues** on both projects for PR 92 (`api/issues/search?...&pullRequest=92&resolved=false`), both quality gates passed |
| NU1903 build warning | **gone** — `dotnet build` reports 0 warnings after the Testcontainers bump below |
| Backend tests after the bump | **153 passed, 0 failed** (Domain 62, Architecture 9, Api.Service 82); coverage unchanged at 94.24% line / 71.75% branch |

## Found on the way — NU1903, now fixed here too

`dotnet build` reported **NU1903: SSH.NET 2025.1.0 has a known high-severity vulnerability**
([GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284) — `ScpClient` recursive
download allows arbitrary file write via server-controlled filenames; patched in SSH.NET
2026.0.0). The exact path, read off the package graph rather than guessed:
`Testcontainers.PostgreSql` 4.13.0 → `Testcontainers` 4.13.0 → `SSH.NET` 2025.1.0. The reference
sits in the core `Testcontainers` package, not in the PostgreSQL module, and only
`StoreIt.Api.Service.Tests` pulls it — production publishes `src/StoreIt.Api` alone.

Test-only and not shipped, so it was first left out of scope — but note *why* it was sitting here
at all: Renovate's `vulnerabilityAlerts` defaults would have opened a PR immediately, bypassing
schedule, approval and concurrency limits. Renovate has not run against a correct config since
2026-07-13 (see `2026-08-18-renovate-config-source-of-truth.md`). This is the first concrete cost
of that config drift.

Rather than carry it as an issue or an accepted risk, it is fixed in this PR:
`Testcontainers.PostgreSql` **4.14.0** pulls `Testcontainers` 4.14.0, which depends on `SSH.NET`
2026.0.0. One line in `backend/Directory.Packages.props`; the 153 backend tests still pass and
`dotnet build` is down to 0 warnings.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | *"Warum ist es möglich, dass sonar cube 2 reliability und zweimal 3 Maintainability Vergehen hat?"* — explicitly discussion only, no changes | Established the impact-vs-legacy-rating explanation above |
| 2 | *"zurück auf die sonar cube findings"* | Trigger to actually clear them |
| 3 | Automated review (CodeRabbit) raised 5 findings | Three markup/wording fixes taken; the `curl` pin rebutted; NU1903 escalated from "not fixed here" to fixed. The finding that the verbatim quotes in this table be replaced with synthetic text was rebutted: the Data & Compliance rule targets real or personal data, and quoting the orchestrator verbatim *is* the accountability record (KAIFe Principle 3) — the practice is repo-wide, see the container-stack and Renovate logs |
| 4 | Chose the `# hadolint ignore=DL3008` pragma with a written rationale over pinning `curl=<version>` | An exact apt pin breaks the build the moment Debian drops the superseded build from the mirror, and freezes the package that most needs patches |
| 5 | Chose to bump `Testcontainers.PostgreSql` in this PR over a tech-debt issue or an accepted-risk expiry | The patched version already existed; a one-line fix beats paperwork for a high-severity advisory, even a test-only one |

## Outcome

- **Result:** ready for review
- **Deviations from spec:** n/a (no spec)
- **Harness follow-up:** none. Worth knowing for future Sonar work: the displayed issue count and
  the gate's rating conditions come from two different models, so "green gate" and "open findings"
  are not a contradiction.
