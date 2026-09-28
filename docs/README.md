# LoadKit documentation

> Status: ready

Documentation has three layers; on conflict the upper one wins (full order in `AGENTS.md`).

1. **Navigation** — `AGENTS.md`, `CLAUDE.md`.
2. **Rules and design** — `docs/`.
3. **Product for users** — `docs/user/`, `ai/skills/loadtest/`.

Formatting rules: `standards/DOCUMENTATION.md`.

## Index

### Onboarding
- `onboarding/AGENT_ONBOARDING.md` — what an agent reads for each task type.
- `onboarding/HUMAN_ONBOARDING.md` — developer quick start.

### Standards
- `standards/STYLE_CSHARP.md` — C# style, hot path.
- `standards/ARCHITECTURE.md` — layers, modules, public contracts, dependency licenses.
- `standards/DOCUMENTATION.md` — where to put a document, language, status, skills.
- `standards/TESTING.md` — test levels, mandatory tests, TargetApi.
- `standards/VERIFICATION.md` — verification commands, CI.
- `standards/COMMITS.md` — commit format.

### Architecture
- `architecture/OVERVIEW.md` — components, execution flow, solution structure.
- `architecture/SCENARIOS.md` — model, loading, validation, templates.
- `architecture/AUTH.md` — providers, token refresh, security.
- `architecture/ENGINE.md` — engine, measurement, percentiles, thresholds.
- `architecture/REPORTING_AND_CLI.md` — reports, commands, interactive init.
- `architecture/AI_INTEGRATION.md` — the `loadtest` skill, distribution, versioning.

### Decisions
- `decisions/ADR-001-own-load-engine.md` — own engine instead of NBomber, k6, etc.
- `decisions/ADR-002-console-first.md` — console first, Core kept separate.
- `decisions/ADR-003-ai-skill-distribution.md` — skill shipped inside the tool.

### For users
- `user/GETTING_STARTED.md` — first run and auth.
- `user/CLI_REFERENCE.md` — commands and flags.
- `../ai/skills/loadtest/SCENARIO_REFERENCE.md` — scenario format.

### Other
- `PLAN.md` — implementation phases and status.
- `glossary/TERMS.md` — terms.
