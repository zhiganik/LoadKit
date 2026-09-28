# First run in 5 minutes

> Status: draft

## 1. Install

```bash
dotnet tool install -g LoadKit
loadtest --version
```

## 2. Create a scenario

```bash
loadtest init loadtests/scenarios/my-api.json
```

`init` asks 2–3 questions: the API address and how it is protected. Result:

```
loadtests/
  scenarios/my-api.json   ← scenario
  scenario.schema.json    ← IDE autocompletion
  .env                    ← required variables (empty), in .gitignore
  reports/                ← in .gitignore
```

## 3. Auth: where to get the token

LoadKit behaves like Postman: it attaches a header with the token to every request.
The API does not need to change. You only need to answer **where to get the token**.

### No auth

Do nothing.

### I copy the token by hand (Swagger, Postman, DevTools)

1. Copy the token the same way you would for Postman.
2. Paste it into `loadtests/.env`: `API_TOKEN=eyJhbGciOi...`
3. In the scenario: `"auth": { "type": "bearer", "token": "${env:API_TOKEN}" }`

The token expires (usually after an hour) — then paste a new one. For runs of a few minutes this is fine.

### Azure Function with a key

1. Portal → Function App → App keys → copy the key.
2. `loadtests/.env`: `FUNC_KEY=...`
3. `"auth": { "type": "apiKey", "header": "x-functions-key", "value": "${env:FUNC_KEY}" }`

The key does not expire.

### The API is protected by Entra ID and I am logged in via `az login`

1. Once: `az login`.
2. `"auth": { "type": "azureIdentity", "scope": "api://my-api/.default" }`

`scope` is the API's Application ID URI from the app registration + `/.default`. LoadKit gets the token itself
via Azure CLI and refreshes it itself. There are no secrets in files. Your account needs access to the API.

A common error on the first run is **AADSTS65001** mentioning "Microsoft Azure CLI". It means
the API does not allow Azure CLI to get tokens for it. The owner of the API's app registration adds
client id `04b07795-8ddb-461a-bbee-02f9e1bf7b46` once under Expose an API → Authorized client applications.

### The API has its own login endpoint

1. `loadtests/.env`: `TEST_USER=...` and `TEST_PASSWORD=...` (a test account).
2. In the scenario, describe the login request and where the token is in its response:

<!-- fragment:auth -->
```json
{
  "type": "login",
  "request": { "method": "POST", "path": "/auth/login", "body": { "email": "${env:TEST_USER}", "password": "${env:TEST_PASSWORD}" } },
  "tokenPath": "$.accessToken",
  "expiresInPath": "$.expiresIn"
}
```

LoadKit logs in before the load and again at ~80% of the token lifetime.

### A service client with a client secret

1. `loadtests/.env`: `CLIENT_ID=...`, `CLIENT_SECRET=...` (and `TENANT_ID=...` for Entra ID).
2. `"auth": { "type": "oauth2ClientCredentials", "tokenUrl": "...", "clientId": "${env:CLIENT_ID}", "clientSecret": "${env:CLIENT_SECRET}", "scope": "api://my-api/.default" }`

If the token is issued but the API answers 401/403, the API does not accept app-only tokens:
it needs an app role for the client. More examples: `ai/skills/loadtest/SCENARIO_REFERENCE.md`.

## 4. Check

```bash
loadtest validate loadtests/scenarios/my-api.json   # format and variables
loadtest check loadtests/scenarios/my-api.json      # one request per endpoint
```

`check` shows the responses. `200` means everything is set up. `401` means the token is wrong or expired — you find out before the load.
The requests are real: if the scenario has a POST, `check` creates one record for each such request.

## 5. Load

```bash
loadtest run loadtests/scenarios/my-api.json --out loadtests/reports/
```

If `baseUrl` is not localhost, LoadKit asks for confirmation. In scripts and CI, add `--yes`.

## 6. Working through an AI assistant

```bash
loadtest ai install
```

Then in Claude Code: `/loadtest load GET /api/orders, 20 concurrent, 1 minute`
or just "run a load test on order creation". The assistant writes the scenario, checks it,
asks you to add secrets to `.env` and summarizes the report.

## 7. Find the run in Application Insights

The report contains the `loadrun` id and a ready-made query:

```kusto
requests
| where url contains "loadrun=<id>"
| summarize p50=percentile(duration,50), p95=percentile(duration,95), p99=percentile(duration,99)
```
