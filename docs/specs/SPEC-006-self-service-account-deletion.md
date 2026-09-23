# Spec: Users delete their own account and all their data

> **Status:** Draft
> **Sprint:** 2026-S39
> **Author:** Claude Fable 5.1 (developer agent), from Marcel Steiner's request (issue #168)
> **Last updated:** 2026-09-23

---

## User Story

As a **signed-in user** I want to **delete my account together with everything I stored**
so that **no data about me remains in store-it, without having to ask the operator.**

---

## Context & Relationship to Existing Work

- SPEC-003 introduced accounts keyed by (Issuer, Subject) and put **account deletion**
  explicitly *out of scope* — while noting that the FK `ON DELETE CASCADE` from `storages`
  to `users` (and from `items` to `storages`) is already modelled. This spec builds that
  user-facing flow on the existing schema; **no migration is needed**.
- The first public deployment (`prod-oracle`, ADR-005) publishes a privacy policy that today
  says "deletion on request" and announces an in-app function. Marcel's direction
  (2026-09-23): promise little, let users act themselves.
- The web client is the only client (SPEC-002 typed client, regenerated from the contract);
  the new endpoint is additive, so the ADR-007 contract gate sees no breaking change.

## Chosen Approach (summary)

One authenticated, CSRF-protected endpoint deletes the current user's row; the database
cascade removes storages and items in the same transaction; the response also expires the
session cookie. The web client offers **Delete account** in the session menu, asks for an
explicit confirmation with a plain warning, and afterwards lands on the sign-in page with a
short confirmation notice.

### Decisions taken (defaults proposed by the agent, to be confirmed at G1)

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | Endpoint `DELETE /api/v1/account`, operationId `deleteAccount`, `204 No Content`; same group rules as the storages tree: `RequireAuthorization` + CSRF filter → `401` / `403`. | Under `/api/v1` because it is an authenticated, versioned resource of the signed-in user — not part of the anonymous `/auth` BFF group. Additive → no `/api/v2`. |
| D2 | Deletion = remove the `users` row; PostgreSQL cascades `storages` → `items` (existing FKs). The use case lives in Application (`DeleteAccountUseCase`), the repository gains `GetByIdAsync` and `Remove`. | Reuses the schema decision of SPEC-003; one statement, one transaction, no partial state. |
| D3 | The response of a successful deletion **signs the session out** (expired cookie), like `POST /auth/logout`. | The principal's `sub_local` points to a row that no longer exists; keeping the cookie would leave a zombie session in this browser. |
| D4 | UI: a second item **Delete account** in the session menu (below *Sign out*), styled as a destructive action; confirmation via the existing `ConfirmDialog` with a title and a message that names what is lost ("all storages and items, irreversible"). **No typed confirmation.** | The existing dialog already focuses the confirm button, traps focus and closes on Escape; a typed confirmation adds friction without adding safety for a one-user, one-click product. |
| D5 | After success the client clears the user signal and navigates to `/login?account=deleted`; the sign-in page shows one line "Your account and all your data have been deleted." | Users need feedback that the action happened; the login page is where they land anyway. |
| D6 | Sessions of the deleted account **on other devices** are not revoked eagerly (that would need a per-request database check the BFF design deliberately avoids — SPEC-003 "pure claim read"). Instead: reads with a stale session return empty lists (ownership filter), and the one write that can fail on the missing owner (`POST /api/v1/storages`, FK violation) is mapped to `401` with `errorCode: auth.session.stale` and the session cookie expired, which the client's 401 handling turns into a redirect to sign-in. | Correct behaviour with zero per-request cost; the window is bounded by the cookie lifetime. |
| D7 | A later sign-in with the same provider account creates a **new, empty** user (existing `ProvisionUserUseCase` behaviour). Nothing is remembered. | "No data about me remains" includes the identity row. |

---

## Acceptance Criteria (EARS Notation)

### API

- [ ] AC-01: WHEN an authenticated user with a valid CSRF token sends `DELETE /api/v1/account`
      THE system SHALL delete the user's row and, by cascade, all storages and items owned
      by that user, and SHALL answer `204 No Content`.
- [ ] AC-02: WHEN the deletion succeeds THE system SHALL expire the session cookie in the
      same response (as `POST /auth/logout` does).
- [ ] AC-03 (Error): WHEN `DELETE /api/v1/account` is called without a session THE system
      SHALL answer `401`; WHEN called without a valid CSRF token THE system SHALL answer `403`
      with `errorCode: csrf.invalid` (group behaviour, unchanged).
- [ ] AC-04: WHEN a user's account has been deleted THE system SHALL leave every other user's
      storages and items untouched (verified by test: two users, one deleted).
- [ ] AC-05: WHEN a request with a session whose user no longer exists creates a storage THE
      system SHALL answer `401` with `errorCode: auth.session.stale` and expire the session
      cookie, instead of a `500` (D6).
- [ ] AC-06: WHEN the same provider identity signs in after a deletion THE system SHALL
      provision a fresh user with no storages (D7).
- [ ] AC-07: The OpenAPI contract SHALL contain the new operation (`deleteAccount`) and the
      breaking-change gate SHALL report no breaking change (additive).

