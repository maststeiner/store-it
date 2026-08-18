# Agent Log — SPEC-003 implementation (accounts & storage ownership)

> **Task:** Implement SPEC-003 (accounts & per-storage ownership; federated login via Microsoft/Google, BFF) per ADR-004 and the frozen plan.
> **Date:** 2026-08-03 – 2026-08-05
> **Orchestrated by:** Marcel Steiner (human) · **Executed by:** Claude Opus 4.8 via subagent-driven development
> **Branch:** `feature/spec-003-auth-impl` (from `develop`) · **Method:** KAIFe L4, one implementer subagent per task + per-task spec+quality review + whole-branch review.

## How the change was produced (KAIFe Principle 3 — accountability)

Executed with the `superpowers:subagent-driven-development` harness: a fresh implementer subagent per task (TDD — failing test first), a spec-compliance + code-quality review after each task, a fix loop for findings, and a final whole-branch review on the most capable model. The controller never edited feature code directly; all code came from reviewed subagent commits. Progress tracked in a git-ignored ledger (`.superpowers/sdd/…/progress.md`).

Notable controller decisions (recorded in the ledger):
- **Re-sequenced** execution to keep the build green: additive tasks (User, ports, provisioning, persistence, auth wiring) first; the ownership **cutover** (Storage.OwnerId + query filter + migration + enforcement + service-test updates) as one coherent breaking group.
- **Single `InitialCreate` migration** (no release exists) instead of stacking a second migration.
- **Podman** (not Docker) runs the Testcontainers service suite; controller ran it (sandbox-off + `DOCKER_HOST`), since subagents are sandboxed. Frontend worktree `node_modules` symlinked from the main repo (identical lockfiles).

## Tasks & commits

| Task | Deliverable | Key commits |
|------|-------------|-------------|
| 1 | `User` domain entity (EC-02 display-name fallback) | f1c6867 |
| 3+4 | `ICurrentUser`, `IUserRepository`, `ProvisionUserUseCase` (race-safe) | 9f97ef1, b2a4aa4 |
| 5 | User persistence (unique `(Issuer,Subject)`, 23505→domain signal + detach) | 5f86651 |
| 7+8 | BFF auth: cookie + per-provider OIDC, provisioning in `OnTokenValidated`, `/auth/*`, test auth handler | c014ef8 |
| 2+6+10 (cutover) | `Storage.OwnerId`, EF **global query filter**, `RequireAuthorization` + fallback policy, owner stamped server-side, `OwnershipTests` | ca33525, 7289f25, 12c751a |
| 9 | Single `InitialCreate` migration (users + ownership) | 5c8e99c |
| — | 401 (not 500) for unauth API (default challenge = cookie) | bc8ac46 |
| 8a | CSRF double-submit on mutations (403) + test-client priming | 698fbd5 |
| 11 | OpenAPI 401/403/404 + `/auth/*` responses | a851bde |
| 12 | Threat-model **R-06 → mitigated** | 4070572 |
| 13+14 | `AuthService` (signal session) + 401 interceptor + XSRF header | bba7f85, 39f7d1b |
| 15+16+17 | Auth guard, login page, session header, i18n de/en/fr/it | 8321e3e, debc8aa |
| 18 | Development-only `/auth/dev-login` + Playwright E2E | 1bda691, a9791fc |
| final | HTTPS OIDC metadata required outside Development; empty-issuer guard | 49155ec |

## Acceptance-criteria coverage (test evidence)

- AC-01 401 unauth · AC-05 `/auth/me` · AC-07 `/health` open → `AuthEndpointsTests`, `OwnershipTests`.
- AC-03/04 JIT provisioning + reuse · EC-02 no-email · EC-03 two providers → `ProvisionUserUseCaseTests`, `UserTests`.
- AC-08 owner server-side · AC-09 list own only · AC-10 cross-user 404 · AC-11 items transitive · AC-12 SPEC-001 unchanged → `OwnershipTests` + SPEC-001 service tests (now authenticated).
- CSRF 403 → `CsrfTests`. EC-01 expired→login, frontend auth → Vitest specs (`auth.service`, `auth.interceptor`, `auth.guard`, `app`).
- AC-02 login flow & EC-04 invalid code/state: no live-IdP automated test (BFF constraint) — covered structurally + by the shared `sub_local` invariant across OIDC/test-handler/dev-login + deferred Playwright E2E (runs in CI).

**Final verification:** backend (via Podman) Domain 60 · Architecture 9 · Service 59; frontend Vitest 51; build 0 warnings. Whole-branch review: **MERGE-READY** (one must-fix — HTTPS metadata gating — applied and re-verified).

## Notes for the reviewer (Gate G2)

- **`POST /auth/dev-login` is Development-only** (`if (app.Environment.IsDevelopment())` in `Program.cs`) — confirm it stays unreachable in staging/prod.
- Deferred minors (see ledger): OpenAPI 403/404 over-declared on GET endpoints (conservative); a few frontend cosmetic/style items and negative-path test gaps. None load-bearing.
- E2E execution and the OIDC callback path are validated in CI (full stack), not locally.
- Data & compliance (KAIFe §7): only synthetic identities used (`e2e@store-it.local`, issuer `dev`); OIDC secrets are empty in `appsettings.json` and come from the environment.

## Local runtime verification + post-review hardening

Ran the real backend (Development, Podman Postgres) and drove the HTTP surface with `curl`: anonymous `/api/v1/**` → 401, `/health` → 200, `POST /auth/dev-login` → session with a real JIT-provisioned user, `GET /auth/me` → profile, mutation without CSRF token → 403, mutation with session+token → 201 (owner stamped), list → only the owner's storage. All as specified.

**Finding fixed in-branch (commit `d4c99bf`):** with unconfigured OIDC (empty `ClientId`, the appsettings default), the OIDC handler threw on every request — `/health` returned 500. Fix: register each OIDC provider only when its `ClientId` is non-empty, and return `400 auth.provider.unconfigured` for a login attempt to an unconfigured provider. `/health` and the app now boot resiliently without OIDC secrets (a k8s liveness probe no longer cascades on an auth misconfig). Regression test `NoOidcConfigTests` added; re-verified at runtime (`/health` → 200 with empty config). Backend service suite: 62/62.
