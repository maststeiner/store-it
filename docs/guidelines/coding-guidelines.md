# Coding Guidelines

> **Owner:** Marcel Steiner (Architecture Stewardship)
> **Stack:** .NET 10 LTS (C#) backend · Angular (TypeScript) frontend
> **Formatter:** CSharpier (backend, `dotnet csharpier format .`) · Prettier + ESLint (frontend)
> **Static analysis:** Roslyn analyzers, `latest-recommended`, enforced at build time (warnings = errors)
> **Last updated:** 2026-07-29

These guidelines are the primary reference for the Developer Agent and the Reviewer Agent.

---

## General

- **Simplicity over cleverness:** The simplest code that satisfies the spec. No speculative abstractions.
- **Naming:** Descriptive names; no abbreviations except established ones (e.g. `id`, `url`).
- **No magic numbers:** Name constants explicitly.
- **Run the project formatter** before every commit (backend: `dotnet csharpier format .` · frontend: `npx prettier --write .`). `.editorconfig` is binding.
- **Roslyn analyzer findings are build errors** — fix them, don't suppress. Suppressions require a justifying comment and reviewer approval (exception: CA1707 in test projects, mandated by the test naming convention).

## SOLID Principles

- **S — Single Responsibility:** Every class/module has exactly one reason to change.
- **O — Open/Closed:** Open for extension, closed for modification. Prefer composition and abstractions over editing existing classes.
- **L — Liskov Substitution:** Subtypes must be fully substitutable for their base types without altering correctness.
- **I — Interface Segregation:** Prefer small, focused interfaces over large general-purpose ones. No client should depend on methods it does not use.
- **D — Dependency Inversion:** Depend on abstractions, not on concrete implementations. Inject dependencies — do not instantiate them inside classes.

The Reviewer Agent checks for SOLID violations as part of every adversarial review.

## Clean Architecture (Robert C. Martin)

The backend layering (ADR-001) follows the Clean Architecture dependency rule:

- **Source dependencies point inward only:** Api / Infrastructure → Application → Domain. Nothing in Domain knows about outer layers.
- **Domain is framework-free:** no EF Core, ASP.NET, or other framework dependencies in Domain — plain C# entities and domain rules.
- **Use cases live in Application:** one use case per operation (add item, rename storage, …); Application defines the interfaces (ports) that Infrastructure implements (adapters).
- **Frameworks are details at the edges:** EF Core mapping via separate configuration classes (no persistence attributes on domain entities); ASP.NET concerns stay in Api.
- **Boundaries cross via DTOs:** domain entities do not leak through the API; request/response models live in Api.

These rules are enforced by the architecture conformance gate (CI) — violations block the merge.

## Structure & Layering

- Layering rules are defined in `docs/architecture/` as ADRs and enforced via the architecture conformance gate in CI.
- Circular dependencies are forbidden.
- Every new external dependency requires justification (ADR or PR comment).

## Twelve-Factor App

The backend targets Kubernetes and follows [the twelve factors](https://12factor.net/); the ones most relevant for day-to-day coding:

- **Config from the environment:** all config (connection strings, URLs, feature flags) via environment variables — never hard-coded, never in committed config files. Secrets never in the repo.
- **Backing services as attached resources:** PostgreSQL & co. addressed via config only — swappable without code change.
- **Stateless processes:** no in-process session state, no local file persistence; any instance can serve any request.
- **Logs as event stream:** structured logs to stdout — no log files, no in-app log routing (the platform handles it).
- **Port binding & disposability:** self-contained service, fast startup, graceful shutdown (k8s lifecycle).
- **Dev/prod parity:** local development runs against real PostgreSQL (container), not an in-memory substitute.
- **Admin processes:** DB migrations run as separate one-off processes (not implicitly at app startup).

## Error Handling

- Catch exceptions only at system boundaries (input, external APIs).
- Do not silently swallow internal errors.
- Use specific exception types instead of `catch (Exception e)`.

## Security (OWASP Basics)

- Validate all external input.
- No secrets or credentials in code or logs.
- SQL only via parameterized queries / ORM.
- No insecure deserialization.

## Commit Conventions

**Conventional Commits** (Angular style), enforced locally by the `commit-msg` git hook (`.githooks/commit-msg`; activate once per clone: `git config core.hooksPath .githooks` — deliberate decision against a CI gate, 2026-07-18):

```text
type(scope): subject
```

- **Types:** `feat` (new behavior) · `fix` (bug fix) · `docs` · `style` (formatting only) · `refactor` (no behavior change) · `perf` · `test` · `build` (dependencies/tooling) · `ci` · `chore` · `revert`
- **Scopes (suggested):** `backend`, `frontend`, `docs`, `ci`, `harness`, `deps` — omit for repo-wide changes
- **Subject:** imperative mood, lowercase, no trailing period, ≤ 100 chars
- **Body:** explains the *why*, wrapped at ~72 chars; reference specs/ADRs where relevant
- **Breaking changes:** `type(scope)!: subject` plus a `BREAKING CHANGE:` footer — for the API also subject to ADR-006 (new version required)

## Naming Conventions

Beyond the general rule (descriptive names, no abbreviations except established ones), the project follows these stack conventions — the Reviewer Agent flags deviations.

**Backend (C#)**

- **Types** PascalCase; **interfaces** `I`-prefixed (`IStorageRepository`). One *primary* public type per file (filename = type name) for entities, services and DTOs — **exception:** a feature's closely-related small types may be grouped in one file (e.g. `ItemUseCases.cs` holds the item use cases + their input records; `Contracts.cs` holds the boundary DTOs). In this small codebase, grouping by feature beats a sprawl of tiny files.
- **Role suffixes** are load-bearing and consistent: use cases `*UseCases` (`StorageUseCases`), endpoints `*Endpoints`, EF config `*Configuration`, DI wiring `*ServiceCollectionExtensions`, exceptions `*Exception` (`StorageNotFoundException`).
- **Use-case input models** bundling several parameters are named `<Operation>Input` (`AddItemInput`, `UpdateItemInput`) — **not** `*Command`. store-it has no command bus/mediator, so `Command` would overclaim CQRS semantics; these are parameter objects for a use case's `ExecuteAsync`. Introduce one once a signature grows past ~4 parameters; keep small signatures positional.
- **Domain stays noun-first and framework-free** (`Storage`, `Item`, `ExpiryRules`, `Unit`) — no technical suffixes leaking persistence/transport concerns.
- **Locals/parameters** camelCase; **constants** PascalCase; **async** methods end in `Async` only when a sync sibling exists.
- **EF migrations** keep the generated `<timestamp>_<Name>` form.

**Frontend (TypeScript / Angular)**

- **Files** kebab-case with a role suffix mirroring the symbol: routed pages `*-page` (`storage-list-page.ts` + co-located `.html`), dialogs `*-dialog`, services either `*.service.ts` or a role name (`storage-api.ts`, `translate.ts`), models `models.ts`.
- **Classes** PascalCase, **members/signals/inputs** camelCase; specs co-located as `*.spec.ts`.
- **No user-facing strings in components** — all text via i18n keys (see [test-guidelines] and ADR-002); the invariant brand wordmark ("store-it") is the sole documented exception.

## Project-Specific Rules

- **Server owns the rules:** the Angular client renders and delegates — no business logic, no status computation client-side (status is server-computed per ADR-002). A rule appearing in the UI is a review failure.
- **Fixed domain lists are constants, not literals:** the unit list and the "expiring soon" threshold (3 days) live as named domain constants, never inlined in the UI.
- **Cross boundaries via DTOs** (Api request/response models) — domain entities never serialize out directly.
