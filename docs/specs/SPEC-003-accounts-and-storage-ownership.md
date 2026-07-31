# Spec: Accounts & storage ownership (login via external IdP)

> **Status:** Frozen (Gate 1)
> **Sprint:** 2026-S16
> **Author:** Analyst Agent (orchestrated by Marcel Steiner)
> **Frozen:** 2026-07-30 · **Last updated:** 2026-07-30

---

## User Story

As a **household member** I want to **sign in with my existing Microsoft or Google
account** so that **only I can see and manage the storages and items I created**.

---

## Context & Relationship to Existing Work

- SPEC-001 shipped the walking skeleton with the deliberate simplification *"all
  storages are globally visible"* — see SPEC-001 *Out of Scope*. This spec removes
  that simplification by introducing user accounts and per-storage ownership.
- This is the trigger for **ADR-004 (Identity / auth solution)** — GitHub issue #16,
  and the three `TODO` rows in `docs/architecture/ARCHITECTURE.md` (:91, :167, :196).
- It addresses threat-model risk **R-06 (Broken access control / no authn-authz)**,
  currently *"Not yet addressed"* in `docs/security/threat-model.md`.

---

## Chosen Approach (summary — full rationale in ADR-004)

**Backend-for-Frontend (BFF) · direct OIDC federation · no broker.**

- The .NET API integrates the upstream providers **directly** as OIDC confidential
  clients — one OpenID Connect handler per provider (**Microsoft + Google** for this
  spec; Apple added later). No intermediate identity broker (no Keycloak).
- The BFF pattern is retained: the OIDC authorization-code (+ PKCE) exchange happens
  **server-side**, and the API holds a secure **HttpOnly session cookie**. **No
  access/ID tokens live in the browser** — the strongest posture against XSS token
  theft (R-06).
- Each provider requires its own app registration (Entra app registration; Google
  Cloud OAuth client) with per-environment redirect URIs. Client id/secret per provider
  come from the environment (12-factor).
- Trade-off vs a broker: no extra infrastructure to host/operate, but the app now owns
  each provider's integration and quirks directly (e.g. Apple's periodic client-secret
  rotation later).

```
Browser (Angular)  --HttpOnly session cookie-->  .NET API (BFF)  --OIDC code+PKCE-->  Microsoft / Google
   /auth/login/{provider} -> 302 -> provider -> /auth/callback/{provider} -> session set -> 302 back into the SPA
```

---

## Acceptance Criteria (EARS Notation)

### Authentication

- [ ] AC-01: WHEN an unauthenticated caller requests any protected endpoint
  (`/api/v1/**`) THE system SHALL respond with **401** (no data disclosure).
- [ ] AC-02: WHEN a user starts login for a chosen provider (`/auth/login/{provider}`)
  THE system SHALL redirect them to that provider via OIDC, and on successful sign-in
  (via `/auth/callback/{provider}`) SHALL set an **HttpOnly session** and redirect back
  into the SPA.
- [ ] AC-03: WHEN a user signs in **for the first time** THE system SHALL provision a
  `User` just-in-time, keyed by the OIDC `(Issuer, Subject)` pair, storing `Email` /
  `DisplayName` when the provider supplies them.
- [ ] AC-04: WHEN an **already-known** user signs in THE system SHALL reuse the existing
  `User` (matched by `(Issuer, Subject)`) and refresh `Email` / `DisplayName` — no
  duplicate user.
- [ ] AC-05: WHEN an authenticated user calls `GET /auth/me` THE system SHALL return
  their profile (`DisplayName`, `Email`); WHEN not authenticated THE system SHALL
  respond with **401** (not a 500).
- [ ] AC-06: WHEN a user calls `POST /auth/logout` THE system SHALL end the local
  session (clear the HttpOnly cookie). Provider-side sign-out is out of scope for this
  spec — ending the local session is sufficient.

### Health / unauthenticated allowlist

- [ ] AC-07: WHEN `GET /health` is called **without** a session THE system SHALL respond
  with **200** (Kubernetes probes, CI and load balancers must stay unaffected).

### Ownership / isolation

