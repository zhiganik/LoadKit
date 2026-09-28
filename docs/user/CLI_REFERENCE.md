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

## `loadtest validate <file>`

Checks the format and variables, sends nothing. Exit code `0` or `2`.
Prints every issue with its JSON path, code and a hint; warnings and info do not change the exit code.

| Flag | Description |
|---|---|
| `--env-file <path>` | path to `.env` (default: `.env` next to the scenario, then in its parent folder) |

When output is redirected (CI, scripts, AI agents), all commands print plain text without colors or line wrapping.

## `loadtest check <file>`

Preflight + one request for each `requests[]` item; shows the status, time and the start of the response body.
Exit code `0`, `2` or `3`.

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

Exit codes: `0` ok, `1` thresholds violated, `2` scenario invalid, `3` preflight failed,
`4` confirmation required (no terminal and no `--yes`), `130` interrupted.

## `loadtest ai install`

| Flag | Where |
|---|---|
| (none) | `./.claude/skills/loadtest/` |
| `--global` | `~/.claude/skills/loadtest/` |
| `--dir <path>` | `<path>/loadtest/` |
| `--agents-md` | additionally a block in `./AGENTS.md` |

## `loadtest ai status`

Shows where the skill is installed and whether its version matches the tool version.
