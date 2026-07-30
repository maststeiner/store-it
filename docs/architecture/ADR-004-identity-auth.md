# ADR-004: Identity & authentication (direct OIDC federation, BFF)

> **Status:** Proposed
> **Date:** 2026-07-30
> **Deciders:** Marcel Steiner

---

## Context

store-it has had no user concept: SPEC-001 shipped a walking skeleton where *all
storages are globally visible* (an explicit MVP simplification). SPEC-003 removes that
by introducing accounts and per-storage ownership, which requires an identity solution.
This ADR is the long-standing `TODO` in `ARCHITECTURE.md` (Technical Context, Security)
and GitHub issue #16, and it directly mitigates threat-model risk **R-06 (Broken access
control / no authn-authz)**.

Constraints and forces:
- The product owner does not want to build or operate an own identity provider or manage
  passwords. Users should sign in with accounts they already have.
- store-it is API-first (ADR-002): a web app today, a native iPhone app later.
- Single-developer pilot under KAIFe; low operational surface is preferred over maximal
  flexibility. Data residency (CH/EU) is a background concern.
- Security posture matters — R-06 is the highest open risk in the threat model.

## Decision

1. **Federated sign-in only, no own IdP.** Authentication is delegated to external OIDC
   providers: **Microsoft and Google** initially, **Apple** added later (additive).
2. **Direct OIDC federation, no identity broker.** The .NET API integrates each provider
   directly as an OIDC confidential client (one OpenID Connect handler per provider). No
   intermediate broker (no Keycloak / Entra External ID / Auth0).
3. **Backend-for-Frontend (BFF) session.** The authorization-code (+ PKCE) exchange runs
   **server-side**; the API issues a secure **HttpOnly, SameSite** session cookie. The
   browser holds **no access/ID tokens**. Endpoints: `/auth/login/{provider}`,
   `/auth/callback/{provider}`, `POST /auth/logout`, `GET /auth/me`.
4. **Local user, JIT-provisioned.** A local `User` (GUID PK) is created on first login,
   keyed by the OIDC **`(Issuer, Subject)`** pair (a `sub` is only unique per issuer, and
   we federate several issuers directly). Ownership FKs point at the local `User.Id`, not
   at an IdP identifier — the app is decoupled from the IdP.
5. **Ownership enforced at the data layer.** Per-storage ownership is applied via an EF
   Core **global query filter** (`OwnerId == currentUser.Id`), so isolation is the
   default for every read rather than a per-handler concern (see SPEC-003 for the ACs).

## Rationale

**Why federated / no own IdP:** avoids storing credentials and the entire password/MFA/
account-recovery burden — the largest part of R-06 — and matches the PO's explicit wish.

**Why direct federation over a broker (the reversible part):** a broker (Keycloak,
Entra External ID, Auth0) would collapse three provider integrations into one and hide
provider quirks (notably Apple's rotating client secret). It was the initial preference.
It was rejected for the pilot because it adds a component to **host, secure, upgrade and
back up** (self-hosted Keycloak) or an external dependency + potential cost and data-
residency questions (managed). With only two providers now, integrating OIDC directly in
ASP.NET Core is modest and removes that operational surface entirely. This is deliberately
**revisitable**: if provider count or account-linking needs grow, a broker can be
introduced behind the same BFF without touching the ownership model — a future ADR would
supersede this point.

**Why BFF over SPA bearer tokens:** keeping tokens out of the browser is the strongest
mitigation against XSS token theft (R-06). The SPA only ever holds an HttpOnly cookie it
cannot read. Cost: the API is stateful w.r.t. the session and must handle CSRF
(SameSite + anti-forgery on state-changing calls).

**Why key on `(Issuer, Subject)`:** OIDC guarantees `sub` uniqueness only per issuer;
federating Google and Microsoft directly means multiple issuers, so `sub` alone can
collide. The same person using two providers is intentionally two accounts (account
linking is out of scope — SPEC-003 EC-03).

## Consequences

**Positive:**
- No credential storage, no own IdP, no broker infrastructure to operate.
- Tokens never reach the browser (R-06 downgraded from "Not yet addressed" to mitigated).
- Ownership isolation is enforced centrally at the data layer, hard to forget.
- Local `User` decouples the domain from IdP identifiers and pre-stages future sharing.

**Negative / Trade-offs:**
- The app owns each provider's OIDC integration and quirks directly — most notably
  **Apple's client secret is a signed JWT that must be rotated (≤6 months)** when it lands.
- Adding future providers means new handlers + app registrations (linear cost a broker
  would have amortized).
- Stateful sessions + CSRF handling on the backend (BFF tax).
- No cross-provider account linking (accepted for now).

**Tech debt / follow-ups:**
- Revisit a broker if providers proliferate or account-linking becomes required.
- Provider-side (RP-initiated) logout is deferred; only the local session is ended today.

## Layering Rules (for the Architecture Conformance Gate)

```
Authorization / ownership filtering MUST live in the Application/Infrastructure layer.
The Api layer and the Angular frontend MUST NOT implement ownership rules (ADR-001, ADR-002).
```

---

*Referenced by SPEC-003 (accounts & storage ownership). Resolves issue #16 and the
`ARCHITECTURE.md` auth TODOs; mitigates threat-model R-06. A human decider moves this ADR
from Proposed to Accepted.*
