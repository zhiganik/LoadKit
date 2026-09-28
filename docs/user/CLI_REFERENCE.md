# Command reference

> Status: draft. This is a public contract: flag changes go into the task's final report, breaking changes need an ADR.

## `loadtest init <file>`

Creates a scenario, a `.env` template and `.gitignore` entries.

| Flag | Description |
|---|---|
| `--base-url <url>` | API address |
| `--auth <type>` | `none`, `bearer`, `apiKey`, `azureIdentity`, `oauth2ClientCredentials`, `login` |
| `--source <src>` | for `azureIdentity`: `azureCli` (default) or `default` |
| `--scope <scope>` | for `azureIdentity` / `oauth2ClientCredentials` |
| `--header <name>` | for `apiKey` (default `x-functions-key`) |
| `--no-interactive` | do not ask questions (automatic when there is no terminal) |

Without a terminal `--base-url` is required; `--scope` is required for `azureIdentity` and `oauth2ClientCredentials`.
Creates the scenario, `loadtests/scenario.schema.json`, `loadtests/.env` (required variables, empty) and adds
`loadtests/.env` and `loadtests/reports/` to `.gitignore`. Existing `.env` values are kept; an existing scenario is
never overwritten. Exit code `0`, or `2` for missing or invalid answers and an existing scenario file.

## `loadtest --version`

Prints the tool version.

## `loadtest validate <file>`

Checks the format and variables, sends nothing. Exit code `0` or `2`.
Prints every issue with its JSON path, code and a hint; warnings and info do not change the exit code.

| Flag | Description |
|---|---|
| `--env-file <path>` | path to `.env` (default: `.env` next to the scenario, then in its parent folder) |

When output is redirected (CI, scripts, AI agents), all commands print plain text without colors or line wrapping.

## `loadtest check <file>`

Preflight + one request for each `requests[]` item; shows the status, time and the start of the response body.
Exit code `0`, `2`, `3` or `4` (remote URL without confirmation).

| Flag | Description |
|---|---|
| `--env-file <path>` | path to `.env` |
| `--yes` | confirm a non-localhost URL in advance |

Preflight (also the first step of `run`): `baseUrl` must answer with any HTTP status, then the first token is
acquired. A failure prints what failed and a `hint:` line, exit `3`. A token without a known lifetime is a warning.

Each request prints `ok` or `UNEXPECTED` (status not in `expect.status`), the method, URL, status and time, and
the first 500 characters of the body (secrets masked). Unexpected statuses do not change the exit code (`0`):
read the output before running load. `check` does not add `loadrun` to URLs.

The requests are real: a POST from the scenario will actually create a record. The same remote URL
confirmation rules apply as for `run`.

## `loadtest run <file>`

| Flag | Description |
|---|---|
| `--out <dir>` | folder for `report.md` and `report.json` |
| `--concurrency <n>` | override `load.concurrency` |
| `--total <n>` | override `load.totalRequests` |
| `--duration <sec>` | override `load.durationSec` |
| `--env-file <path>` | path to `.env` |
| `--no-tag` | do not add `loadrun` to the query string |
| `--yes` | confirm a non-localhost URL in advance |

`--total` or `--duration` replaces the load mode of the scenario; passing both is an error (exit `2`).
With `--out <dir>`, `run` writes `<dir>/<yyyyMMdd-HHmmss>-<scenario-name>/report.md` and `report.json` (also for an
interrupted run) and prints the path. Without `--out` only the console summary is printed.
`report.json` follows `schemas/report.schema.json` (a public contract, versioned by `schemaVersion`).

Output: live progress in a terminal, then tables per request and overall (count, errors, RPS, min/mean/p50/p95/p99/max),
status codes, errors by kind, sample errors, threshold checks and warnings (interrupted run, unexpected 401s,
failed token refresh, load generator CPU above 85%, fewer than 100 measured requests). When output is redirected, there is no live
progress and the tables are Markdown. The first `Ctrl+C` stops the run and prints the partial results (exit `130`);
a second one terminates immediately.

Exit codes: `0` ok, `1` thresholds violated, `2` scenario invalid, `3` preflight failed,
`4` confirmation required (no terminal and no `--yes`), `130` interrupted.

## `loadtest ai install`

| Flag | Where |
|---|---|
| (none) | `./.claude/skills/loadtest/` |
| `--global` | `~/.claude/skills/loadtest/` |
| `--dir <path>` | `<path>/loadtest/` |
| `--agents-md` | additionally a block in `./AGENTS.md` (between `<!-- loadkit:start/end -->`, replaced on reinstall) |

`--global` and `--dir` cannot be combined. Prints `created` / `updated` / `unchanged` for each file. Exit code `0`.

## `loadtest ai status`

Shows where the skill is installed (project `./.claude/skills/loadtest/` and global `~/.claude/skills/loadtest/`),
whether each copy is up to date, older or newer than the tool, and whether `AGENTS.md` has the LoadKit block.
Exit code `0`. `validate` also prints `warning (skill-outdated)` when an installed copy is older than the tool.
