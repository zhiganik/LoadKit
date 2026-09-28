# Agent onboarding

> Status: ready

Always: `AGENTS.md` (already in context via `CLAUDE.md`). Beyond that, only what the task type needs.

| Task type | Read |
|---|---|
| Unclear context | `docs/architecture/OVERVIEW.md`, `docs/PLAN.md` |
| Core logic | `docs/standards/ARCHITECTURE.md`, `STYLE_CSHARP.md`, the component document in `docs/architecture/` |
| New auth type | skill `adding-auth-provider`, `docs/architecture/AUTH.md` |
| Scenario format | skill `changing-scenario-format`, `docs/architecture/SCENARIOS.md` |
| Engine, percentiles | `docs/architecture/ENGINE.md`, `docs/standards/TESTING.md` |
| CLI, reports | `docs/architecture/REPORTING_AND_CLI.md`, `docs/user/CLI_REFERENCE.md` |
| User-facing skill | `docs/architecture/AI_INTEGRATION.md`, `ai/skills/loadtest/*` |
| Tests | `docs/standards/TESTING.md`, `VERIFICATION.md` |
| Documentation | `docs/standards/DOCUMENTATION.md` |
| Commit | `docs/standards/COMMITS.md` |

## Source of truth

`AGENTS.md` → `docs/standards` → accepted ADRs → `docs/architecture` → `docs/user`, skill → code.
If a document and the code disagree, report it and fix the document in the same PR.

## Before a non-trivial task

1. Find the phase in `docs/PLAN.md`.
2. A short change plan and test audit in the chat.
3. Check whether a public contract is affected (`docs/standards/ARCHITECTURE.md`).
