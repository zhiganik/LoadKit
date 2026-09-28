# AGENTS.md (global)

## How to use this file

- This file is the navigation layer for AI agents and humans working on LoadKit.
- Detailed standards and architecture live in `docs/`.
- Load only the docs relevant to the current task (do not bulk-load everything).
- Documentation text is Russian; agent entry files (`AGENTS.md`, `CLAUDE.md`, `SKILL.md`) are English.

## Two AI audiences — do not mix them

| Audience | Who | Files |
|---|---|---|
| **Developing LoadKit** | agents working in this repo | `AGENTS.md`, `CLAUDE.md`, `docs/`, `.claude/skills/` |
| **Using LoadKit** | agents in *other* repos that run load tests | `ai/skills/loadtest/` (shipped inside the tool, installed via `loadtest ai install`) |

`ai/skills/loadtest/` is a product artifact, not instructions for this repo.
Edit it only when the scenario format or CLI behavior changes (see skill `changing-scenario-format`).

## What LoadKit is

A .NET console tool (`dotnet tool`, command `loadtest`) for local HTTP load testing.
Load is declared in a JSON scenario, not programmed. Output: p50/p95/p99, errors by status code,
Markdown/JSON reports, exit codes for thresholds. Background: `docs/decisions/ADR-001-own-load-engine.md`.

## Priority order (when sources conflict)

1. `AGENTS.md`
2. `docs/standards/*`
3. `docs/decisions/*` (accepted ADRs)
4. `docs/architecture/*`
5. `docs/user/*`, `ai/skills/loadtest/*`
6. Code (if docs are stale — update docs in the same PR)

## Repository map

```
AGENTS.md, CLAUDE.md, README.md
src/
  LoadKit.Core/          # scenarios, auth, engine, metrics, reporting — no console I/O
  LoadKit.Cli/           # commands, console rendering, DI; packs as dotnet tool
                         # embeds ai/skills/loadtest/** and schemas/ by link (no copy)
samples/
  TargetApi/             # local API with /fast /slow /fail /dep /secure for tests and demos
  scenarios/             # example scenarios (validated in CI)
tests/
  LoadKit.Core.Tests/    # unit tests
  LoadKit.IntegrationTests/  # CLI + TargetApi end-to-end
schemas/scenario.schema.json
ai/skills/loadtest/      # user-facing skill (SKILL.md + SCENARIO_REFERENCE.md)
.claude/skills/          # dev skills for this repo
docs/                    # see docs/README.md
```

## Project standards map

- Code style: `docs/standards/STYLE_CSHARP.md`
- Architecture rules: `docs/standards/ARCHITECTURE.md`
- Documentation rules: `docs/standards/DOCUMENTATION.md`
- Testing: `docs/standards/TESTING.md`
- Verification: `docs/standards/VERIFICATION.md`
- Commits: `docs/standards/COMMITS.md`

## Orientation docs

- `README.md` — human entrypoint.
- `docs/README.md` — documentation index.
- `docs/onboarding/AGENT_ONBOARDING.md` — read order by task type.
- `docs/architecture/OVERVIEW.md` — components and execution flow.
- `docs/PLAN.md` — implementation phases and current status.
- If task scope is unclear, read these before component docs.

## Stack (short)

- .NET 10 (LTS), C# latest, nullable enabled, warnings as errors
- `Spectre.Console` / `Spectre.Console.Cli` — commands and output
- `System.Text.Json`; own `ScenarioValidator` (no JSON Schema library; the schema file is for IDEs only)
- `Azure.Identity` — `azureIdentity` auth
- xUnit for tests

## Required coding principles

- Style rules: `docs/standards/STYLE_CSHARP.md`. Layering rules: `docs/standards/ARCHITECTURE.md`.
- `LoadKit.Core` never writes to the console. Progress goes through `IProgress<T>`, results are returned as objects.
- Hot path (request loop): no locks, no LINQ, no string parsing, no network calls for tokens.
- Secrets: only via `${env:...}` / `.env`. Never log, print or put secrets into reports.
- Dependencies: MIT / Apache-2.0 / BSD only. Check the license before adding a package.
  Known exclusions: NBomber v5+, FluentAssertions v8+, Newtonsoft.Json.Schema, JsonSchema.Net
  (commercial licenses, usage quotas or maintenance-fee EULAs).
- **Contract sync:** any change to the scenario format updates, in one PR: schema, model, validator,
  `ai/skills/loadtest/SCENARIO_REFERENCE.md`, sample scenarios, tests. Use skill `changing-scenario-format`.
- **Reuse before duplicate:** search `Core` for existing helpers (template parsing, env substitution,
  percentile calculation) before writing new ones.
- Explicit naming, small methods, no meaningless short names in new code.
- Update nearby tests/docs when behavior or conventions change.

## Verification policy

- Tests are mandatory for any change touching business logic in `LoadKit.Core`.
- For non-trivial changes run:
  - `dotnet build -warnaserror`
  - `dotnet format --verify-no-changes`
  - `dotnet test`
- Validation scope by change type:
  - Core logic (engine, metrics, auth, scenarios): build + format + tests; add/update a targeted test.
  - Scenario format: all of the above + docs example tests (JSON blocks in `SCENARIO_REFERENCE.md` are validated).
  - CLI output only: build + format + snapshot/integration test if output format changed.
  - Docs-only: `dotnet test --filter Category=Docs` + manual audit.
  - Refactor without behavior change: build + format + full test suite.
- Before non-trivial work do a short test audit: what is covered, what must be added now, what is deferred and why.
- Never run load against a non-localhost URL without explicit user confirmation.

Details: `docs/standards/TESTING.md`, `docs/standards/VERIFICATION.md`.

## Commit policy

- Format (details: `docs/standards/COMMITS.md`): `type(scope): short description`
  - types: `feat`, `fix`, `refactor`, `docs`, `test`, `build`, `chore`
- Scope is required, one per commit, English, imperative mood.
- Scopes: `cli`, `engine`, `auth`, `scenarios`, `metrics`, `reporting`, `schema`, `ai-skill`, `samples`, `docs`, `deps`, `build`.
- Forbidden: `fix bug`, `update code`, `misc changes`, `wip`.
- One logical task → one commit.

## Working chat report format

Final report for every implementation task:

1. Changed files
2. What was changed
3. Why it is safe / root cause found
4. Tests and checks run
5. Results of those commands
6. Confirmation that unrelated contracts were not changed (scenario format, CLI flags, exit codes, report schema)

For bugfix tasks additionally: how the bug was confirmed, diagnostic evidence, exact root cause, logic before/after.

## Audit output format

1. Findings ordered by severity, with file references.
2. Open assumptions/questions (only if blocking or risky).
3. Residual risks and next steps.

## Skills in this repo

- `.claude/skills/adding-auth-provider/` — adding a new `auth.type`.
- `.claude/skills/changing-scenario-format/` — any change to scenario fields, templates or validation rules.
