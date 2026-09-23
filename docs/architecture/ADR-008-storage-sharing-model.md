# ADR-008: Storage sharing — one owner, flat membership, link invitations

> **Status:** Accepted (Marcel Steiner, 2026-09-23)
> **Date:** 2026-09-23
> **Deciders:** Marcel Steiner

---

## Context

SPEC-003 gave every storage exactly one owner (`storages.OwnerId`) and isolates users with an
EF Core global query filter (`OwnerId == current user`). Issue #81 asks for sharing: several
household members working on the same storage. The issue left the permission model, the
invitation flow, the data model and ownership handling open and required an ADR before
implementation.

Marcel's direction for the first round (2026-09-23): keep it simple — a storage can be
shared, everyone with access can edit it, sharing happens through a time-limited link that is
sent over any medium, a storage always has one owner, and the owner can remove members.

---

## Decision

1. **Flat permission model.** Two relations to a storage: **owner** (exactly one) and
   **member** (any number). Members may read and change everything *inside* the storage —
   items and the storage name. Only the owner may delete the storage for everyone, manage
   members, create or deactivate the invitation link and hand over ownership. No roles, no
   read-only access.
2. **Membership table, owner stays on the storage.** `storage_members(storage_id, user_id,
   joined_at)` holds the non-owner members; `storages.OwnerId` stays as it is. The access
   predicate becomes *owner or member*. The global query filter changes from
   `OwnerId == me` to `OwnerId == me OR EXISTS member(me)`; a by-id request from a non-member
   still answers 404, never 403 (no existence leak, as SPEC-003 AC-10).
3. **Invitation by link, not by e-mail lookup.** The owner creates an invitation for a
   storage; the app shows a URL carrying an opaque random token in the **fragment**
   (`/join#<token>`), so no server — Caddy, nginx or the API — ever logs it. One active
   invitation per storage: creating a new one replaces the old; the owner can deactivate it.
   The token is valid for **7 days** and may be used by any number of signed-in users while
   valid. The database stores only a **hash** of the token (SHA-256); the plain token exists
   once, in the owner's browser. The web client sends the token to the API in a request body.
   Redeeming requires a signed-in user and an explicit "join" click on a page that names the
   storage and its owner.
4. **Leaving vs. deleting.** A member leaves a storage (their membership row is removed; the
   storage is untouched). The owner deletes a storage; if it has members, the owner chooses
   between deleting it for everyone and handing ownership to a member first. A storage is
   physically deleted only by its owner — never as a side effect of the last member leaving,
   because the owner is always still there.
5. **Ownership transfer** is an explicit owner action (to any member); the previous owner
   becomes an ordinary member. The invitation link, if any, stays valid — it belongs to the
   storage, not to a person.
6. **Account deletion (SPEC-006) is not softened.** Deleting an account deletes every storage
   that account **owns** — for all its members — and removes the account from every storage
   it is a **member** of. No automatic handover. The account-deletion dialog names the number
   of owned storages that still have members so the person can hand them over first.
7. **Identity shown to others: display name only.** Member lists and the join page show
   `DisplayName`, never the e-mail address (it is personal data the household does not need).
   Members may see the member list; only the owner changes it.

---

## Rationale

- **Flat model over roles:** in a household, anyone who can see a storage must be able to
  change it, otherwise the sharing is useless. Roles would add role UI, role tests and a role
  question to every later feature. Marcel confirmed no real read-only user exists.
- **Link over e-mail lookup:** no mail infrastructure; works for people who have no account yet
  (they sign in first, then join); and the app never has to make users discoverable by e-mail
  address — some providers deliver none (SPEC-003 EC-02), and one person has different
  addresses at Google and Microsoft. Multi-use within 7 days lets one link serve a family
  group chat.
- **Owner kept on the storage** rather than as a role row: the existing schema, filter and
  every "is owner" check stay meaningful; the membership table is purely additive. A storage
  can never be ownerless.
- **Hashed token:** a database dump or backup must not be a list of usable invitations. The
  hash costs nothing.
- **Leave ≠ delete, labelled honestly:** Marcel's first sketch had one "delete" that only
  removed the storage from one's own view. A member who clicks *Delete* while others keep the
  storage — or believes it is gone for everyone — is misled. Two words, two actions.
- **No handover on account deletion:** keeps SPEC-006's promise ("all your data are deleted")
  literally true and avoids a silent transfer to someone who did not ask for it. The owner can
  hand over deliberately before deleting.

Rejected: roles (viewer/editor/co-owner); invitation by e-mail address; pending invitations that
activate on first sign-in; automatic ownership handover to the longest-standing member on
account deletion (Marcel's first idea, replaced by decision 6); soft delete / "disappears from my
view" semantics for members.

---

## Consequences

**Positive:**
- Small additive schema change; no data migration of existing storages (their owner is set).
- Every authorization question has a one-word answer: owner or member?
- Invitation needs no external service; tokens leak nothing if the database leaks.

**Negative / Trade-offs:**
- Every storage query now joins the membership table (index on `(user_id, storage_id)` keeps
  it cheap; the app is a household tool, not a platform).
- A forwarded link joins anyone who is signed in within 7 days — the owner is told this in the
  UI and can deactivate the link or remove members.
- An owner who deletes the account without handing over takes shared storages with them;
  the dialog warns, nothing more.
- Concurrent edits by several members are last-write-wins (out of scope in #81).

---

## Layering Rules (for the Architecture Conformance Gate)

Unchanged (ADR-001). The access predicate lives in one place — the Infrastructure query filter
plus an Application-level `IStorageAccess`/owner check for owner-only operations — never in the
web client. Tokens are generated and hashed in the API/Infrastructure layer; the Domain knows
memberships and ownership, not tokens.
