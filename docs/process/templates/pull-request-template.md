<!--
  Generic master. Instantiate as .github/pull_request_template.md and extend the
  human attestation with the project-specific invariants named in the project's
  coding guidelines. Keep the gate structure intact.
-->
# Pull Request

**Spec:** [link to docs/specs/... — or the issue that is the frozen G1 input]
**Agent run log:** [link to docs/agent-logs/...]

## Summary

[What does this PR change and why — one or two sentences.]

---

## Gate G2 · Review checklist

- [ ] Automated AI review passed — findings fixed, or deferred as a `tech-debt` issue (never just closed)
- [ ] Layering is machine-checked (CI architecture gate) — all gates green
- [ ] Implementation verified **against the spec's acceptance criteria** (verification table filled)
- [ ] Tests derived from acceptance criteria, not from the implementation
- [ ] No new external dependencies without justification (ADR or comment below)

**Human attestation (must be ticked by a person, never by AI — Gate G2, Principle 3):**

- [ ] Human review done — code understood, not just skimmed (no vibe coding)
- [ ] Manually verified what machines can't: the project-specific invariants named in the project's coding guidelines, and no spec constraints bypassed

## Gate G3 · DoD checklist (manual part — pipeline covers the rest, see docs/process/DEFINITION-OF-DONE.md)

- [ ] Agent run log created and linked above
- [ ] Process/project docs updated if process or structure changed
- [ ] Spec status updated (verification table, gate status)
