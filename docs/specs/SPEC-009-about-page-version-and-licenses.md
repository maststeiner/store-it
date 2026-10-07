# Spec: About page — version, license and third-party notices

> **Status:** Frozen (Gate 1) — approved by Marcel Steiner, 2026-10-07 (issue #208)
> **Sprint:** 2026-S41
> **Author:** Claude Fable 5.1 (analyst/developer agent), from Marcel Steiner's request (issue #208, 2026-10-07)
> **Last updated:** 2026-10-07

---

## User Story

As a **user of store-it** I want **an About page in the account menu that shows the version I am
using, the license store-it is published under and every third-party framework and library it
ships with, each with its license** so that **the attribution those licenses require is visible in
the product and anybody — me, the operator, a curious visitor — can see what is running.**

---

## Context & Relationship to Existing Work

- **The account menu is the agreed growth point** for account-level entries (#86 run log): it
  holds the identity, *Statistics* for the operator (SPEC-008), *Sign out* and the destructive
  *Delete account* (SPEC-006). *About* is a navigation entry like *Statistics*, not an action.
- **The running software does not know its version today.** ADR-007 decision 4 names three
  version concepts; the **product release version** is the git tag `vMAJOR.MINOR.PATCH`
  (`v0.3.0` on 2026-10-06). The release workflow writes that tag only into the OCI labels of the
  images — neither the Angular bundle nor the API carries it. The web and the API must therefore
  be **stamped at build time**; nothing can be read from the tag at runtime.
- **The web build already extracts the licenses of what it bundles.** The Angular production
  build (`extractLicenses`, on by default) writes `dist/frontend/3rdpartylicenses.txt` — today
  seven packages: `@angular/{core,common,platform-browser,router,forms}`, `rxjs`, `tslib`, each
  with the full license text. The file sits **outside** `browser/` and is not copied into the
  web image, so it is produced and thrown away. It is the authoritative list of what is actually
  shipped to the browser: `npm ls --omit=dev` would also name `zod` and `@standard-schema/spec`,
  which are resolved but never bundled.
- **The API runtime image carries about fifteen NuGet packages**, all Microsoft (MIT) except
  `Npgsql` / `Npgsql.EntityFrameworkCore.PostgreSQL` (PostgreSQL License). Design-time packages
  (`Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.ApiDescription.Server`,
  `SonarAnalyzer.CSharp`) are `PrivateAssets=all` and are **not** published. The exact published
  set is in the publish output's `StoreIt.Api.deps.json`; the license expression of each package
  is in its `.nuspec` in the restore cache.
- **License compliance is already gated** (`docs/SETUP.md` §3): store-it is MIT; dependencies
  must carry a permissive license from the allow-list (0BSD, MIT, Apache-2.0, BSD-2/3-Clause,
  ISC), enforced by Trivy and `dependency-review-action`; a CycloneDX SBOM is produced per CI
  run. The About page is **attribution and transparency** on top of that gate, not a replacement
  for it — and it must not become a second, hand-maintained copy of the dependency list.
- **`/privacy` is not part of the application**: the deployment serves it as a static page
  (Caddy, `store-it-deploy`). The About page therefore does not depend on it (see Out of Scope).
- The web client is the only client (SPEC-002); the new endpoint is additive, so the ADR-007
  contract gate sees no breaking change.

## Chosen Approach (summary)

Both images are stamped with the release tag at build time. The web shows its own version and
asks the API for its version and its third-party notices through one new endpoint. Every list of
third-party components is **generated from the build artefacts** — Angular's license extraction
for the web, the published dependency manifest for the API — so the page can never drift from
what is shipped. A tiny static list covers the platform components that are neither npm nor NuGet
(.NET runtime, nginx, PostgreSQL).

### Decisions taken (confirmed at G1, 2026-10-07)

| # | Decision | Rationale / alternative |
|---|----------|-------------------------|
| D1 | **Version = the release tag** (`vMAJOR.MINOR.PATCH`, ADR-007 decision 4), passed by `release.yml` as build args `VERSION=${{ github.ref_name }}` and `REVISION=${{ github.sha }}` to the backend and web image builds. Builds without the args (CI job 1c, local `docker build`, `ng serve`, `dotnet run`) report **`dev`** and no revision. | The tag is the one source of truth for "what is released"; the same value already feeds the OCI labels, so the page and `imagetools inspect` cannot disagree. Alternative — a version field in `package.json` / `.csproj` bumped per release — would be a second place to maintain and could drift from the tag. |
| D2 | **Web and API each report their own version**; the page shows both and a notice when they differ. The API reports through a new endpoint `GET /api/v1/about` (operationId `getAbout`, tag `About`, `200 AboutResponse { version, revision, runtime, components[] }`). | The runtime contract says *run `backend` and `web` from the same tag*; the page makes a violation visible without a shell. Alternative — web version only, no backend change — is cheaper but leaves the API's third-party notices (D3) without a carrier. |
| D3 | **Third-party notices are generated at build time, never committed.** Web: a post-build step (`node scripts/third-party-notices.mjs`, no new dependency) converts Angular's `3rdpartylicenses.txt` into `browser/assets/third-party-notices.json`, adding version and homepage from each package's `package.json`. API: a post-publish step (a .NET 10 file-based C# script in `backend/scripts/`, run in the Dockerfile) reads `StoreIt.Api.deps.json` and the `.nuspec` files of the restore cache and writes `third-party-notices.json` next to the binaries; the endpoint serves it. **Either generator fails the build when a package carries no license information.** | A committed list would go stale with every Renovate bump — either a drift check fails every dependency PR or the list is silently wrong. Generating from the artefacts lists exactly what ships, including transitive packages, and the fail-on-unknown rule keeps the existing license policy honest at the attribution level too. Alternative — hand-maintained Markdown in `docs/` — rejected for the drift reason. |
| D4 | **Scope of the list = what is shipped.** Three groups: *Web* (bundled npm packages), *API* (published NuGet packages), *Platform* (static: .NET runtime, nginx, PostgreSQL — name, license, link; the .NET runtime version comes from the API at runtime, the other two show no version). **Not listed:** dev/test tooling (ESLint, Prettier, vitest, Playwright, xUnit, Stryker, Testcontainers, …) because it is never distributed, and the OS packages of the base images, which the CI SBOM artifact covers. | Attribution duties attach to what is distributed. Listing hundreds of Debian/Alpine packages would bury the entries a reader actually looks for. |
| D5 | **Behind the session like every page**: route `/about` with `authGuard`; the endpoint uses the default authenticated policy (`401` without session). The generated web notices file is a static asset and is, by nature, fetchable without a session. | Secure by default (SPEC-003); an anonymous version endpoint would hand version fingerprints to anyone. The attribution duty is met by shipping the notices with the bundle, which the static file does regardless. Alternative — public page and endpoint — if Marcel wants licenses visible before sign-in. |
| D6 | **Presentation**: one page, three sections — *store-it* (version web / API, short revision, link to the GitHub release when it is a release build), *License* (MIT, copyright line, full text, link to `LICENSE`), *Third-party software* (groups per D4; each entry: name, version, license id, copyright holder where known, link; the full license text expandable with `<details>`). Labels are translated; license texts, package names and license identifiers are not. | License texts are legal text and stay in their original language; everything a user reads as UI copy is in the four locales as elsewhere. |

---

## Acceptance Criteria (EARS Notation)

### Navigation & access

- [ ] AC-01: WHEN a signed-in user opens the account menu THE client SHALL show a menu item
      *About* (`about.menu`), placed after the identity block and the operator's *Statistics*
      entry and before *Sign out*, that navigates to `/about`.
- [ ] AC-02: WHEN `/about` is opened without a session THE client SHALL redirect to the sign-in
      page and return to `/about` after sign-in (existing `authGuard` behaviour).

### Version

- [ ] AC-03: WHEN the About page loads THE client SHALL show the version of the web build:
      the release tag (e.g. `v0.3.0`) for an image built by the release workflow, `dev` otherwise.
- [ ] AC-04: WHEN a signed-in user calls `GET /api/v1/about` THE system SHALL answer `200` with
      `AboutResponse` carrying `version` (release tag or `dev`), `revision` (commit SHA or
      `null`), `runtime` (the .NET runtime description) and `components` (AC-09); WHEN called
      without a session THE system SHALL answer `401`.
- [ ] AC-05: WHEN the web version and the API version differ THE client SHALL show a notice
      line (`about.versionMismatch`) next to the versions; WHEN they are equal THE notice SHALL
      not be rendered.
- [ ] AC-06: WHEN the release workflow builds the images for a tag THE images SHALL report that
      tag and the commit SHA (web: build define; API: assembly informational version); WHEN the
      images are built without the build args THE images SHALL report `dev`. *(Human G3
      verification on the next release; the stamping path is covered by unit tests, see
      Verification.)*

### License of the application

- [ ] AC-07: THE About page SHALL show the application's license: the identifier *MIT License*,
      the copyright line from `LICENSE` (`Copyright (c) 2026 Marcel Steiner`), the full license
      text and a link to the `LICENSE` file in the repository.

### Third-party notices

- [ ] AC-08: WHEN the web production build runs THE build SHALL produce
      `assets/third-party-notices.json` with one entry per package in Angular's license
      extraction — `name`, `version`, `license` (SPDX identifier), `url`, `text` — and SHALL fail
      when a package has no license information.
- [ ] AC-09: WHEN the API is published THE publish step SHALL produce `third-party-notices.json`
      with one entry per NuGet package in `StoreIt.Api.deps.json` — `name`, `version`, `license`
      (SPDX expression), `copyright` (when the package states one), `url` — and SHALL fail when a
      package has no license information; THE endpoint of AC-04 SHALL return these entries as
      `components`.
- [ ] AC-10: WHEN the About page loads THE client SHALL list the components in three groups —
      *Web*, *API*, *Platform* — each entry with name, version (where known), license identifier
      and link, and the license text expandable where available; THE *Platform* group SHALL
      contain .NET runtime (version from `runtime` of AC-04), nginx and PostgreSQL.
- [ ] AC-11: WHEN the API call fails or the web notices file is missing (development build)
      THE page SHALL still render the web version, the application license and whatever
      components are available, and SHALL show the existing error message or a hint
      (`about.notAvailableInDev`) in place of the missing group — never a blank page.

### Contract & i18n

- [ ] AC-12: THE OpenAPI contract SHALL contain `getAbout` under a new tag `About`, and the
      breaking-change gate SHALL report no breaking change (additive).
- [ ] AC-13: All new user-facing strings SHALL exist in de / en / fr / it (i18n completeness
      test); component names, license identifiers and license texts are not translated.

---

## Edge Cases

- EC-01: **Development build** (`ng serve`, `dotnet run`, CI job 1c): version `dev`, revision
  `null`, no mismatch notice (both say `dev`); the web notices file does not exist under
  `ng serve` → AC-11 hint. The API notices file does not exist without the publish step → the
  endpoint returns an empty `components` array, the page shows the hint for the *API* group.
- EC-02: **Web and API from different tags** (deployment broke the "same tag" rule): both
  versions shown, mismatch notice visible. The page does not try to decide which one is right.
- EC-03: **Compound license expressions** (`MIT OR Apache-2.0`): shown verbatim as the SPDX
  expression; no attempt to pick one.
- EC-04: **Legacy NuGet packages with `licenseUrl` only** (no `<license>` element): the
  generator records the URL as `license` and `url` so the entry stays attributable; it fails
  only when neither exists.
- EC-05: **Transitive packages** (e.g. `Microsoft.IdentityModel.*` behind the OpenID Connect
  handler): listed like direct ones — they are shipped. Grouping by "direct vs. transitive" is
  not shown; it means nothing to the reader.
- EC-06: **Package without a copyright statement** in its metadata: the `copyright` field is
  omitted and the entry shows name, version, license and link only.
- EC-07: **Session expired while on the page**: the API call answers `401`, handled by the
  existing interceptor like everywhere else; the web part of the page remains rendered.
- EC-08: **Very long license texts** (Apache-2.0 is ~10 kB): collapsed by default
  (`<details>`), so the page stays scannable; no truncation.

---

## UI Requirements (web)

- Route `/about` (lazy-loaded page), guarded by `authGuard`.
- Session menu: `menuitem` *About* (`about.menu`), after *Statistics* (when rendered) and before
  *Sign out*; same `.session-menu-item` styling, not destructive.
- Page: title `about.title`; section *store-it* with `about.version.web`, `about.version.api`,
  `about.revision`, optional `about.versionMismatch` notice (`role="status"`), link to the GitHub
  release for a release build; section *License* (`about.license.title`) with the MIT text in a
  monospace block; section *Third-party software* (`about.thirdParty.title`) with group headings
  `about.thirdParty.web` / `.api` / `.platform` and one `<details>` per entry where a text exists.
- Loading and error states via the existing patterns (`role="status"`, API error interceptor).
  No new dependency, no markdown renderer.

---

## Out of Scope

- **Legal links on the sign-in page** (About / privacy / imprint footer) — a separate, small
  item once the page exists; today nothing links `/privacy` from the app either.
- **Linking `/privacy` from the About page**: the page exists only in the production deployment
  (Caddy static file), not in the local stack; linking it needs a deployment-aware switch.
- **Imprint / contact data**, **changelog or release notes in the app**, **update check**.
- **OS-level packages of the base images** (Debian, Alpine): covered by the CI SBOM artifact.
- **Translating license texts.**
- **A hand-maintained `THIRD-PARTY-NOTICES.md` in the repository** — rejected (D3).

---

## Technical Constraints (from Architect Agent)

<!-- Proposal; confirmed/adjusted after G1 -->

- [ ] Layering: `AboutEndpoints.cs` in `StoreIt.Api` reads version data from the entry
      assembly (`AssemblyInformationalVersionAttribute`) and the notices JSON from the content
      root through an `IAboutInformation` service registered in Api; no Application or Domain
      change, no persistence, no EF (ADR-001; architecture tests stay green).
- [ ] Version stamping: backend `dotnet publish -p:Version=<tag without v> -p:InformationalVersion=<tag>+<sha>`
      with `IncludeSourceRevisionInInformationalVersion=false` (otherwise the SDK appends a
      second `+sha`); web `ng build --define STOREIT_VERSION='"<tag>"' --define STOREIT_REVISION='"<sha>"'`
      with `dev` / `null` defaults in `angular.json`; `release.yml` passes both build args to
      the backend and web `docker/build-push-action` steps; CI job 1c builds without args.
- [ ] Generators: `frontend/scripts/third-party-notices.mjs` (Node, no dependency; parses the
      `Package:` / `License:` blocks of `3rdpartylicenses.txt`, reads
      `node_modules/<name>/package.json` for `version` and `homepage`/`repository`), wired as
      `npm run build` → `ng build && node scripts/third-party-notices.mjs`;
      `backend/scripts/ThirdPartyNotices.cs` (.NET 10 file-based app, `dotnet run
      scripts/ThirdPartyNotices.cs -- <deps.json> <nuget cache> <out>`), run in the Dockerfile
      after `dotnet publish`. Both fail with a non-zero exit on missing license information.
- [ ] Contract: `backend/openapi/StoreIt.Api.json` regenerated (`getAbout`, tag `About`,
      `AboutResponse`, `ThirdPartyComponentResponse`), web client regenerated
      (`src/app/api/fn/about`, `AboutService`), both committed.
- [ ] Tests: backend — `AboutEndpointsTests` (401/200, version parsing from informational
      version incl. `dev` fallback, notices file present / absent / malformed), a unit test for
      the notices generator against fixture `deps.json` + `.nuspec` files (incl. EC-03/04 and
      the fail-on-missing rule), `OpenApiContractTests`; web — `session-menu.spec.ts` (SPEC-009
      block), `about-page.spec.ts` (versions, mismatch notice, three groups, hint when the file
      is missing, API error keeps the page), a unit test for the Node generator against a fixture
      `3rdpartylicenses.txt`, `i18n.spec.ts`.
- [ ] Docs: `docs/operations/runtime-contract.md` (§1: images carry the version in the app, not
      only in labels; `GET /api/v1/about` behind the session), `README`/`SETUP.md` note that
      the notices are generated, not committed.
- [ ] Dependencies: none new.
- [ ] ADR required: no (uses ADR-007's version concept; no structural decision).

---

## Verification

<!-- Filled in by QA Agent -->

| AC | Test | Status |
|----|------|--------|
| AC-01 | `session-menu.spec.ts` → SPEC-009 block | ⬜ |
| AC-02 | existing `auth.guard.spec.ts` (route-agnostic) + `about-page.spec.ts` route config | ⬜ |
| AC-03 | `about-page.spec.ts` (define value rendered; `dev` default) | ⬜ |
| AC-04 | `AboutEndpointsTests` | ⬜ |
| AC-05 | `about-page.spec.ts` (notice shown / hidden) | ⬜ |
| AC-06 | unit tests of the stamping path (version parsing, define default) · **human G3 check on the next release** | ⬜ |
| AC-07 | `about-page.spec.ts` (MIT text, copyright line, link) | ⬜ |
| AC-08 | generator unit test (fixture `3rdpartylicenses.txt`, fail-on-missing) · CI job 1c builds the web image | ⬜ |
| AC-09 | generator unit test (fixture `deps.json` + `.nuspec`) · `AboutEndpointsTests` (components passthrough) | ⬜ |
| AC-10 | `about-page.spec.ts` (three groups, entry fields, `<details>`) | ⬜ |
| AC-11 | `about-page.spec.ts` (API error → page still rendered; 404 notices → hint) | ⬜ |
| AC-12 | `OpenApiContractTests` (`getAbout`), CI job *API contract gate* (additive) | ⬜ |
| AC-13 | `i18n.spec.ts` (de/en/fr/it key parity) | ⬜ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ | 2026-10-07 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
