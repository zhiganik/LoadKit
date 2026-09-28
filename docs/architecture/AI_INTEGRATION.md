# AI assistant integration

> Status: draft. Decision: ADR-003.

## Two audiences

| Audience | Where they work | What they read |
|---|---|---|
| LoadKit developers | this repository | `AGENTS.md`, `CLAUDE.md`, `docs/`, `.claude/skills/*` |
| LoadKit users | their own repositories | the `loadtest` skill, installed with `loadtest ai install` |

## The `loadtest` skill

Source: `ai/skills/loadtest/`:
- `SKILL.md` — when to apply, workflow, safety rules, how to read the report;
- `SCENARIO_REFERENCE.md` — the full format reference; examples are checked by tests.

The format is the open Agent Skills standard. In Claude Code, a skill in `.claude/skills/loadtest/` becomes
the `/loadtest` command and is invoked by the model automatically based on its `description`.

## Distribution

The skill is built into the tool: `LoadKit.Cli.csproj` includes the `ai/skills/loadtest/**` files as embedded
resources by link (`<EmbeddedResource Include="..\..\ai\skills\loadtest\**" LinkBase="AiAssets" />`).
There is no copy in the repository, so there is nothing to sync, and the skill version always matches the
CLI and scenario format versions. `schemas/scenario.schema.json` is embedded the same way (for `init`).

```bash
loadtest ai install                     # → ./.claude/skills/loadtest/ (Claude Code, project)
loadtest ai install --global            # → ~/.claude/skills/loadtest/
loadtest ai install --dir <path>        # → any skills directory of another AI tool
loadtest ai install --agents-md         # + a short block in the project's AGENTS.md
loadtest ai status                      # installed skill version vs tool version
```

`--agents-md` is a universal fallback: it adds a block to the project's `AGENTS.md` between the
`<!-- loadkit:start -->` / `<!-- loadkit:end -->` markers with a link to the skill and the three main rules
(scenarios only, secrets via `.env`, confirmation for remote URLs). Running it again
updates the block without duplicating it.

Paths for other AI tools are checked against their documentation; LoadKit does not guess them and accepts `--dir` instead.

## Versioning

`metadata` in `SKILL.md` contains `loadkit-version` and `scenario-format-version`.
- `ai status` and `validate` warn if the installed skill is older than the tool.
- A breaking format change → bump `scenario-format-version` (skill `changing-scenario-format`).

## Usage example

```
User: /loadtest load order creation, 30 concurrent, 2 minutes
Agent: finds the routes in code → writes loadtests/scenarios/orders-create.json →
       validate → asks to add API_TOKEN to .env → check → run → summarizes report.md
```

## Checking skill quality

A skill is code too. Before a release it is checked with agent scenarios (see `docs/PLAN.md`, phase 5):
baseline behavior without the skill → behavior with the skill → closing the loopholes found.
Typical failures that must be closed: the agent writes a load script instead of a scenario, puts
a token into JSON, skips `check`, runs load against a remote URL without confirmation.
