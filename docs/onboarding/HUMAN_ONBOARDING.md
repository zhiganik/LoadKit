# Developer onboarding

> Status: draft

## Requirements

- .NET 10 SDK
- Azure CLI (to check `azureIdentity`)
- IDE: Rider / Visual Studio / VS Code with C# Dev Kit
- Optional: Claude Code or another AI agent

## First run

```bash
git clone <repo> && cd LoadKit
dotnet build
dotnet test
dotnet run --project samples/TargetApi                       # in a separate terminal
dotnet run --project src/LoadKit.Cli -- run samples/scenarios/smoke.json
```

## Local install as a tool

```bash
dotnet pack src/LoadKit.Cli -c Release -o ./artifacts
dotnet tool install -g LoadKit --add-source ./artifacts
```

## What to read

1. `README.md` — why the tool exists.
2. `docs/decisions/*` — why it is built this way.
3. `docs/architecture/OVERVIEW.md` — how it works.
4. `AGENTS.md` — rules for code, tests and commits.
5. `docs/PLAN.md` — what we are doing now.

## Working with an AI agent in this repository

`CLAUDE.md` includes `AGENTS.md`. The `/adding-auth-provider` and `/changing-scenario-format` skills
are available in Claude Code. Ask the agent: "take the next task from phase 2 of PLAN.md" — it will find
the relevant documents via `AGENT_ONBOARDING.md`.
