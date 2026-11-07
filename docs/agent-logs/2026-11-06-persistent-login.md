# Agent Run Log: stay signed in — persistent sessions and a durable key ring

> **Date:** 2026-11-06
> **Spec:** none — tech-debt item without an issue (Marcel: "Ein Issue braucht es dafür nicht"); the
> decision below, taken in chat, is the frozen G1 input
> **Persona(s):** architect → developer → qa
> **Model:** Claude Fable 5.1 (claude-fable-5-1)
> **Branch / PR:** `tech-debt/persistent-login` → PR against `develop`

---

## Task

Marcel: "Gibt es eine Möglichkeit, dass ich mich nicht jedes Mal auf der App einloggen muss?" —
then, after the trade-off discussion: "ok, bleiben wir dabei. Dies direkt umsetzen."

## Findings (why the sign-in did not stick)

Two independent causes, both in the backend / deployment, none in the client:

1. **The session was a browser-session cookie.** `AuthEndpoints.HandleLogin` challenged without
   `IsPersistent`, so the cookie had no `Expires` and every browser close ended the session —
   although the server-side ticket would have lived 14 days.
2. **Every release threw everyone out.** The data-protection key ring was never persisted: it
   lived in the container file system under the `storeit` user's home. `compose up` recreates
   the `backend` container on every image change, the keys were gone, and no existing session
   or CSRF cookie could be decrypted any more. Fix 1 alone would have changed nothing on release
   day.

## Decision (G1, in chat)

| # | Decision | Why |
|---|---|---|
| D1 | *Stay signed in* is an **opt-in** checkbox on the sign-in page (`?persistent=true` on `/auth/login/{provider}`), **off by default** | the user of a shared device decides; the default stays a browser session |
| D2 | **Sliding 14 days, absolute 30 days** from sign-in — for every session, persistent or not | sliding alone extends forever; the cap bounds a stolen or forgotten cookie |
| D3 | The cap is a **pure ticket check** (`OnValidatePrincipal` reads a sign-in stamp from the ticket); **no per-request database check** | SPEC-003 "pure claim read" and SPEC-006 D6 stand; a stale session is already handled there (empty reads, `401 auth.session.stale` on the first write). The "user still exists" check I had proposed in chat was dropped for this reason — D6 delivers the outcome without the cost |
| D4 | **Key ring in PostgreSQL** (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, table `data_protection_keys`, `SetApplicationName("store-it")`) | no new volume, no deploy-repo change, the nightly `pg_dump` covers it; a restore restores sessions |
| D5 | Keys **stored unencrypted** (framework warning at startup accepted) | the database is reachable from the compose network only and already holds what the keys protect; `ProtectKeysWithCertificate` is the documented step up |
| D6 | **No refresh tokens** | the app calls no provider API; `offline_access` would add a consent text, Google's 7-day testing expiry and a credential more valuable than the cookie |

Recorded as an amendment in [ADR-004](../architecture/ADR-004-identity-auth.md) and in the
[runtime contract](../operations/runtime-contract.md) §2.5.

## Implementation notes

- **Clock.** The cookie handler measures expiry with its own `TimeProvider` (system by default).
  It now gets the application's `TimeProvider` via `AddOptions<CookieAuthenticationOptions>(…).Configure<TimeProvider>`
  so the service tests can let days pass (`DevLoginFixture.Clock`, a `FakeTimeProvider`). The
  fake clock starts at the *real* time on purpose: the test client's cookie jar judges `Expires`
  against the real clock, and a cookie dated in the past is silently never sent.
- **Build-time OpenAPI generation.** `AddDataProtection()` registers a hosted service that preloads
  the key ring at host start; the document generator starts the host without a database and the
  preload logged `fail: … An error occurred while reading the key ring` into every build. The
  key-ring store is therefore skipped in exactly that context
  (`AddInfrastructure(persistDataProtectionKeys: !StartupConfigurationCheck.IsBuildTimeDocumentGeneration)`).
- **Fixtures.** `ForwardedHeadersFixture` started PostgreSQL but never migrated — the OIDC challenge
  seals its state with data protection, which now needs the table, so the fixture migrates like
  the others. The fixture hosts still log one key-ring failure at start (host up before
  `MigrateAsync`); harmless, the provider retries on first use.
- **`POST /auth/dev-login?persistent=true`** mirrors the opt-in so the lifetime rules are testable
  end-to-end without an IdP; the challenge properties of the real login are asserted on the
  `ChallengeHttpResult` (`HandleLogin` is `internal` now) because over HTTP they are sealed inside
  the OIDC `state`.
- Contract: one optional boolean query parameter on `login`; client regenerated.

## Verification

| Check | Result |
|---|---|
| `dotnet build` (solution) | clean, 0 warnings, no key-ring failure in the OpenAPI step |
| `dotnet test` (solution, Testcontainers) | 277 passed — incl. 2 challenge-property tests, 2 HTTP challenges whose sealed `state` is unprotected to assert `IsPersistent` (route binding, after a CodeRabbit remark), 6 lifetime/key-ring tests (browser-session cookie has no `Expires`; persistent cookie expires at +14 d; renewal after 10 d; 401 + expired cookie at day 31 despite use; cap also for browser sessions) |
| Stryker (local, `--mutate` on the five changed source files) | 66 % before the key-table test — above the 60 % break; the survivors are the known ones (OIDC callback has no coverage without an IdP, option-lambda attribution to the first test, `<=`→`<` boundary) |
| `npm run lint` · `npm run test:coverage` · `npm run build` | clean · 227 passed · clean |
| `prettier --check` · `csharpier` | clean |
| Contract drift (`npm run generate:api`) | regenerated, committed |

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "Ist die Security höher oder schlechter?" — answered: slightly lower, acceptable with opt-in, cap and server-side revocation | risk acceptance before implementation |
| 2 | "Renew tokens?" — answered: not for this app (D6) | scope |
| 3 | "Dies direkt umsetzen", no issue | process: chat decision as G1 input |
| 4 | "Schlüssel zusätzlich verschlüsseln, z.B. transparent?" — answered in the PR / chat: PostgreSQL has no TDE; the OCI block volume is encrypted at rest; app-level `ProtectKeysWithCertificate` would need a certificate outside the database (D5) | left out of this PR by recommendation |

## Outcome

- **Result:** PR open against `develop`; G3 by Marcel on the running software (sign in with the box
  ticked, close and reopen the browser, still signed in; then deploy a release and still be signed in).
- **Deviations from the chat proposal:** the "user still exists" check (D3) — see table.
- **Harness follow-up:** none.
