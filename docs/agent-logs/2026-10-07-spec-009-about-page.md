# Agent Run Log: SPEC-009 About page — version, license, third-party notices

> **Date:** 2026-10-07
> **Spec:** [docs/specs/SPEC-009-about-page-version-and-licenses.md](../specs/SPEC-009-about-page-version-and-licenses.md) — frozen at G1 the same day (Marcel Steiner, "ja, passt so")
> **Persona(s):** analyst → developer (spec drafted in the same session, frozen by Marcel before any code)
> **Model:** Claude Fable 5.1 (claude-fable-5-1)
> **Branch / PR:** `feature/about-page` → PR (issue #208)

---

## Task

An About page in the account menu: which version is running, under which license store-it is
published, and every third-party framework and library it ships with, each with its license.

## Plan

1. Analysis: how the app could know its version (it cannot today), what license information the
   builds already produce, what the runtime images actually ship.
2. Spec draft with six decisions, G1 by Marcel.
3. Backend: version stamp + notices generator + `GET /api/v1/about`; contract and typed client.
4. Web: `BUILD_INFO` token, menu entry, lazy `/about` page, post-build notices script, i18n.
5. Dockerfiles + release workflow build args; nginx cache rule; docs; local image builds as proof.

## Findings that shaped the design

- **The running software did not know its version.** ADR-007 makes the git tag the product
  version; `release.yml` wrote it into OCI labels only. Both images now receive `VERSION` and
  `REVISION` as build args and stamp them — the API into `AssemblyInformationalVersion`, the web
  bundle through `ng build --define`. Everything built without the args says `dev`.
  **Trap found by the first stamped image build:** Docker exposes build args as environment
  variables, and MSBuild treats every environment variable as a property — `VERSION=v9.9.9`
  became `$(Version)` and every project failed with `NETSDK1018` (not a NuGet version). I first
  misread this as the SDK validating `InformationalVersion` and switched to a custom
  `AssemblyMetadata` attribute; the second build failed identically, which exposed the real cause.
  Reverted to the informational version the spec names; the Dockerfile now unsets the two
  variables before `dotnet publish` runs.
- **Angular already extracts the licenses of what it bundles** (`extractLicenses`, default on in
  production) — into `dist/frontend/3rdpartylicenses.txt`, *outside* `browser/`, so the web image
  never shipped it. The extraction is also the only honest list: `npm ls --omit=dev` names `zod`
  and `@standard-schema/spec`, which are resolved but never bundled.
- **The API runtime image ships ~15 NuGet packages**, all MIT except Npgsql (PostgreSQL License).
  The publish output's `StoreIt.Api.deps.json` is the exact list; each package's `.nuspec` in the
  restore cache carries the SPDX expression, copyright and project URL.

## Key Decisions

- **Followed the frozen spec (D1–D6). Three "how" details differ from the pre-G1 proposal in the
  Technical Constraints section and are marked ⚠ there:**
  - **The API notices generator is a command mode of the API binary**
    (`dotnet StoreIt.Api.dll third-party-notices <deps.json> <out>`), not a separate .NET
    file-based script. Reason: one artefact to build in the Dockerfile, the logic is unit-tested
    from the existing test project (`ThirdPartyNoticesCommandTests`, synthetic manifest + nuspec
    cache), and `Program.cs` intercepts the arguments before the web host — nothing of the
    hosting pipeline runs. The precedent is the usual `--migrate`-style admin command.
  - **`ThirdPartyComponentResponse` has an optional `text`** (packages that ship a license file
    instead of an SPDX expression get their text embedded; the web entries carry Angular's
    extracted texts). The page shows texts collapsed in `<details>` (EC-08).
  - **No `angular.json` defaults for the defines.** `build-info.ts` guards with `typeof`; an
    undefined global *is* the development build. Unit tests therefore see `dev` without any
    config, and the page spec overrides the `BUILD_INFO` token.
- **Platform group is a static constant in the page** (.NET runtime, nginx, PostgreSQL — D4). Only
  the .NET version is known (from the API's `runtime`); nginx and PostgreSQL are pinned by image
  tags the page cannot see and are shown without a version. Documented in the spec, not hidden.
- **nginx `no-cache` rule widened** from `assets/i18n/*.json` to `assets/**/*.json`: the notices
  file is a runtime-loaded JSON without a content hash, exactly the #182/#184 cache case.
- **Mismatch notice is `role="status"`, not an alert**: an operator hint, not a user error.

## Verification

| Check | Result |
|---|---|
| `dotnet build` (Release) · CSharpier | clean |
| `dotnet test` (Domain, Architecture, Api.Service incl. Testcontainers) | 238 passed |
| `ng test` | 186 passed / 21 files |
| `ng lint` · `prettier --check .` | clean |
| `ng build --configuration production --define …` | stamp present in the lazy `about-page` chunk; `postbuild` wrote 7 components |
| `docker build frontend --build-arg VERSION=v9.9.9 REVISION=…` | `assets/third-party-notices.json` (18.8 kB) in the image, stamp in the chunk |
| `docker build backend --target runtime --build-arg …` | see the PR description for the generated API notices (real nuspec data) |
| OpenAPI contract | additive: `getAbout`, tag `About`, two schemas; typed client regenerated |

**Not verified visually in a browser**: the sandbox has no signed-in session against a real IdP.
The rendered page (layout, `<details>`, the mismatch notice) needs a human look — part of the
G3 test, like the About page on prod showing the real tag after the next release (AC-06).

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Froze the spec with all six proposed decisions unchanged ("ja, passt so") | G1 |

## Outcome

- **Result:** PR open, awaiting G2/G3
- **Deviations from spec:** none in scope or acceptance criteria; the implementation details that
  differ from the pre-G1 *proposal* are marked ⚠ in the spec's Technical Constraints
- **Harness follow-up:** none. Observation worth keeping: the Angular license extraction has
  been produced and discarded since the first production build — a build artefact nobody looked
  at. Not a guideline change; a single occurrence.
