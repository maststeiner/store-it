---
name: write-adr
description: Use when a decision about store-it's architecture, tooling, or cross-cutting approach needs to be recorded or changed — writing a new ADR, moving one through its status lifecycle, or superseding one. Keeps store-it's decision records consistent and discoverable.
---

# Writing an ADR for store-it

Architectural Decision Records live in `docs/architecture/` and are the durable
record of *why* store-it is built the way it is. Use this skill whenever a
non-trivial, hard-to-reverse decision is made (or revised).

## Steps

1. **Pick the number.** Look at existing `docs/architecture/ADR-*.md`; take the next
   free `NNN` (zero-padded, e.g. `004`). Never reuse or renumber.
2. **Create the file** `docs/architecture/ADR-NNN-<slug>.md` where `<slug>` is a short
   kebab-case summary (e.g. `ADR-004-identity-auth.md`). Copy `ADR-TEMPLATE.md` as the
   starting point.
3. **Fill the header:**
   - `Status:` — start at `Proposed`. Use `Research` while actively exploring options
     (spikes, comparisons). Move to `Accepted` once the human decider signs off. Use
     `Deprecated` or `Superseded by ADR-NNN` when it no longer holds — never delete an ADR.
   - `Date:` — the decision date (YYYY-MM-DD). `Deciders:` — the human(s) accountable.
4. **Write the body** (template sections): **Context** (the forces, honestly), **Decision**
   (stated directly), **Rationale** (why this, why not the alternatives), **Consequences**
   (positive · negative/trade-offs · tech-debt). If the ADR defines layer boundaries, add
   the machine-checkable rules under **Layering Rules** so the architecture gate can enforce them.
5. **Link it into the register.** Update the ADR table in
   `docs/architecture/ARCHITECTURE.md` (status + link). If the ADR unblocks or supersedes
   other work, cross-reference it.
6. **A decision is a human gate.** An agent drafts an ADR as `Proposed`/`Research`; only a
   human moves it to `Accepted`. Don't self-accept.

## Conventions

- One decision per ADR. Big decisions may get their own folder with exploration/spike notes,
  but the `Accepted` outcome always lands in a numbered `ADR-NNN-*.md`.
- Keep it short and honest — record the trade-offs and the rejected alternatives, not just
  the winner. An ADR that hides the downsides is not useful later.
- ADRs are immutable once `Accepted`: to change a decision, write a new ADR that supersedes it
  and flip the old one's status to `Superseded by ADR-NNN`.
