# Agent Run Log: self-service account deletion (SPEC-006, #168)

> **Date:** 2026-09-23
> **Spec:** [SPEC-006](../specs/SPEC-006-self-service-account-deletion.md) — frozen by Marcel Steiner 2026-09-23 ("ja, einfrieren und umsetzen")
> **Persona(s):** analyst (spec), developer, qa
> **Model:** Claude Fable 5.1
> **Branch / PR:** `feature/self-service-account-deletion` → PR linked from #168

---

## Task

While reviewing the privacy policy of the first public deployment, Marcel decided the policy
should promise little and users should be able to delete all their data themselves. Write the
spec, get it frozen, implement it in the same PR (precedent: SPEC-004/#83, SPEC-005/#145).

## Plan

1. Read SPEC-003 (accounts, cascade already modelled, deletion out of scope), the auth
   endpoints, the user aggregate/repository, the session menu and confirm dialog.
2. Draft SPEC-006 with defaults for the open points (endpoint, confirmation, feedback, stale
   sessions), ask for the freeze; incorporate Marcel's answer (typed e-mail).
3. Backend: use case, repository, endpoint, 401 mapping for stale sessions; service tests
   incl. a Test-scheme knob for a stale `sub_local`; regenerate the contract.
4. Frontend: menu item, dialog challenge, service call, login notice, four locales; vitest.
5. Verification table, this log, PR.

## Key Decisions

- **`DELETE /api/v1/account` under the versioned API, not under `/auth`** — it is an
  authenticated, CSRF-protected resource of the signed-in user; `/auth` is the anonymous BFF
  group. Additive, so the ADR-007 gate (0.x report mode) shows an addition only.
- **Deletion = delete the `users` row; the database cascades.** No migration; the schema
  decision of SPEC-003 does the work. Idempotent when the row is already gone (two tabs).
- **The delete response signs the cookie out** like logout — otherwise the browser keeps a
  session whose `sub_local` points nowhere.
- **Stale sessions on other devices are not revoked eagerly** (would need a DB check per request,
  which SPEC-003 avoids). The single write that can hit the missing owner (`POST /api/v1/storages`,
  FK `FK_storages_users_OwnerId`) is translated to `401 auth.session.stale` + cookie expired;
  reads simply return empty lists. Only that constraint name maps — an item whose storage vanished
  still surfaces as 404 through the aggregate load.
- **Typed e-mail confirmation (Marcel).** The generic `ConfirmDialog` gained an optional
  `challenge` input; callers without it are unchanged. Comparison trimmed + case-insensitive;
  accounts without an e-mail type their display name (always present, SPEC-003 EC-02). Focus goes
  to the field when a challenge exists; the focus trap includes it and skips disabled buttons.
- **CSRF filter extracted** into `CsrfEndpointFilter` instead of copying it into the new group.
- **Test-scheme knob `X-Test-LocalId`**: the Test scheme provisions on every request (unlike a
  production cookie), so a stale session needed an explicit way to stamp a deleted id.
- **No Playwright E2E added**: the flow is covered by service + component tests; the human G3 on
  the public URL is what the spec asks for.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "datenschutz: etwas eingeschränkter erfassen. Ich würde den Weg gehen, dass die Benutzer all ihre daten selbst löschen können" | Origin of #168 and this spec; privacy text in the deploy repo shortened meanwhile. |
| 2 | "1: abtippen der eigenen emailadresse würde ich machen" | D4 changed from a plain dialog to a typed challenge before the freeze. |
| 3 | "ja, einfrieren und umsetzen" | G1. Implementation in the same PR. |

## Verification

See the spec's table. Local: backend 176 tests green (105 service incl. 8 new), CSharpier;
frontend 113 vitest green (14 new), lint, prettier, `ng build`. Contract and client regenerated
and committed; the PR's own `2 · API contract gate` run shows the additive change in report mode.

## Automated review (CodeRabbit, G2)

Four findings on the first push, all valid and fixed in the follow-up commit:

1. **Concurrent double submit could 500** — load-then-remove races two tabs into a
   `DbUpdateConcurrencyException`. Replaced by a set-based `DeleteByIdAsync`
   (`ExecuteDelete`), no existence check; 0 affected rows is success. Use case is now one call.
2. Two service tests renamed to `Method_Scenario_ExpectedResult` (spec table updated).
3. German strings switched to the formal "Sie" register used elsewhere in the app.
4. The challenge label said "e-mail address" even for accounts that type their display name —
   label now follows the same condition as the value (`challengeLabelName` in four locales,
   component test added).

## Outcome

- **Result:** PR opened against `develop`.
- **Deviations from spec:** none.
- **Harness follow-up:** the commit-msg hook rejects any uppercase in the subject (e.g. a spec
  id) — worth a note in the coding guidelines if it bites again; not changed here.
- **Follow-up outside this repo:** after the release that ships this, `store-it-deploy`
  `deployments/prod-oracle/public/privacy.html` changes "deletion on request" to self-service.
