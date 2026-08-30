# Process Changelog

All substantive changes to the process subtree (`docs/process/`) are recorded here.
Copied instances in other projects can diff against the version noted in
[`PROCESS.md`](PROCESS.md).

## 1.0 — 2026-08-30

Initial extraction. The KAIFe L4 process was factored out of the pilot project's
`CLAUDE.md` and `.claude/agents/` into this project-independent subtree:

- `PROCESS.md` — gates, personas, orchestrator loop, branching, isolation/WIP,
  artifacts, compliance, permission tiers, metrics concept
- `PROJECT-INTERFACE.md` — the contract of conventional project paths
- `DEFINITION-OF-DONE.md` — Gate G3 made explicit
- `personas/` — five canonical personas with subagent frontmatter
- `templates/` — spec, agent-log, ADR, and pull-request templates
