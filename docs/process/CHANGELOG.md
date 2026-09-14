# Process Changelog

All substantive changes to the process subtree (`docs/process/`) are recorded here.
Copied instances in other projects can diff against the version noted in
[`PROCESS.md`](PROCESS.md).

## 1.0 — 2026-08-30

Initial extraction. The process was factored out of the pilot project's `CLAUDE.md`
and `.claude/agents/` into this project-independent subtree and renamed from its
internal framework name to **AI-Dev Process**:

- `PROCESS.md` — gates (G3 now includes human functional testing), workflow /
  lifecycle of a work item, personas, orchestrator loop, branching as a reference
  model, isolation with the WIP limit as a project-overridable recommendation,
  artifacts, compliance, permission tiers, metrics concept
- `PROJECT-INTERFACE.md` — the contract of conventional project paths
- `DEFINITION-OF-DONE.md` — Gate G3 made explicit, incl. human functional testing and
  the CI check that `.claude/agents/` mirrors `personas/`
- `personas/` — five canonical personas with subagent frontmatter
- `templates/` — spec, agent-log, ADR, and pull-request templates
