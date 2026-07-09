# Pull Request

**Spec:** [link to docs/specs/...]
**Agent run log:** [link to docs/agent-logs/...]

## Summary

[What does this PR change and why — one or two sentences.]

---

## Gate G2 · Review checklist

- [ ] Automated AI review passed (findings addressed or justified)
- [ ] Human review done — code understood, not just skimmed (no vibe coding)
- [ ] Implementation verified **against the spec's acceptance criteria** (not against the code)
- [ ] Tests derived from acceptance criteria, not from the implementation
- [ ] No architecture/layering violations, no constraint bypasses
- [ ] No new external dependencies without justification (ADR or comment below)

## Gate G3 · DoD checklist (manual part — pipeline covers the rest)

- [ ] Agent run log created and linked above
- [ ] `CLAUDE.md` / `docs/` updated if process or structure changed
- [ ] Spec status updated (verification table, gate status)
