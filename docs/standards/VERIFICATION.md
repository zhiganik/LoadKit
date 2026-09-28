# Verification

> Status: ready

## Commands

```bash
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

Fast variants:

```bash
dotnet test --filter Category!=Integration   # unit + docs only
dotnet test --filter Category=Docs           # documentation examples only
```

## What to run by change type

| Change | build | format | tests |
|---|---|---|---|
| Core logic | ✓ | ✓ | full suite + new targeted test |
| Scenario format | ✓ | ✓ | full suite, including Docs |
| CLI output | ✓ | ✓ | integration |
| Documentation only | — | — | `Category=Docs` + manual audit |
| Refactoring | ✓ | ✓ | full suite |

## CI

GitHub Actions / Azure Pipelines on every PR:
1. `dotnet build -warnaserror`
2. `dotnet format --verify-no-changes`
3. `dotnet test` (including integration)
4. `loadtest validate samples/scenarios/*.json` with the built tool
5. A test that the built `LoadKit.Cli` contains all `ai/skills/loadtest/**` files and the schema

## Pre-commit (optional)

`dotnet format` on changed files via Husky.Net or a git hook.
