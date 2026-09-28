---
name: loadtest
description: Use when the user asks to load test, stress test or benchmark an HTTP API or endpoint, measure latency percentiles (p50, p95, p99) under load, generate traffic for Application Insights dashboards, or mentions loadtest, LoadKit or scenario files in loadtests/.
metadata:
  loadkit-version: "1.0"
  scenario-format-version: "1"
---

# Load testing with LoadKit

## Overview

LoadKit is a CLI (`loadtest`) that runs HTTP load from a declarative JSON scenario.
Your job: turn the request into a valid scenario, verify it, run it safely, explain the report.

**Never write code to generate load** (C#, k6, scripts, loops with curl). If the scenario format
cannot express what the user wants, say so and suggest the closest scenario.

Request details when invoked as a command: $ARGUMENTS

## Preconditions

- `loadtest --version` must work. If not, tell the user: `dotnet tool install -g LoadKit`.
- Layout in the user's repo:
  ```
  loadtests/
    scenarios/*.json       # committed
    scenario.schema.json   # committed, IDE autocompletion
    reports/               # gitignored
    .env                   # gitignored, secrets
  ```
  If `loadtests/` is missing: `loadtest init loadtests/scenarios/<name>.json --base-url <url> --auth <type>`
  (`--scope <scope>` is required for `azureIdentity` and `oauth2ClientCredentials`; `--header <name>` for `apiKey`).
  It creates the layout above, `.env` with empty variables and `.gitignore` entries, and prints the next steps.
- If any command prints `warning (skill-outdated)`, tell the user to run `loadtest ai install` to update this skill.

## Workflow

1. **Target.** Determine base URL and endpoints. If the API code is in this repo, find routes yourself
   (controllers, `MapGet`/`MapPost`, Azure Functions `HttpTrigger`) instead of asking.
2. **Auth.** Pick `auth.type` from the table below. If unclear how the API is protected, ask once.
3. **Scenario.** Create or edit a file in `loadtests/scenarios/`. Fields and examples:
   [SCENARIO_REFERENCE.md](SCENARIO_REFERENCE.md). Use only fields listed there.
4. **Validate.** `loadtest validate <file>`. Fix every reported error, repeat until clean.
5. **Secrets.** If validate reports a missing variable, ask the user to add it to `loadtests/.env`.
   Never ask the user to paste a secret into chat. Never read or print `.env`.
6. **Check.** `loadtest check <file>`. It exits `0` even when a status is unexpected, so read the output:
   any `UNEXPECTED` line (especially 401/403/404) → stop and report it; do not run load. Exit `3` means preflight
   failed: follow the printed `hint:` line (API not running, wrong credentials, `az login`, scope).
7. **Safety gate.** If `baseUrl` is not `localhost`/`127.0.0.1`, show URL, concurrency and
   total/duration and wait for explicit user confirmation. Only then add `--yes`.
   Exit code 4 means you ran a remote URL without `--yes` — ask the user, never add `--yes` on your own.
   `check` sends real requests too (a POST creates data), so the same gate applies to it.
8. **Run.** `loadtest run <file> --out loadtests/reports/` (plus `--yes` only after confirmation).
9. **Explain.** `run` prints `Report: <path>/report.md`. Read that file and summarize (see "Reading the report").

## Choosing auth

| Situation | `auth.type` |
|---|---|
| No auth | omit `auth` |
| User copies a token from Swagger/Postman | `bearer` |
| Azure Functions key | `apiKey` with `header: "x-functions-key"` |
| Entra ID protected API, user ran `az login` | `azureIdentity` (needs `scope`; on AADSTS65001 see reference) |
| Service client with client secret | `oauth2ClientCredentials` |
| API has its own login endpoint | `login` |

## Defaults for a new scenario

`concurrency` 5–10, `totalRequests` 200–500, `warmup` 20, `expect.status` on every request.
Increase load only when the user asks.

## Reading the report

- Lead with: RPS, p50 / p95 / p99, error rate, thresholds verdict (the `Result` row; exit code `1` = failed).
- Mention every item of the `## Warnings` section: `interrupted`, `unauthorized-responses`, `token-refresh-failed`,
  `tester-cpu` (the load machine was the bottleneck — numbers are pessimistic), `few-requests` (p99 unreliable).
- p99 ≥ 10× p50 → unstable tail (cold start, GC, locks, slow dependency).
- All percentiles grow together → saturation (CPU, DB, too few instances).
- Many `Timeout`/`Connection` errors → server overloaded or the load machine is the bottleneck.
- Many 401 → token expired or wrong scope; rerun `check`.
- Give the `loadrun` id and the KQL filter from the report for Application Insights.

## Exit codes

`0` ok · `1` thresholds failed · `2` scenario invalid · `3` preflight failed (server or auth) ·
`4` confirmation required for a remote URL · `130` interrupted.

## Red flags — stop

- About to write code that sends requests in a loop.
- A token, key or password written literally in the JSON.
- Running `run` without `validate` and `check`.
- Load bigger than the user asked for.
- Remote URL without confirmation.
- A field that is not in SCENARIO_REFERENCE.md.
- Expected 4xx/5xx counted as errors — list them in `expect.status` if they are intended.
