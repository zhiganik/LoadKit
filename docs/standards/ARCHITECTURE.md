# Architecture rules

> Status: ready. How the application is built is described in `docs/architecture/OVERVIEW.md`. This file
> lists the rules that must not be broken.

## Layers and dependencies

```
LoadKit.Cli  ──►  LoadKit.Core
tests/*      ──►  LoadKit.Core, LoadKit.Cli, samples/TargetApi
```

- `Core` knows nothing about `Cli`, the console, Spectre.Console or the DI container.
- `Cli` is a thin shell: argument parsing, calling Core, rendering the result. There is no business logic in Cli.
- A future shell (for example, Desktop) connects to Core the same way Cli does, without changes to Core.

## Core modules

| Module | Responsible for | Does not do |
|---|---|---|
| `Scenarios` | model, loading, `.env`, `${env:}`, templates, validation (own validator) | HTTP |
| `Auth` | acquiring, caching and applying tokens | timing |
| `Engine` | workers, request selection, sending, timing | percentile calculation |
| `Metrics` | result collection, percentiles, histogram, thresholds | formatting |
| `Reporting` | report model, Markdown, JSON | console output |

Dependencies between modules go only downstream along the execution flow:
`Scenarios → Auth → Engine → Metrics → Reporting`. Backward references are forbidden.

## Contracts (public, changed only deliberately)

1. Scenario format — `schemas/scenario.schema.json` + `SCENARIO_REFERENCE.md`.
2. CLI commands and flags — `docs/user/CLI_REFERENCE.md`.
3. Exit codes — `0/1/2/3/4/130`.
4. The `report.json` schema — `schemas/report.schema.json`, versioned by `schemaVersion`.
5. The `ai/skills/loadtest/` skill.

Any contract change is a separate item in the task's final report. A breaking change requires an ADR.

## Reuse

- Before adding a helper, search Core: template substitution, `.env`, secret masking and
  percentiles are already implemented in one place.
- One piece of logic, one place. The `check` and `run` commands use the same `HttpClient` pipeline
  and the same auth providers.

## Dependencies (packages)

- Allowed licenses: MIT, Apache-2.0, BSD. Before adding a package, check its license in the PR.
- Forbidden: NBomber v5+ (commercial license, ADR-001), FluentAssertions v8+ (commercial license),
  Newtonsoft.Json.Schema (free validation quota), JsonSchema.Net (EULA with a maintenance fee).
- Check the license from the license/EULA file of the specific version, not from memory: terms change.
- Prefer the BCL. A new package is justified if it saves more than it costs to maintain.