- [ ] AC-08: WHEN an authenticated user creates a storage THE system SHALL assign the
  current user as its `OwnerId` server-side (never taken from client input).
- [ ] AC-09: WHEN a user lists storages THE system SHALL return **only their own**
  storages.
- [ ] AC-10: WHEN a user reads, updates or deletes a storage or item owned by
  **another** user (by id) THE system SHALL respond with **404** (existence not
  disclosed).
- [ ] AC-11: WHEN a user reads/creates/updates/deletes items THE system SHALL operate
  only within **their own** storages (ownership is transitive via the storage).

### Regression guard

- [ ] AC-12: WHEN an authenticated user uses the SPEC-001 features (storages, items,
  expiry overview) THE system SHALL behave functionally **unchanged**, only additionally
  scoped to that user's own data.

---

## Edge Cases

- EC-01: Session expired → the next `/api/v1` call returns **401**; the SPA redirects to
  login (no silent failure).
- EC-02: A provider returns no email (e.g. Apple later) → the `User` is still
  provisioned, `Email` = null, and `DisplayName` falls back to a placeholder / short form
  of `Subject`.
- EC-03: The same person signing in via **both** Google and Microsoft results in **two
  separate `User` records** (different `Issuer` and `Subject`). Account linking is out of
  scope.
- EC-04: Login callback with an invalid/expired authorization code or a state mismatch
  (CSRF) → login is aborted, **no** session is set, and the user is shown a clear error /
  redirect.

---

## UI Requirements (web)

Consistent with SPEC-001 (modern-but-minimal, no business logic in the frontend,
fully localized in **de / en / fr / it**):

- **Login screen:** minimal, with "Sign in with Microsoft" and "Sign in with Google"
  buttons (Apple added later). No own password form.
- **Route guard:** all app routes (storages/items) are protected; unauthenticated users
  are redirected to login. The SPA determines status via `GET /auth/me`.
- **Session indicator:** header shows `DisplayName` / `Email` and a **Logout** button.
- **401 handling:** an HTTP interceptor catches `401` (e.g. expired session) and
  redirects to login (EC-01).
- **i18n:** all new strings (login, logout, "signed in as …", error messages) exist in
  all four languages; no hard-coded user-facing strings.
- **No token in the browser:** the SPA holds no access/ID tokens — only the BFF's
  HttpOnly session cookie (core of Approach A).

---

## Out of Scope

- **Apple** sign-in — additive follow-up iteration (adding one more OIDC handler; brings
  its own client-secret rotation, hence deferred).