### Web client

- [ ] AC-08: WHEN the session menu is open THE client SHALL offer *Delete account* as a
      destructive menu item below *Sign out*, keyboard-navigable like the existing item.
- [ ] AC-09: WHEN the user chooses *Delete account* THE client SHALL show a confirmation
      dialog naming the consequence ("all your storages and items, irreversible") and SHALL
      call `DELETE /api/v1/account` only after the confirm button.
- [ ] AC-10: WHEN the deletion succeeded THE client SHALL clear the session state and
      navigate to the sign-in page, which SHALL show a one-line confirmation notice.
- [ ] AC-11 (Error): WHEN the deletion request fails THE client SHALL keep the session state
      unchanged and show the error through the existing API error handling.
- [ ] AC-12: All new user-facing strings SHALL exist in de / en / fr / it (i18n completeness
      test).

---

## Edge Cases

- EC-01: **Double submit** (two tabs, both confirm): the second `DELETE` finds no user → the
  use case treats "already gone" as success (`204`, cookie expired). Idempotent.
- EC-02: **Stale sessions elsewhere** — D6/AC-05. Item endpoints cannot hit the case: the
  storages they need are gone, so they answer `404` as today.
- EC-03: **Concurrent write while deleting** (a storage is being created in another tab):
  whichever commits second loses — the insert gets the FK violation → `401` (AC-05), or the
  cascade takes the new storage with it. No orphan either way.
- EC-04: **Provider identity unchanged**: deletion does not touch the Google/Microsoft
  account; the privacy policy says so (deployment repository).
- EC-05: **Dev login** (`Development` only) provisions a user like any provider; deleting it
  works the same and needs no special handling.

---

## UI Requirements (web)

- Session menu: second `menuitem` "Delete account" (`auth.session.deleteAccount`), visually
  separated from *Sign out* and coloured as destructive (existing `btn-danger` tone).
- Confirmation: `ConfirmDialog` with `auth.deleteAccount.title` /
  `auth.deleteAccount.message`; confirm label stays the generic `actions.delete`.
- Sign-in page: notice `auth.deleteAccount.done` when the route carries
  `account=deleted`; rendered as a status line (`role="status"`), no toast library.
- Strings in all four locales; no hard-coded text.

---

## Out of Scope

- **Data export** before deletion (would be its own story; nobody asked for it yet).
- **Eager revocation** of sessions on other devices (D6 explains the trade-off).
- **Deleting the identity at the provider** — not ours.
- **Operator-side deletion tooling** (a runbook SQL is enough and exists in the deploy repo's
  knowledge; not part of the product).
- **Grace period / soft delete / undo** — the user is told it is irreversible; keeping data
  "just in case" contradicts the purpose.

---

## Technical Constraints (from Architect Agent)

<!-- To be confirmed after Gate 1 -->

- [ ] Layering: use case in `StoreIt.Application` (`DeleteAccountUseCase(IUserRepository,
      ICurrentUser)`), repository methods in `IUserRepository` / `UserRepository`, endpoint in
      `StoreIt.Api` (`AccountEndpoints`), no EF types outside Infrastructure (ADR-001).
- [ ] AC-05 mapping: `StorageRepository.SaveChangesAsync` translates the FK violation
      (`23503` on `storages.OwnerId`) into an Application exception
      (`OwnerNoLongerExistsException`); `DomainExceptionHandler` maps it to `401` +
      `auth.session.stale` and signs the cookie scheme out.
- [ ] Contract: `backend/openapi/StoreIt.Api.json` regenerated (`dotnet build`), web client
      regenerated (`npm run generate:api`), both committed (drift gates).
- [ ] Dependencies: none new.
- [ ] ADR required: no (uses ADR-001, ADR-004, ADR-006 as they stand).

---

## Verification

<!-- Filled in by QA / developer during implementation -->

| AC | How verified | Status |
|----|--------------|--------|
| AC-01 | service test: create storages+items as user A, `DELETE /api/v1/account` → 204, rows gone (DbContext count) | ⬜ |
| AC-02 | service test: `Set-Cookie` expires `.AspNetCore.Cookies` | ⬜ |
| AC-03 | service tests: anonymous → 401; no CSRF → 403 `csrf.invalid` | ⬜ |
| AC-04 | service test: user B's storages unchanged after A's deletion | ⬜ |
| AC-05 | service test: delete user, keep the old client, `POST /api/v1/storages` → 401 `auth.session.stale`, cookie expired | ⬜ |
| AC-06 | service test: provision same (issuer, subject) again → new id, zero storages | ⬜ |
| AC-07 | contract gate in CI (drift + report mode), `OpenApiContractTests` | ⬜ |
| AC-08–AC-11 | vitest: session-menu (item, keyboard), app flow (dialog → service call → navigation), login-page notice; auth.service delete method | ⬜ |
| AC-12 | existing `i18n.spec.ts` key-parity test | ⬜ |
| End to end (G3) | Marcel deletes a test account on `prod-oracle` and signs in again to an empty app | ⬜ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ⬜ | | |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
