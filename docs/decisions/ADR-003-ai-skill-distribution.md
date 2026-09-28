# ADR-003: The AI skill ships inside the tool

- **Status:** accepted (ready)
- **Date:** 2026-09

## Context

The goal: a user can tell an AI assistant "run a load test on X" or invoke `/loadtest`,
and the assistant correctly writes a scenario, checks it and runs it. The assistant must know the scenario
format and the safety rules, and the version of that knowledge must match the tool version.

## Options considered

1. **Documentation in the README** — the assistant may not find it or may read an outdated version.
2. **A separate skills repository** — its version drifts from the tool version.
3. **Skill inside the tool + an install command** — the version always matches; one-command install.

## Decision

Option 3. The source is `ai/skills/loadtest/`; at build time it is embedded into `LoadKit.Cli` by link.
The `loadtest ai install` command copies the skill into the project or globally; `--agents-md` adds
a block to `AGENTS.md` for tools that do not support skills.

The skill format is the open Agent Skills standard (frontmatter uses only spec fields),
so one file works in Claude Code and other compatible tools.

## Consequences

- The skill is embedded into the build by linking `ai/skills/loadtest/**`; there is no copy in the repository.
- A scenario format change must update the skill (skill `changing-scenario-format`).
- The skill is tested with agent scenarios before a release.
