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
- **Arrange-Act-Assert:** always, no logic inside the test.

## Naming

```
[Method/Feature]_[Scenario]_[ExpectedResult]
// e.g.: CreateUser_WhenEmailInvalid_ThrowsValidationException
```

## Coverage

- Target: **≥ 70%** (calibrate during pilot, then fix as pipeline gate).
- Coverage is a means, not a goal: 100% meaningless tests do not beat 70% good ones.

## Stop Condition

Failing tests block the Developer Agent. No feature is done before tests are green.

## TODO

<!-- Customize per project -->
- [ ] Fix coverage threshold after pilot experience
- [ ] Define test data strategy (fixtures, builder pattern, etc.)
