# Test Guidelines

> **Owner:** Marcel Steiner (Architecture / QA Stewardship)
> **Stack:** xUnit + coverlet (backend) · Angular default test setup (frontend)
> **Last updated:** 2026-07-27

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

- Target: **≥ 70%**, fixed as the pipeline gate after the SPEC-001 pilot (see [Decisions & Calibration](#decisions--calibration)) — enforced by CI (backend coverlet · frontend `vitest-base.config.ts`).
- Coverage is a means, not a goal: 100% meaningless tests do not beat 70% good ones.

## Test Effectiveness

Coverage measures *how much* code runs under test, not whether the tests would *catch a regression*. store-it treats effectiveness as a first-class concern:

- **Mutation testing proves tests bite.** AI-generated tests often look right but assert nothing. Stryker.NET (backend, break < 60%) injects mutants and fails if the suite doesn't catch them. It runs as a **required CI gate on every PR** (job `1a`); scope it to business-critical modules to keep runtime sane. Moving it out of PR validation (e.g. to a nightly run) requires an explicit, documented exception — it must not silently become optional. (Frontend mutation testing is intentionally out — see Decisions & Calibration.)
- **Assertion minimum (anti-tautology).** Every test asserts observable behavior. No test without a real assertion; no assertion that is always true (e.g. `Assert.True(true)`, or asserting that a mock returns exactly what it was set up to return). A test that cannot fail is a defect.
- **Determinism.** No wall-clock or randomness in tests. Time is injected via `TimeProvider` (already used across the use cases) and controlled with `FakeTimeProvider` — essential for the expiry logic (`expired` / `expiring soon`). No `DateTime.Now`, no unseeded randomness, no shared mutable state between tests.
- **Flaky-test policy.** A non-deterministic test is quarantined immediately (skipped with a linked tracking issue), then root-caused — never "fixed" by a blind retry. Flakiness is a defect in the test or the code, not noise to tolerate.
- **Isolated QA.** The QA persona derives tests from the spec's acceptance criteria and never reads the implementation — this prevents self-confirming tests and has caught real bugs (see Core Principle).

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

## Test Data Strategy

- **Synthetic data only** — tests use exclusively synthetic data; real *and* anonymized production data are forbidden. Keeps tests reproducible and prevents PII from entering the codebase.
- **No data-generation library** (Bogus/Faker) — deliberate. Arrange blocks stay explicit so a reader sees exactly which values drive the case.
- **Domain / unit tests:** arrange inline, values chosen to make the scenario obvious (e.g. an expiry date `today + 2` for "expiring soon").
- **Service tests:** share `ApiTestFixture` (in-process API host + Testcontainers PostgreSQL); each test seeds only the data it asserts on and cleans up via the fixture lifecycle.
- **E2E tests:** each test creates its own uniquely-named data per run so parallel/repeat runs stay independent.
- Introduce a builder pattern only once arrange blocks visibly repeat across many tests — not preemptively (simplicity over cleverness).

## Decisions & Calibration

- **Coverage threshold confirmed at 70%** after the SPEC-001 pilot (2026-07-27). Actuals sit comfortably above (snapshot 2026-07-27: backend domain ~98%, frontend ~87% vitest line coverage; SonarCloud reports frontend ~84% as it counts template lines differently), so the gate catches regressions without being noise. Enforced as a pipeline gate (backend coverlet · frontend `vitest-base.config.ts`). Revisit only if a later spec shows it miscalibrated.
- **E2E tests (Playwright)** added with SPEC-001 (2026-07-20) — the pyramid is now unit → service → integration → E2E; CI job `1b · End-to-end` runs the full stack.
- **Frontend mutation testing (StrykerJS): consciously dropped** (2026-07-20). Weak value/effort ratio — the frontend is logic-thin (template + delegation, server-computed status per ADR-002), while the branch-heavy logic lives in the backend domain, already gated by Stryker.NET (60%). Revisit only if substantial client-side logic appears.
