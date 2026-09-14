# Definition of Done (Gate G3)

A work item is **done** when all of the following hold. The machine part is enforced
as required CI checks; the manual part is the PR checklist
([`templates/pull-request-template.md`](templates/pull-request-template.md)).

## Machine part (CI — required status checks)

- [ ] Build and all tests green (unit, service, integration, end-to-end where present)
- [ ] Coverage and test-effectiveness gates met (thresholds owned by the project's `docs/guidelines/test-guidelines.md`)
- [ ] Security scan, dependency & license review green; SBOM produced
- [ ] Quality gate (static analysis) green
- [ ] Architecture conformance gate green (structural debt = 0)
- [ ] Format check green
- [ ] Harness consistency green (`.claude/agents/` byte-identical to `docs/process/personas/`)

The concrete job set is the project's CI pipeline; jobs may be added, never silently
weakened. Exceptions (e.g. auto-merge for low-risk dependency updates) must be
documented in the project.

## Manual part (PR checklist)

- [ ] Gate G2 passed: automated AI review + human review with human attestation
- [ ] **Functionality tested by a human** on the running software — the change does what the spec (or issue) asks for, not only what the tests assert
- [ ] Agent run log created in `docs/agent-logs/` and linked in the PR
- [ ] Spec status updated: verification table filled, gate status current
- [ ] Process/project docs updated if the change touched process or structure

## Merge

- [ ] Only a **human** merges. No agent merges, ever.
