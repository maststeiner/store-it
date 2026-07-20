# Test Guidelines

> **Owner:** Marcel Steiner (Architecture / QA Stewardship)
> **Stack:** xUnit + coverlet (backend) · Angular default test setup (frontend)
> **Last updated:** 2026-07-09

---

## Core Principle

Tests verify **required behavior** (from acceptance criteria), not the implemented code. The QA Agent reads the spec — not the implementation — before writing tests.

## Structure

- **Unit tests:** isolated, no external dependencies, fast. Use mocks/stubs for dependencies.
- **Service tests (Contract/API):** verify the service contract from the outside — endpoints, response shapes, status codes, error contracts. Use an in-process test host or equivalent; no mocking of internal layers. Clearly marked (e.g. `*.Service.Tests`).
- **Integration tests:** full stack including external resources (DB, message bus). Clearly marked (e.g. `*.Integration.Tests`).
- **E2E tests (Playwright):** drive the real UI against the live backend + PostgreSQL (`frontend/e2e/`). Cover core user flows only — expensive, so kept few and meaningful. Each test creates its own uniquely-named data so runs are independent.
- **Arrange-Act-Assert:** always, no logic inside the test.

### Running E2E locally (Podman)

```bash
podman compose up -d                                   # PostgreSQL
cd backend && ConnectionStrings__storeit="Host=localhost;Port=5432;Database=storeit;Username=storeit;Password=storeit" \
  dotnet ef database update --project src/StoreIt.Infrastructure --startup-project src/StoreIt.Api
ConnectionStrings__storeit="Host=localhost;Port=5432;Database=storeit;Username=storeit;Password=storeit" \
  dotnet run --project src/StoreIt.Api --no-launch-profile --urls http://localhost:5000 &
cd ../frontend && npx playwright install chromium && npm run e2e   # Playwright starts ng serve itself
```

CI (`1b · End-to-end`) uses a postgres service container and the runner's browser deps — no Podman needed there.

## Naming

```
[Method/Feature]_[Scenario]_[ExpectedResult]
// e.g.: CreateUser_WhenEmailInvalid_ThrowsValidationException
```

## Coverage

- Target: **≥ 70%** (calibrate during pilot, then fix as pipeline gate).
- Coverage is a means, not a goal: 100% meaningless tests do not beat 70% good ones.

## Containers for Tests (Podman)

Service/integration tests run against real PostgreSQL via Testcontainers. Locally the
house standard is **Podman** (not Docker) — point Testcontainers at the Podman socket:

```bash
podman machine start   # once
export DOCKER_HOST=unix://$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}')
export TESTCONTAINERS_RYUK_DISABLED=true   # ryuk needs a privileged docker daemon
dotnet test
```

CI (GitHub Actions ubuntu runner) provides a Docker daemon — no configuration needed there.

## Stop Condition

Failing tests block the Developer Agent. No feature is done before tests are green.

## TODO

<!-- Customize per project -->
- [ ] Fix coverage threshold after pilot experience
- [ ] Define test data strategy (fixtures, builder pattern, etc.)
- [x] E2E tests (Playwright) added with SPEC-001 — pyramid is now unit → service → integration → E2E; CI job `1b · End-to-end` runs the full stack (done 2026-07-20)
- [ ] Frontend mutation testing (StrykerJS) — deferred to a follow-up (decision 2026-07-20)
