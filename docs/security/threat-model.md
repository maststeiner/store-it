# store-it — Threat Model

> **Owner:** Marcel Steiner (Architecture / Security Stewardship)
> **Scope:** the store-it application, its repository, and its CI/CD supply chain
> **Last updated:** 2026-07-28

This is a living risk register. Its purpose is not to list tools, but to name the
risks store-it must cover — and make explicit **which control addresses which risk**
and **which risks nothing covers yet**. store-it is a public GitHub repository
(.NET 10 API + Angular client + PostgreSQL, deployed via GitHub Actions).

**Likelihood / Impact:** L / M / H. **Status:** ✅ mitigated · 🟡 partial · ⚪ accepted/deferred.

---

## 1. Application & data risks

| ID | Risk | Impact | Likelihood | Mitigation in store-it | Status |
|----|------|--------|-----------|------------------------|--------|
| R-01 | Invalid/malicious input reaches the domain | M | M | Server-side validation in the domain (never the client); API-first per ADR-002; validation ACs covered by tests | ✅ |
| R-02 | Business rules bypassed via the client | M | M | Client is render-only; all rules server-side; architecture gate (NetArchTest) forbids logic leaking outward | ✅ |
| R-03 | SQL injection | H | L | EF Core parameterized queries only; no string-built SQL | ✅ |
| R-04 | Sensitive data in logs / error responses | M | M | Structured logs to stdout; domain exceptions mapped to sanitized responses (`DomainExceptionHandler`); no PII in test data (synthetic-only policy) | 🟡 |
| R-05 | Secrets committed to the repo | H | M | Env-based config (12-factor); no secrets in repo; **PreToolUse guardrails block agents from reading/editing secret files**; Trivy secret scan | ✅ |
| R-06 | Broken access control / no authn-authz | H | H | **Not yet addressed** — store-it has no user/auth concept. Deferred to ADR-004 (identity/auth). Until then the app is single-tenant/unauthenticated by design | ⚪ |
| R-07 | Denial of service (resource exhaustion) | M | L | No app-level rate limiting yet; owner concern at the ingress/hosting layer (ADR-005) | ⚪ |
| R-08 | CORS / API surface misconfiguration | M | L | API surface is explicit (minimal APIs, typed contracts); revisit CORS with the first real client deployment | 🟡 |

## 2. Supply-chain & CI/CD risks

| ID | Risk | Impact | Likelihood | Mitigation in store-it | Status |
|----|------|--------|-----------|------------------------|--------|
| R-09 | Vulnerable dependency (known CVE) | H | M | Trivy scan (repo-wide) + Dependabot alerts (continuous); dependency-review-action on PR diff | ✅ |
| R-10 | Incompatible / copyleft OSS license slips in | M | M | License policy (MIT + permissive only): Trivy license scan + dependency-review-action block GPL/AGPL/LGPL/SSPL | ✅ |
| R-11 | Dependency version drift across projects | L | M | **Central Package Management** (`Directory.Packages.props`) — one version per package, no per-project drift | ✅ |
| R-12 | Malicious change injected via CI/CD | H | L | Least-privilege workflow permissions (`contents: read`); pinned action SHAs; branch protection + required checks; human merge gate (G3) | ✅ |
| R-13 | Unreviewed change reaches a protected branch | H | L | Branch protection on `main`/`develop`; conversation-resolution-required; CodeRabbit + human review (Gate G2) | ✅ |
| R-14 | Silent breaking API change | M | M | oasdiff contract gate (drift + breaking-change classification) on every PR | ✅ |

## 3. AI-development-specific risks

store-it is built with AI agents (KAIFe L4). That adds a risk class most threat models omit:

| ID | Risk | Impact | Likelihood | Mitigation in store-it | Status |
|----|------|--------|-----------|------------------------|--------|
| R-15 | AI-generated code carries security flaws (studies report 30–50%) | H | M | CodeRabbit adversarial review; SonarCloud security hotspots; human review gate (G2) | 🟡 |
| R-16 | Tests that look plausible but assert nothing | M | M | Stryker.NET mutation testing (backend, break < 60%); isolated QA persona reads the spec, never the code; assertion-minimum policy (test-guidelines) | ✅ |
| R-17 | Code duplication from generation (~8× per industry data) | L | M | SonarCloud duplication detection in the quality gate | ✅ |
| R-18 | Architecture erosion via AI edits | M | M | NetArchTest + ReferencesRuler architecture gate blocks layering violations on every build | ✅ |
| R-19 | License contamination via generated code | M | L | Same controls as R-10 (license scans); permissive-only policy | 🟡 |
| R-20 | Agent exfiltrates secrets or acts unprompted | H | L | PreToolUse guardrails: deny secret reads (bash + file), require human sign-off on every commit | ✅ |

---

## Not covered / owner responsibility

Some concerns are the operator's, not the app's — store-it's job is to **not block** them:

- **At-rest / in-transit encryption** — provided by the hosting/DB layer (ADR-005 pending).
- **Auth & fine-grained authorization** — deferred to ADR-004; no user concept exists yet.
- **DoS / rate limiting / WAF** — expected at the ingress layer (ADR-005).
- **Secret management / rotation** — env-injected at deploy time; the repo only guarantees no secret is committed.

Review this register whenever a new spec adds a data flow, a dependency, or a deployment surface.
