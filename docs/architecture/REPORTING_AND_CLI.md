# Reports and CLI

> Status: draft. Command reference for users: `docs/user/CLI_REFERENCE.md`.

## Report

`RunReport` is the single model all formats are built from:

- parameters: scenario, baseUrl (without secrets), concurrency, volume, warmup, duration, `loadrun` id;
- totals: total, successful, errors, error rate, RPS, min/p50/p95/p99/max;
- the same for each scenario request;
- status codes and `ErrorKind` with counts;
- sample error responses (masked);
- duration histogram (10–20 buckets);
- threshold checks;
- warnings (tester CPU, interrupted run, frequent 401s);
- a ready-made KQL query for Application Insights by `loadrun`.

| Format | Writer | Purpose |
|---|---|---|
| Console | `Cli/Rendering/RunSummaryRenderer` | right after the run; Markdown tables when output is redirected |
| `report.md` | `MarkdownReportWriter` | reading, PRs, AI analysis |
| `report.json` | `JsonReportWriter` | machine processing, run comparison; the schema is a contract |

Report folder name: `<out>/<yyyyMMdd-HHmmss>-<scenario-name>/`.

## CLI

Spectre.Console.Cli. Each command is a class in `Commands/`; the logic is calls into Core.

| Command | Core operations |
|---|---|
| `init` | generate a scenario from answers, `.env` template, `.gitignore` |
| `validate` | load + validate |
| `check` | validate + preflight + one request for each `requests[]` item |
| `run` | the full flow |
| `ai install` / `ai status` | see `AI_INTEGRATION.md` |

Exit codes: `0` ok, `1` thresholds violated, `2` scenario invalid, `3` preflight failed,
`4` confirmation required for a non-localhost URL (run without a terminal and without `--yes`), `130` interrupted by `Ctrl+C`.

### Remote URL confirmation

- In a terminal: a prompt "Load https://…? concurrency 20, 2000 requests [y/N]".
- Without a terminal (AI agent, CI) and without `--yes`: exit 4 and the text "get confirmation and retry with `--yes`".
  Following the skill, the agent asks the user and adds `--yes` only after they agree.

### Interactive `init`

If stdin is a terminal and no flags are given, it asks: baseUrl, auth type (options
in plain language), scope/header if needed. Without a terminal (agent, CI), flags only:
`--base-url`, `--auth`, `--scope`, `--header`. Result: the scenario, `loadtests/.env` with the required
variables left empty, `loadtests/scenario.schema.json`, `.gitignore` entries, a hint for the next command.