- **Storage sharing** between users — the data model is prepared for it (per-storage
  ownership), the feature itself comes later (the "future" part of issue #16).
- **Account linking** (merging the same person's Google + Microsoft accounts — EC-03).
- **Roles / admin**, fine-grained permissions.
- **Account deletion / GDPR data export** — the FK `ON DELETE CASCADE` behaviour is
  already modelled correctly, but no user-facing flow is built.
- **Profile editing** (name/avatar) — profile data is read-only, sourced from the IdP.
- **MFA / email verification** — handled by the identity provider, not by the app.

---

## Technical Constraints (from Architect Agent)

- [ ] Layering per ADR-001: ownership filtering lives in the Application/Infrastructure
  layer — never in Api controllers or in the frontend.
- [ ] API-first per ADR-002: auth and ownership are exposed/enforced server-side; the
  Angular UI carries no authorization rules.
- [ ] Persistence per ADR-003 (PostgreSQL + EF Core):
  - New `User` entity: `Id` (GUID, PK) · `Issuer` (string — OIDC `iss`) · `Subject`
    (string — OIDC `sub`) with a **unique index on `(Issuer, Subject)`** (a provider's
    `sub` is only unique per issuer, and we now federate multiple issuers directly) ·
    `Email` (nullable) · `DisplayName` (nullable) · `CreatedAt` (timestamptz).
  - `Storage.OwnerId` → FK to `User.Id`, **non-nullable**, `ON DELETE CASCADE`.
  - `Item` has no own owner column; ownership is transitive via its `Storage`.
  - Migration: dev data is synthetic → **fresh start**; the migration adds the `User`
    table and non-nullable `Storage.OwnerId` (no server default). It is **non-destructive** —
    if the `storages` table is non-empty it fails rather than deleting data; the dev DB is
    recreated (drop + update). No blanket `DELETE` (it could destroy data on staging/prod).
- [ ] **Ownership enforced at the data-access layer via EF Core Global Query Filters**
  (defense-in-depth, not a per-handler `WHERE`):
  - A scoped `ICurrentUser` service resolves the current local `User.Id` from the session
    per request and is injected into the `DbContext`.
  - A global query filter on `Storage` (`OwnerId == currentUser.Id`) is appended to
    **every** read automatically; `Item` is filtered transitively via its `Storage`.
  - Consequence: a by-id lookup of another user's storage matches nothing → returns
    `null` → the handler maps it to **404** (this is how AC-10 is satisfied by default).
  - The write path still sets `OwnerId` explicitly (the filter only affects reads).
  - System/no-user contexts (`/health`, JIT provisioning, migrations, seeders) must not
    be filtered — the `User` DbSet is unfiltered and `IgnoreQueryFilters()` is used at
    defined places so the first user can be created.
- [ ] Unauthenticated allowlist (secure-by-default; everything else requires a session):
  `GET /health`, `GET /auth/login/{provider}`, `GET /auth/callback/{provider}`,
  `POST /auth/logout`, `GET /auth/me` (returns 401 when anonymous), `GET /auth/csrf`
  (issues the XSRF cookie the SPA echoes on mutations), and the OpenAPI document
  (dev/non-prod). `/health` stays a pure liveness check with **no** external
  IdP dependency (so a provider outage does not evict the API from the load balancer).
- [ ] API versioning & contract gate per ADR-006: requiring auth on `/api/v1/**` is a
  deliberate **behavioural** contract change. As there are no external consumers yet and
  the path stays `v1`, it is recorded as an intentional change in
  `backend/openapi/StoreIt.Api.json` (the repo's canonical generated contract — adding
  `401` responses and the `/auth/*` endpoints) rather than opening `v2`.
- [ ] Secrets (per-provider OIDC client id/secret) come from the environment
  (12-factor) and are **never committed** (KAIFe §7).
- [ ] ADR required: **yes** — this feature authors **ADR-004** (direct OIDC federation
  + BFF decision, no broker);
  it is the **planned mitigation** for threat-model **R-06** — marked mitigated only after
  the implementation tests pass (Task 12); this docs baseline does not itself claim
  mitigation — and resolves the `ARCHITECTURE.md` TODOs (:91, :167, :196).

---

## Verification

<!-- Filled in by QA Agent — tests derived black-box from the ACs, never from the code. -->
`Domain` = StoreIt.Domain.Tests · `Service` = StoreIt.Api.Service.Tests ·
`E2E` = frontend/e2e · `FE` = frontend vitest specs.

| AC / EC | Test | Status |
|---------|------|--------|
| AC-01 unauthenticated → 401 | | ⬜ |
| AC-02 login flow sets session | | ⬜ |
| AC-03 JIT provisioning (first login) | | ⬜ |
| AC-04 known user reused, no duplicate | | ⬜ |
| AC-05 `/auth/me` (authed vs 401) | | ⬜ |
| AC-06 logout ends session | | ⬜ |
| AC-07 `/health` open without session | | ⬜ |
| AC-08 create assigns OwnerId server-side | | ⬜ |
| AC-09 list returns only own storages | | ⬜ |
| AC-10 cross-user access → 404 | | ⬜ |
| AC-11 items scoped via owning storage | | ⬜ |
| AC-12 SPEC-001 features unchanged for owner | | ⬜ |
| EC-01 expired session → 401 → login | | ⬜ |
| EC-02 no email from IdP still provisions | | ⬜ |
| EC-03 two providers → two users, no linking | | ⬜ |
| EC-04 invalid code / state mismatch aborts | | ⬜ |
| Global query filter isolates at repo level | | ⬜ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ frozen | 2026-07-30 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
