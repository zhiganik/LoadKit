# Reports and CLI

> Status: ready. Command reference for users: `docs/user/CLI_REFERENCE.md`.

## Report

`src/LoadKit.Core/Reporting/`: `RunReport` (+ `Report*` records), `RunReportBuilder`, `MarkdownReportWriter`,
`JsonReportWriter`, `ReportFileWriter`, `ApplicationInsightsQuery`.

`RunReport` is the single model all formats are built from, the console summary included:

- parameters: scenario, baseUrl (masked), auth type, effective concurrency / volume / warmup / timeout, `loadrun` id;
- totals: count, successful, errors, error rate, RPS, min/mean/p50/p95/p99/max (ms, rounded to 0.001);
- the same for each scenario request, with method and path;
- status codes (with unexpected counts) and `ErrorKind` counts;
- sample errors (masked);
- latency histogram: fixed 1-2-5 bounds from 1 ms to 30 s plus an open bucket, empty edge buckets dropped
  (`Metrics/HistogramBuilder`); fixed bounds keep two reports comparable;
- threshold checks and the verdict (`thresholdsPassed`, null without thresholds);
- warnings (`ReportWarningCodes`): `interrupted`, `unauthorized-responses` (advice depends on the auth type),
  `token-refresh-failed`, `tester-cpu` (average CPU of the LoadKit process above 85% of all cores), `few-requests`
  (fewer than 100 measured requests: p99 is close to the max);
- a ready-made KQL query for Application Insights: `loadrun` filter plus a timestamp window of ±5 minutes
  around the run (null when the run was not tagged).

| Format | Writer | Purpose |
|---|---|---|
| Console | `Cli/Rendering/RunSummaryRenderer` | right after the run; Markdown tables when output is redirected |
| `report.md` | `MarkdownReportWriter` | reading, PRs, AI analysis; stable headings, `\n` line endings |
| `report.json` | `JsonReportWriter` | machine processing, run comparison; contract: `schemas/report.schema.json` |

`report.json` is camelCase with explicit nulls and carries `schemaVersion` (`RunReport.CurrentSchemaVersion`).
`ReportSchemaConsistencyTests` (Category=Docs) check that a serialized report matches the schema in both directions.
A change to the report shape updates the model, the schema and `schemaVersion` together; a breaking change needs an ADR.

Report folder name: `<out>/<yyyyMMdd-HHmmss>-<scenario-name>/` (UTC start time; the name reduced to letters, digits,
`-`, `_`). Reports are written for interrupted runs too.

## CLI

Spectre.Console.Cli. Each command is a class in `Commands/`; the logic is calls into Core.

| Command | Core operations |
|---|---|
| `init` | `ScenarioScaffolder` (scenario from answers) + `WorkspaceInitializer` (files) |
| `validate` | load + validate |
| `check` | validate + preflight + one request for each `requests[]` item |
| `run` | the full flow; `--out` → `RunReportBuilder` + `ReportFileWriter` |
| `ai install` / `ai status` | see `AI_INTEGRATION.md` |

`--version` prints the tool version (`Version` in `Directory.Build.props`).

Exit codes: `0` ok, `1` thresholds violated, `2` scenario invalid, `3` preflight failed,
`4` confirmation required for a non-localhost URL (run without a terminal and without `--yes`), `130` interrupted by `Ctrl+C`.

### Remote URL confirmation

- In a terminal: a prompt "Load https://…? concurrency 20, 2000 requests [y/N]".
- Without a terminal (AI agent, CI) and without `--yes`: exit 4 and the text "get confirmation and retry with `--yes`".
  Following the skill, the agent asks the user and adds `--yes` only after they agree.

### Interactive `init`

In a terminal (and without `--no-interactive`) it asks for what the flags did not give: baseUrl, auth type
(options in plain language), scope for `azureIdentity`/`oauth2ClientCredentials`, header for `apiKey`.
Without a terminal (agent, CI), flags only: `--base-url` (required), `--auth`, `--source`, `--scope`, `--header`.

Result, for `loadtests/scenarios/<name>.json` (the workspace root is the folder above `scenarios/`, otherwise the
scenario's folder):

- the scenario: safe defaults (concurrency 5, 200 requests, warmup 20, p95 500 ms, 1% errors), one placeholder
  request, secrets only as `${env:...}`, `"$schema": "../scenario.schema.json"`;
- `loadtests/scenario.schema.json`, embedded in the CLI by link from `schemas/` (updated if it differs);
- `loadtests/.env` with the required variables left empty; existing values are never changed;
- `.env` and `reports/` entries in the repository's `.gitignore` (found by `.git`, otherwise the working directory);
- a numbered list of next steps.

It never overwrites a scenario (exit 2) and can be run again for another scenario.
