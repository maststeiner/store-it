# Spec: Shared storages — several household members work on one storage

> **Status:** Frozen (Gate 1) — approved by Marcel Steiner, 2026-09-23
> **Sprint:** 2026-S39
> **Author:** Claude Fable 5.1 (developer agent), from Marcel Steiner's direction (issue #81)
> **Last updated:** 2026-09-23

---

## User Story

As a **household member** I want to **share one of my storages with the people I live with,
by sending them a link,** so that **we all see and maintain the same pantry, freezer or cellar
instead of each keeping our own copy.**

---

## Context & Relationship to Existing Work

- SPEC-003: one owner per storage, global query filter, cross-user by-id → 404. Sharing was
  left out on purpose ("the data model is prepared for it").
- SPEC-006: account deletion cascades the account's storages. This spec decides what that means
  for shared storages (decision 6 of ADR-008; SPEC-006 amendment A1 below).
- ADR-008 (this feature's model): flat membership, owner stays on the storage, link
  invitations with hashed 7-day tokens, leave ≠ delete, no automatic handover.
- Delivered in **two PRs** against one frozen spec: **PR 1 "sharing"** (AC-01 – AC-17) and
  **PR 2 "ownership"** (AC-18 – AC-24). Both ship in the same release.

## Decisions taken (Marcel Steiner, 2026-09-23)

| # | Decision |
|---|----------|
| D1 | One level of access. Members change items and the storage name; owner-only: delete for everyone, members, invitation link, ownership transfer. |
| D2 | Invitation by link, 7 days valid, multi-use, one active link per storage, a new link replaces the old, the owner can deactivate it. |
| D3 | Opening a link shows a join page ("*Owner* shares the storage *Name* with you" + *Join*); unauthenticated visitors sign in first and come back; existing members are sent straight to the storage. The token travels in the URL **fragment** (`/join#<token>`), so it never reaches the server's access logs — only the web client reads it and sends it to the API in a request body. |
| D4 | Members see **Leave**; the owner sees **Delete** and, when members exist, chooses between *hand over to …* and *delete for everyone*. |
| D5 | Account deletion deletes owned storages for everyone and leaves the others; the dialog states how many owned storages still have members. No automatic handover. |
| D6 | People are shown by display name only — no e-mail addresses in member lists or on the join page. |
| D7 | Members can **see** the member list (display names only), only the owner can change it. |
| D8 | Shared storages appear in the ordinary list, alphabetically, marked with a **shared icon** (accessible label); the owner's display name is shown on the detail page, not in the list. |

---

## Acceptance Criteria (EARS Notation)

### PR 1 — sharing

**Access**

- [ ] AC-01: WHEN a user lists storages THE system SHALL return the storages they own **and**
      the storages they are a member of, each with `isOwner`, `memberCount` (members without
      the owner) and, for shared ones, the owner's display name.
- [ ] AC-02: WHEN a member reads, adds, updates or deletes items of a shared storage, or renames
      it, THE system SHALL behave exactly as for the owner (D1).
- [ ] AC-03 (Error): WHEN a user who is neither owner nor member addresses a storage by id THE
      system SHALL answer `404` (no existence leak, unchanged from SPEC-003).
- [ ] AC-04 (Error): WHEN a member calls an owner-only operation (delete storage, create or
      deactivate invitation, remove a member) THE system SHALL answer `403` with
      `errorCode: storage.ownerOnly`.

**Invitation link**

- [ ] AC-05: WHEN the owner creates an invitation THE system SHALL generate a random token (at
      least 128 bit), store only its SHA-256 hash with `expires_at = now + 7 days`, replace any
      earlier active invitation of that storage, and return the token once together with the
      expiry.
- [ ] AC-06: WHEN the owner reads the storage's invitation THE system SHALL return whether one is
      active and its expiry — never the token again.
- [ ] AC-07: WHEN the owner deactivates the invitation THE system SHALL make the token unusable
      immediately.
- [ ] AC-08: WHEN a signed-in user previews a token (sent in the request body, never in the URL)
      THE system SHALL return the storage name and the owner's display name for a valid token, and `404` with `errorCode: invite.invalid`
      for an unknown, expired or deactivated one — same answer for all three, so a token
      cannot be probed for its state.
- [ ] AC-09: WHEN a signed-in user accepts a valid token THE system SHALL add them as a member
      (idempotent for existing members) and return the storage id; the owner accepting their
      own link SHALL be treated as "already a member".
- [ ] AC-10 (Error): WHEN an expired or deactivated token is accepted THE system SHALL answer
      `404 invite.invalid` and add nobody.

**Members**

- [ ] AC-11: WHEN an owner or member lists a storage's members THE system SHALL return the owner
      and the members with display name and `joinedAt` (members) — no e-mail addresses (D6, D7).
- [ ] AC-12: WHEN the owner removes a member THE system SHALL end that membership; the removed
      user's next request for the storage answers `404`.
- [ ] AC-13: WHEN a member leaves a storage THE system SHALL end their membership; the storage
      and its items are untouched.
- [ ] AC-14 (Error): WHEN the owner tries to leave THE system SHALL answer `409` with
      `errorCode: storage.ownerCannotLeave` (hand over or delete instead — PR 2).

**Web client (PR 1)**

- [ ] AC-15: The storage list marks shared storages with an icon (D8); the detail page of a shared
      storage shows the owner's name and offers *Leave* to members and *Share* to the owner.
- [ ] AC-16: The owner's *Share* view creates the link, shows it with a copy button and the
      expiry, and offers *Deactivate*; the member list shows display names and, for the owner,
      *Remove* per member.
- [ ] AC-17: The route `/join` reads the token from the URL fragment (`/join#<token>`, D3) and
      shows the join page; *Join* opens the storage; a missing or invalid token shows a plain
      message with a link back to the list. All strings in de/en/fr/it.

### PR 2 — ownership

- [ ] AC-18: WHEN the owner transfers ownership to a member THE system SHALL make that member
      the owner and the previous owner a member; the invitation link (if any) stays valid.
- [ ] AC-19 (Error): WHEN ownership is transferred to a non-member THE system SHALL answer `404`
      with `errorCode: member.notFound`.
- [ ] AC-20: WHEN the owner deletes a storage that has members THE client SHALL offer *hand over
      to …* (choose a member) or *delete for everyone*; without members the dialog is the
      existing one.
- [ ] AC-21: WHEN a user deletes their account THE system SHALL delete the storages they own
      (for everyone) and end their memberships elsewhere (D5) — the SPEC-006 cascade plus the
      membership FK.
- [ ] AC-22: WHEN a user opens the account-deletion dialog THE client SHALL state how many of
      their owned storages still have members ("N shared storages will be deleted for everyone
      — hand them over first if you want to keep them"), from a value the API provides.
- [ ] AC-23: The member list offers the owner *Make owner* per member (AC-18).
- [ ] AC-24: The deployment's privacy text is not affected (deletion still deletes all own data);
      SPEC-006 gets amendment A1 naming the shared-storage consequence.

---

## Edge Cases

- EC-01: **Link forwarded outside the household** — anyone signed in can join within 7 days.
  Mitigation is the owner's: deactivate the link, remove the member. The share view says so.
- EC-02: **Token reuse by the same person** — second accept is a no-op (AC-09).
- EC-03: **Owner deletes the storage while a member is editing** — the member's next request
  answers 404; the client's existing 404 handling sends them back to the list.
- EC-04: **Member removed while on the detail page** — same as EC-03.
- EC-05: **Two owners?** Impossible by construction: `OwnerId` is one column; transfer swaps it
  in one transaction (AC-18).
- EC-06: **Display name changes** — names are read from `users` at request time, never copied.
- EC-07: **Item counts / expiry summaries** are per storage, not per user — unchanged.
- EC-08: **Stale session** (SPEC-006 D6) of a deleted account touching a shared storage: reads
  see nothing (not owner, not member), writes to items answer 404; creating a storage still maps
  to `401 auth.session.stale`.
- EC-09: **Join page while signed out** — `/join` is a protected route: the guard sends the
  visitor to sign-in with `returnUrl=/join#<token>` (existing mechanism; the fragment must
  survive the round trip, `appLocalPath` keeps it), sign-in returns them there.
- EC-10: **Token exposure** — the token appears only in the owner's share view, in the medium
  the owner chose, and in the recipient's browser (address bar, history). Neither Caddy, nginx
  nor the API log it: fragment in the URL, request body towards the API, hash in the database.

---

## UI Requirements (web)

- List: shared icon with an accessible label ("shared"); nothing else changes in the row.
- Detail page header: owner name for members; actions *Share* (owner) / *Leave* (member) next to
  the existing rename/delete controls; *Delete* only for the owner.
- Share view (owner): invitation card (create / link + copy + expiry / deactivate), member list
  (display names; *Remove*; PR 2: *Make owner*).
- Join page: storage name, owner name, *Join*; invalid → message + link to the list.
- Confirmations reuse `ConfirmDialog`; the owner's delete-with-members dialog (PR 2) adds a member
  picker for *hand over*.
- All strings in de/en/fr/it; no e-mail addresses anywhere in these views.

---

## Out of Scope

- Roles / read-only access; per-item permissions.
- Invitations by e-mail address, pending invitations, notifications.
- Automatic ownership handover on account deletion (decided against, D5).
- Activity log ("who changed what"), real-time updates, conflict resolution.
- Sharing across households/organizations.

---

## Technical Constraints (from Architect Agent)

<!-- To be confirmed after Gate 1 -->

- [ ] Data: `storage_members(storage_id FK→storages ON DELETE CASCADE, user_id FK→users ON DELETE
      CASCADE, joined_at)` with PK `(storage_id, user_id)` and index `(user_id, storage_id)`;
      `storage_invitations(storage_id PK/FK ON DELETE CASCADE, token_hash unique, expires_at,
      created_at)`. One EF migration.
- [ ] Access predicate in one place: the `Storage` query filter becomes `OwnerId == me OR
      Members.Any(m => m.UserId == me)`; owner-only operations check `OwnerId == me` in the
      Application layer (`StorageOwnerOnlyException` → 403).
- [ ] Domain: `Storage` gains `Members` (collection of `StorageMember(UserId, JoinedAt)`),
      `AddMember`, `RemoveMember`, `TransferOwnership`; invitations are an Infrastructure/API
      concern (token generation + hashing in the API layer, storage via a repository port).
- [ ] API (additive, `/api/v1`): `GET/POST/DELETE /storages/{id}/invitation`,
      `POST /invitations/preview` and `POST /invitations/accept` (token in the JSON body),
      `GET /storages/{id}/members`, `DELETE /storages/{id}/members/{userId}`,
      `DELETE /storages/{id}/membership` (leave); PR 2: `PUT /storages/{id}/owner`,
      `GET /account` (`ownedSharedStorages` count). `StorageResponse` gains `isOwner`,
      `memberCount`, `ownerName`. Contract + client regenerated.
- [ ] Layering per ADR-001/ADR-008; architecture tests unchanged.
- [ ] Dependencies: none new (SHA-256 and `RandomNumberGenerator` are in the BCL).
- [ ] ADR required: yes → ADR-008.

---

## Verification

<!-- Filled in by QA / developer during implementation -->

| AC | How verified | Status |
|----|--------------|--------|
| AC-01 … AC-14 | service tests over HTTP + PostgreSQL (two/three users via the Test scheme) | ⬜ |
| AC-15 … AC-17 | vitest component tests; i18n parity | ⬜ |
| AC-18 … AC-24 | service + component tests (PR 2) | ⬜ |
| End to end (G3) | Marcel and Patrizia share a storage on `prod-oracle` via a link, both edit it, one leaves; owner hands over and deletes | ⬜ |

---

## Gate Status

| Gate | Status | Date | Person |
|------|--------|------|--------|
| G1 · Spec Freeze | ✅ | 2026-09-23 | Marcel Steiner |
| G2 · Review | ⬜ | | |
| G3 · DoD/Merge | ⬜ | | |
