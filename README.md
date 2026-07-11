# store-it

Digital pantry management: know what's in your pantry or freezer — and when it expires.

**store-it** lets you track the contents of a storage (pantry, freezer, cellar shelf): add items, remove what you consume, and see at a glance what expires soon. Storages can be shared across accounts, so a family or flat-share manages the same pantry together.

## Stack

| Layer | Technology |
|-------|------------|
| Backend | .NET (C#), API-first REST |
| Frontend | Angular (TypeScript) |
| Mobile | iPhone app (planned, consumes the same API) |
| Runtime | Cloud-native, Kubernetes |
| DevOps | GitHub + GitHub Actions |

## Repository structure

| Path | Purpose |
|------|---------|
| `backend/` | .NET solution (API) — *scaffold pending* |
| `frontend/` | Angular app — *scaffold pending* |
| `docs/` | Specs, arc42 architecture doc, ADRs, guidelines, agent logs |
| `.claude/` | KAIFe agent personas + permission tiers |
| `.github/workflows/` | CI: DoD gates (build/test, security+SBOM, quality, architecture, format) |

## Development process

This project follows the **KAIFe Framework (L4)** — AI-driven development with three non-negotiable human gates:

1. **G1 · Spec Freeze** — every work item starts as a spec in `docs/specs/`, frozen by a human
2. **G2 · Review** — automated + human code review on every PR
3. **G3 · DoD/Merge** — CI fully green; only a human merges

See `CLAUDE.md` for orchestration rules and `docs/SETUP.md` for the setup checklist.
