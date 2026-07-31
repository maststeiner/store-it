# ADR-007: Release process, SemVer tagging, and the breaking-change baseline

> **Status:** Accepted
> **Date:** 2026-07-31
> **Deciders:** Marcel Steiner

---

## Context

ADR-006 introduced URL path versioning (`/api/v1`) and a two-stage contract gate (drift + breaking). The breaking check compares the PR's OpenAPI spec against the **target branch** (`develop`). Because `develop` already carries the v1 contract, this blocks *any* breaking change to v1 — **even now, while v1 has never been released and no client depends on it.**

That is too strict for a pre-release API. The intent of ADR-006 is to protect *published* clients (the web app in production, the planned iPhone app). Before the first release, the contract is still being shaped and breaking changes must stay cheap. There is currently **no release process, no tags, and `main` carries no contract** (0 tags, `main` is still the scaffold).

## Decision

1. **A release is a `develop` → `main` merge, marked by an annotated SemVer tag `vMAJOR.MINOR.PATCH`.** `main` is the released line (GitFlow-light) — this is distinct from SonarCloud's *analyzed* branch, which is `develop`. **Release tags are protected** (a GitHub `v*` tag-protection rule): since the breaking-change gate reads the contract at the latest tag, that tag must not be movable or deletable. The CI selects the baseline strictly — an annotated `vMAJOR.MINOR.PATCH` tag reachable from `main`.
2. **Pre-release semantics (SemVer `0.x`): while no `v*` tag exists, `/api/v1` is unfrozen** — breaking changes to it are allowed. The first stable tag **`v1.0.0`** freezes the released `/api/v1` contract; from then on a breaking change requires a new path version (`/api/v2`, per ADR-006).
3. **The breaking-change baseline is the OpenAPI contract at the latest release tag**, not `develop`. This refines ADR-006 stage 2:
   - No `v*` tag yet → skip the breaking check (pre-release, breaking allowed).
   - A tag exists → `oasdiff breaking <contract@latest-tag> <PR-contract> --fail-on ERR`.
   - The **drift check is unchanged** — the committed contract must always match the code, released or not.
4. **Three distinct version concepts** (kept separate to avoid confusion):
   - **API path version** (`/api/v1`, `/api/v2`) — in the URL; changes only on a breaking change *after* a stable release.
   - **OpenAPI `info.version`** — document metadata for the current API version (v1 → `1.x`); evolves additively within v1; a `/api/v2` document starts at `2.0.0`.
   - **Product release version** — the git tag SemVer of what was shipped; the source of truth for "what is released."

## Rationale

- Keeps breaking changes cheap while the API is unpublished, then makes compatibility machine-enforced the moment a release is cut — exactly when it starts to matter.
- Ties the gate to an explicit, immutable marker (the tag) rather than the moving integration branch.
- Reuses the existing GitFlow-light + `main`-as-released model; no new infrastructure.
- The first `v1.0.0` release is a deliberate human act (freezing v1), not an accident of merging.

## Consequences

**Positive:** breaking changes are free until the first release; after `v1.0.0` the contract is protected against silent breaks; the release/versioning policy is explicit.

**Negative / Trade-offs:** the first stable release must be chosen deliberately (it freezes `/api/v1`); the breaking check is a no-op until then, so contract discipline pre-1.0 relies on review (Gate G2) rather than the gate. Releases require tagging discipline (an annotated `vX.Y.Z` per `develop` → `main` merge).

## Relationship to other ADRs

- **Refines ADR-006** (baseline: last release tag instead of `develop`; the rest of ADR-006 stands).
- Release *deployment* (how a tagged release reaches Kubernetes) is out of scope here and belongs to **ADR-005**.
