@AGENTS.md

# Claude Code notes

`AGENTS.md` above is the single source of truth for rules. This file only adds Claude Code specifics.
Do not duplicate rules here — change `AGENTS.md` instead.

## Workflow

1. Read `docs/onboarding/AGENT_ONBOARDING.md` for the read order matching the task type.
2. Check `docs/PLAN.md` to see which phase the task belongs to and what is already done.
3. For non-trivial tasks: propose a short plan and a test audit before editing code.
4. Implement, then run the verification commands from `AGENTS.md`.
5. Finish with the report format from `AGENTS.md`.

## Commands

```bash
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
dotnet test --filter "FullyQualifiedName~PercentileCalculator"   # single class/test
dotnet test --filter Category=Docs                         # docs example tests only
dotnet run --project samples/TargetApi                      # local target on http://localhost:5080
dotnet run --project src/LoadKit.Cli -- validate samples/scenarios/smoke.json
dotnet run --project src/LoadKit.Cli -- run samples/scenarios/smoke.json
```

Before Phase 0 of `docs/PLAN.md` is done, the solution may not exist yet and these commands will fail.
Check `PLAN.md` first rather than assuming the code in `docs/architecture/` exists.

## Non-obvious things

- Integration tests host `samples/TargetApi` on real Kestrel with port 0, not `WebApplicationFactory`:
  the CLI makes real network calls, and TestServer only works in memory.
- CLI exit codes are a contract: 0 ok, 1 thresholds failed, 2 validation, 3 preflight (token/baseUrl),
  4 non-localhost URL without a terminal and without `--yes`, 130 Ctrl+C.
- `secret-literal` validation runs **before** `${env:}` substitution (after substitution, the secret is already in the value).
- `schemas/scenario.schema.json` is only for IDE autocompletion. Validation is done by `ScenarioValidator`.

## Project skills

- `/adding-auth-provider` — use when adding or changing an `auth.type`.
- `/changing-scenario-format` — use for any scenario format change.

## Dogfooding the user skill

`ai/skills/loadtest/` is NOT a project skill of this repo. To try it as a user would:

```bash
mkdir /tmp/lk-demo && cd /tmp/lk-demo
dotnet run --project <repo>/src/LoadKit.Cli -- ai install
```

Then open Claude Code in `/tmp/lk-demo` and use `/loadtest`.

## Safety

- Load only `localhost` / `samples/TargetApi` unless the user explicitly confirms another URL.
- Never read `.env` values into the conversation; refer to variable names only.
