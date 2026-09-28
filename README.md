# LoadKit

> Status: ready

A simple CLI tool for load testing HTTP APIs during local development.
A scenario is described in a JSON file and started with one command; the result is a report with percentiles
(p50 / p95 / p99), errors by status code and sample responses.

```bash
loadtest run scenarios/orders.json
```

> `LoadKit` is a working name and may change.

---

## What problem it solves

1. **Load tests are written as code from scratch every time.** Each task gets its own project,
   its own request loop, its own metric calculation and a hand-assembled report. It is slow and not reusable.
2. **Tests are hard to hand over to a colleague.** To repeat a run, you have to understand someone else's code.
3. **Monitoring needs realistic load.** To see meaningful data in Application
   Insights (dashboards, workbooks, percentiles, Application Map), you need a mix of requests, errors and auth,
   not a single endpoint in a loop.
4. **Industrial tools are overkill for a local task.** k6, Azure Load Testing and JMeter
   are good for full-scale tests, but too heavy for "quickly run it locally before a commit".
   Detailed comparison: [ADR-001](docs/decisions/ADR-001-own-load-engine.md).

## Where the idea came from

While setting up monitoring for a Web App and a Function App in Azure (Application Insights,
dashboards, workbooks, alerts), I regularly needed to generate load and check that metrics
and percentiles were displayed correctly. Writing code for that every time was inconvenient. Hence the idea:
**one tool where load is declared as a scenario, not programmed.**

An additional goal: an AI assistant should be able to write scenarios. For that there is a JSON Schema,
the `validate` command and the [`loadtest`](ai/skills/loadtest/SKILL.md) skill with the [SCENARIO_REFERENCE.md](ai/skills/loadtest/SCENARIO_REFERENCE.md) reference.

## Features

- JSON scenario with IDE autocompletion (via JSON Schema).
- Method, path, headers, query, body; weighted request mix.
- Concurrency, total request count or duration, warmup, timeout.
- Auth: bearer token, API key / function key, Azure Identity (`az login`),
  OAuth2 client credentials, login through your own endpoint.
- Secrets only from environment variables or `.env`, never in the scenario itself.
- Templates for varied data: `{{guid}}`, `{{randomInt:1:100}}`, `{{seq}}`, `{{now}}`.
- Report: RPS, p50/p95/p99, min/max, errors by status code, sample error response bodies.
- Report export to Markdown and JSON.
- Thresholds: if p95 or the error rate is above the given value, the tool returns a non-zero exit
  code. This makes it usable in CI.
- Run tag `loadrun=<id>` in the query string, to filter your run in Application Insights.

## Quick start

```bash
# 1. Install (after the package is published)
dotnet tool install -g LoadKit

# 2. Create a scenario (asks 2–3 questions: API address and how it is protected)
loadtest init loadtests/scenarios/my-api.json

# 3. Fill in secrets in loadtests/.env (the file is already in .gitignore)
#    API_TOKEN=eyJ...

# 4. Check the scenario without load
loadtest validate loadtests/scenarios/my-api.json

# 5. One request per endpoint: checks availability and auth
loadtest check loadtests/scenarios/my-api.json

# 6. Load
loadtest run loadtests/scenarios/my-api.json --out loadtests/reports/
```

Example scenario:

```json
{
  "$schema": "../scenario.schema.json",
  "version": 1,
  "name": "orders-smoke",
  "baseUrl": "https://localhost:5001",
  "auth": { "type": "bearer", "token": "${env:API_TOKEN}" },
  "load": { "concurrency": 20, "totalRequests": 2000, "warmup": 50 },
  "requests": [
    { "name": "list", "method": "GET", "path": "/api/orders", "weight": 70,
      "expect": { "status": [200] } },
    { "name": "create", "method": "POST", "path": "/api/orders", "weight": 30,
      "body": { "productId": "{{randomInt:1:100}}", "qty": 1 },
      "expect": { "status": [201] } }
  ],
  "thresholds": { "p95Ms": 500, "errorRatePercent": 1 }
}
```

## What the tool is NOT

- Not a replacement for k6 / Azure Load Testing for production load and large volumes.
- Not distributed: load comes from a single machine.
- Not for request chains that pass data between steps (in v1).

## Working through an AI assistant

```bash
loadtest ai install
```

The command installs the `loadtest` skill into the project (`.claude/skills/loadtest/`). Then in Claude Code:

```
/loadtest load order creation, 30 concurrent, 2 minutes
```

or just "run a load test on GET /api/orders". The assistant finds the routes, writes the
scenario, checks it, asks you to add secrets to `.env`, runs it and summarizes the report.
For other AI tools: `loadtest ai install --dir <path>` or `--agents-md`.

## Documentation

| For whom | Where to look |
|---|---|
| User | [docs/user/GETTING_STARTED.md](docs/user/GETTING_STARTED.md), [docs/user/CLI_REFERENCE.md](docs/user/CLI_REFERENCE.md) |
| AI in your repository | [ai/skills/loadtest/](ai/skills/loadtest/SKILL.md) |
| LoadKit developer | [AGENTS.md](AGENTS.md), [docs/README.md](docs/README.md) |
| Why it is built this way | [docs/decisions/](docs/decisions/) |

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Run succeeded, thresholds met |
| 1 | Run completed, but thresholds were violated |
| 2 | Scenario error (validation) |
| 3 | Preflight failed (server unreachable or token not obtained) |
| 4 | Confirmation required for a non-localhost URL (no terminal and no `--yes`) |
| 130 | Interrupted with `Ctrl+C` (a report on the collected data is still created) |

## License

[MIT](LICENSE).
