# Coding Guidelines

> **Owner:** Architecture Stewardship
> **Stack:** TODO — defined per project (see `docs/SETUP.md`)
> **Last updated:** YYYY-MM-DD

These guidelines are the primary reference for the Developer Agent and the Reviewer Agent.

---

## General

- **Simplicity over cleverness:** The simplest code that satisfies the spec. No speculative abstractions.
- **Naming:** Descriptive names; no abbreviations except established ones (e.g. `id`, `url`).
- **No magic numbers:** Name constants explicitly.
- **Run the project formatter** before every commit. `.editorconfig` is binding.

## SOLID Principles

- **S — Single Responsibility:** Every class/module has exactly one reason to change.
- **O — Open/Closed:** Open for extension, closed for modification. Prefer composition and abstractions over editing existing classes.
- **L — Liskov Substitution:** Subtypes must be fully substitutable for their base types without altering correctness.
- **I — Interface Segregation:** Prefer small, focused interfaces over large general-purpose ones. No client should depend on methods it does not use.
- **D — Dependency Inversion:** Depend on abstractions, not on concrete implementations. Inject dependencies — do not instantiate them inside classes.

The Reviewer Agent checks for SOLID violations as part of every adversarial review.

## Structure & Layering

- Layering rules are defined in `docs/architecture/` as ADRs and enforced via the architecture conformance gate in CI.
- Circular dependencies are forbidden.
- Every new external dependency requires justification (ADR or PR comment).

## Error Handling

- Catch exceptions only at system boundaries (input, external APIs).
- Do not silently swallow internal errors.
- Use specific exception types instead of `catch (Exception e)`.

## Security (OWASP Basics)

- Validate all external input.
- No secrets or credentials in code or logs.
- SQL only via parameterized queries / ORM.
- No insecure deserialization.

## TODO

<!-- Customize per project -->
- [ ] Define naming conventions for this project
- [ ] Add further project-specific rules
