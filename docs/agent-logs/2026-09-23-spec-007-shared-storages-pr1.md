# Agent Run Log: shared storages — PR 1 "sharing" (SPEC-007, ADR-008, #81)

> **Date:** 2026-09-23
> **Spec:** [SPEC-007](../specs/SPEC-007-shared-storages.md) — frozen by Marcel Steiner 2026-09-23; [ADR-008](../architecture/ADR-008-storage-sharing-model.md) accepted the same day
> **Persona(s):** analyst (spec/ADR), developer, qa
> **Model:** Claude Fable 5.1
> **Branch / PR:** `feature/shared-storages` → PR linked from #81 (PR 1 of 2)

---

## Task

Let household members work on one storage (#81). Marcel asked to be walked through every
decision, to be asked whatever was unclear, and to have his choices challenged where warranted.
Deliver spec + ADR, then the first of two PRs: membership, invitation links, member list, leave.

## Plan

1. Read SPEC-003 (ownership, filter, "sharing later"), the aggregate, the endpoints, the client.
2. Ask the fundamental questions (permission model, invitation flow, revocation, owner deletion)
   with a proposal and a counter-argument each; take Marcel's simpler sketch, challenge two
   points, agree on two PRs.
3. Draft SPEC-007 + ADR-008; ask the detail questions (member list visibility, list marking,
   token exposure); incorporate; get acceptance + freeze.
4. PR 1: domain (members, invitation), application (use cases, ports, guards), infrastructure
   (filter, tables, migration, hashed tokens), API (endpoints, DTOs, error codes), web (share
   panel, join page, list badge, leave), tests, docs.

## Key Decisions (Marcel's, with the agent's challenges where they changed something)

- **Flat access, one owner** — Marcel's direction; matches the household. No roles.
- **Leave ≠ Delete** — challenged Marcel's "delete removes it from my view" as misleading;
  members see *Leave*, only the owner *Delete* (PR 2 adds the hand-over choice).
- **No automatic hand-over on account deletion** — Marcel dropped his "longest member" idea
  after the agent pointed at the SPEC-006 promise; owned storages are deleted for everyone,
  the dialog warns (PR 2).
- **Invitation by link, 7 days, multi-use, one per storage, deactivatable; display names only;
  members may see the member list; shared icon in the list** — Marcel's answers to the
  agent's proposals.
- **Token in the URL fragment** (`/join#<token>`) — agent's proposal, accepted: no server ever
  logs it; the client posts it in a body; the database stores a SHA-256.
- **Owner stays `storages.OwnerId`, members in `storage_members`** — additive schema, the
  filter becomes `owner OR member`; the owner is never a member row (domain guard).
- **Exception handler as a type table** — Sonar S1541 fired on the third new mapping; fixed
  data instead of switch arms, with the dynamic cases (validation, bad request) kept in the switch.
- **Test scheme gotcha kept in mind:** the Test scheme provisions on every request, so
  "stranger" is a real third user, not a stale session.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "frage alles was nicht klar ist, nichts annehmen und selber entscheiden … meine Entscheidungen hinterfragen" | Working mode for this feature. |
| 2 | Marcel's simplified sketch (flat access, link, owner, delete = disappear from my view, hand-over on account deletion, remove members) + "in einem oder mehrere Tasks?" | Basis of ADR-008; two points challenged (leave vs delete; no automatic hand-over); split into two PRs agreed. |
| 3 | "1: Ja (nur die Anzeigenamen), 2: Ja … mit einem Symbol, 3. mit hash, 4 und 5 werde ich anschliessend reviewen" | D7, D8, fragment token; review before acceptance/freeze. |
| 4 | "ADR-008 akzeptiert, SPEC-007 einfrieren und PR 1 umsetzen" | G1 + ADR acceptance. |

## Verification

See the spec table (AC-01 – AC-17 all mapped to tests). Local: backend 196 tests green, CSharpier;
frontend 129 vitest green, lint, prettier, `ng build`. Migration `SharedStorages` scaffolded and
applied by the service-test fixture against PostgreSQL 18.

## Outcome

- **Result:** PR 1 opened against `develop`; PR 2 (ownership: hand-over, owner delete dialog,
  account-deletion hint, SPEC-006 amendment) follows on the same spec.
- **Deviations from spec:** none.
- **Harness follow-up:** none.
