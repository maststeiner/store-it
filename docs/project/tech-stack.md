# Tech Stack — store-it

> **Single source of truth for the technology stack.** Other documents (CLAUDE.md,
> README, guidelines, architecture doc) link here instead of restating stack facts.
> Required by the process interface: `docs/process/PROJECT-INTERFACE.md`.

## Languages & Frameworks

| Layer | Technology | Notes |
|-------|------------|-------|
| Backend | .NET 10 LTS (C#), API-first REST | LTS support until Nov 2028; layering per [ADR-001](../architecture/ADR-001-monorepo-layering.md) |
| Frontend | Angular (TypeScript) | Node 22 LTS toolchain; thin client, server owns the rules ([ADR-002](../architecture/ADR-002-api-first.md)) |
| Mobile | iPhone app (planned) | Consumes the same API |
| Persistence | PostgreSQL via EF Core | [ADR-003](../architecture/ADR-003-persistence.md) |
| Auth | OIDC federation (Google/Microsoft), BFF session | [ADR-004](../architecture/ADR-004-identity-auth.md) |
| Runtime | Containers (multi-arch images on GHCR), one VM with Docker Compose; Kubernetes deferred | Twelve-factor principles; deployment lives in the separate repository `store-it-deploy` — [ADR-005](../architecture/ADR-005-hosting-deployment.md), contract in [`docs/operations/runtime-contract.md`](../operations/runtime-contract.md) |
| DevOps | GitHub + GitHub Actions | CI carries the DoD gates (`.github/workflows/ci.yml`) |
| AI orchestration | Claude Code | AI-Dev Process harness, see `docs/process/PROCESS.md` |

## Formatting & Static Analysis

| Concern | Backend | Frontend |
|---------|---------|----------|
| Formatter | CSharpier (`dotnet csharpier format .`) | Prettier (`npx prettier --write .`) |
| Linting / analyzers | Roslyn analyzers `latest-recommended`, warnings = errors; `SonarAnalyzer.CSharp` solution-wide (pinned to SonarCloud's version) | ESLint |
| Cloud analysis | SonarCloud (`maststeiner_store-it-backend`) | SonarCloud (`maststeiner_store-it-frontend`) |

`.editorconfig` is binding for both sides.

## Test Frameworks

| Kind | Backend | Frontend |
|------|---------|----------|
| Unit / service / integration | xUnit + coverlet; Testcontainers (PostgreSQL; local house standard: Podman) | Angular default setup (vitest, `vitest-base.config.ts`) |
| End-to-end | — | Playwright (`frontend/e2e/`), full stack |
| Mutation testing | Stryker.NET (CI job `1a`) | Consciously dropped (see [test-guidelines](../guidelines/test-guidelines.md)) |

Gate thresholds (coverage, mutation score) are defined in
[`docs/guidelines/test-guidelines.md`](../guidelines/test-guidelines.md) — not here.

## Repository Layout

| Path | Content |
|------|---------|
| `backend/` | .NET solution: `StoreIt.Api` · `StoreIt.Application` · `StoreIt.Domain` · `StoreIt.Infrastructure` + `tests/`, committed OpenAPI contract (`backend/openapi/`) |
| `frontend/` | Angular app + Playwright E2E |
