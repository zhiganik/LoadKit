# LoadKit scenario format (version 1)

> Status: draft. Reference for people and AI assistants. Working rules are in [SKILL.md](SKILL.md).
> All JSON examples in this file are automatically checked by tests (`Category=Docs`).

## Structure

```json
{
  "$schema": "../scenario.schema.json",
  "version": 1,
  "name": "orders-smoke",
  "description": "What the scenario checks",
  "baseUrl": "https://localhost:5001",
  "headers": { "Accept": "application/json" },
  "auth": { "type": "bearer", "token": "${env:API_TOKEN}" },
  "tagRuns": true,
  "load": { "concurrency": 10, "totalRequests": 500, "warmup": 20 },
  "requests": [
    { "name": "list", "method": "GET", "path": "/api/orders", "expect": { "status": [200] } }
  ],
  "thresholds": { "p95Ms": 500, "errorRatePercent": 1 }
}
```

`$schema` is optional and gives IDE autocompletion. `loadtest init` puts the schema into
`loadtests/scenario.schema.json` and sets the relative path.

## Root fields

| Field | Req. | Default | Description |
|---|---|---|---|
| `version` | yes | — | format version, currently `1` |
| `name` | yes | — | scenario name, shown in the report |
| `description` | no | — | what the scenario checks |
| `baseUrl` | yes | — | API address; supports `${env:...}` |
| `headers` | no | `{}` | headers for all requests |
| `auth` | no | no auth | see "Authentication" |
| `tagRuns` | no | `true` | add `loadrun=<id>` to the query string for filtering in Application Insights |
| `load` | yes | — | load parameters |
| `requests` | yes | — | at least one request |
| `thresholds` | no | — | thresholds that affect the exit code |

## `load`

| Field | Req. | Default | Description |
|---|---|---|---|
| `concurrency` | yes | — | how many requests are "in flight" at the same time |
| `totalRequests` | one of two | — | total number of requests |
| `durationSec` | one of two | — | run duration in seconds |
| `warmup` | no | `0` | the first N requests are not counted in metrics |
| `timeoutMs` | no | `30000` | timeout for a single request |

`concurrency` is not "requests per second". 20 means 20 workers each send the next request right
after the response to the previous one.

## `requests[]`

| Field | Req. | Default | Description |
|---|---|---|---|
| `name` | yes | — | unique name, shown in the report |
| `method` | yes | — | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| `path` | yes | — | path relative to `baseUrl`, templates allowed |
| `weight` | no | `1` | share in the mix (weights 70/30 = 7/3) |
| `headers` | no | — | added to the root headers |
| `query` | no | — | object of query parameters |
| `body` | for POST/PUT/PATCH | — | JSON object |
| `bodyRaw` | no | — | body as a string instead of `body` |
| `contentType` | no | `application/json` | content type for `bodyRaw` |
| `allowEmptyBody` | no | `false` | allow POST/PUT/PATCH without a body |
| `auth` | no | `true` | `false` — send without auth |
| `expect.status` | yes | — | allowed status codes, e.g. `[200]` |
| `expect.maxMs` | no | — | a slower response counts as an error |

## `thresholds`

`p50Ms`, `p95Ms`, `p99Ms` — maximum for the percentile across all requests; `errorRatePercent` — maximum
error rate. Violating any threshold → exit code `1`.

## Authentication

The token is acquired **before the load starts** and refreshed in the background; this does not affect metrics.
Secrets only as `${env:NAME}`; the values live in `loadtests/.env`.

| Type | Required fields | Optional |
|---|---|---|
| `bearer` | `token` | `header` (`Authorization`), `format` (`Bearer {token}`) |
| `apiKey` | `value` + one of `header` / `query` | — |
| `azureIdentity` | `scope` | `source`: `azureCli` (default) or `default` |
| `oauth2ClientCredentials` | `tokenUrl`, `clientId`, `clientSecret`, `scope` | — |
| `login` | `request`, `tokenPath` | `expiresInPath`, `header` (`Authorization`), `format` (`Bearer {token}`) |

`login`: `tokenPath` and `expiresInPath` use a JSONPath subset — `$.accessToken`, `$.data.token`,
`$.items[0].token`, `$['access-token']`. `expiresInPath` points to seconds (a number or a numeric string); without it the
lifetime comes from the JWT `exp` claim, and a token with neither is treated as non-expiring (`check` warns).
Tokens are refreshed in the background at ~80% of their lifetime.

Prefer passing `apiKey` in a header: a value in `query` ends up in the URL, and the URL is written to server logs.

<!-- fragment:auth -->
```json
{ "type": "bearer", "token": "${env:API_TOKEN}" }
```

<!-- fragment:auth -->
```json
{ "type": "apiKey", "header": "x-functions-key", "value": "${env:FUNC_KEY}" }
```

<!-- fragment:auth -->
```json
{ "type": "apiKey", "query": "code", "value": "${env:FUNC_KEY}" }
```

<!-- fragment:auth -->
```json
{ "type": "azureIdentity", "scope": "api://my-api/.default" }
```

If `check` fails with AADSTS65001 mentioning "Microsoft Azure CLI": the API does not allow Azure CLI to get
tokens. The owner of the API's app registration adds client id `04b07795-8ddb-461a-bbee-02f9e1bf7b46`
under Expose an API → Authorized client applications.

<!-- fragment:auth -->
```json
{
  "type": "oauth2ClientCredentials",
  "tokenUrl": "https://login.microsoftonline.com/${env:TENANT_ID}/oauth2/v2.0/token",
  "clientId": "${env:CLIENT_ID}",
  "clientSecret": "${env:CLIENT_SECRET}",
  "scope": "api://my-api/.default"
}
```

<!-- fragment:auth -->
```json
{
  "type": "login",
  "request": {
    "method": "POST",
    "path": "/api/auth/login",
    "body": { "email": "${env:TEST_USER}", "password": "${env:TEST_PASSWORD}" }
  },
  "tokenPath": "$.accessToken",
  "expiresInPath": "$.expiresIn",
  "format": "Bearer {token}"
}
```

## Substitutions

| Syntax | When evaluated | Example |
|---|---|---|
| `${env:NAME}` | once, at load time | value from the environment or `.env` |
| `{{guid}}` | per request | `3f2b8c1e-...` |
| `{{seq}}` | per request | `1`, `2`, `3` |
| `{{randomInt:MIN:MAX}}` | per request | `42` (bounds inclusive) |
| `{{now}}` | per request | `2026-09-24T10:15:30Z` |

They work in `path`, `query`, `headers`, `body`, `bodyRaw`. In `body`, a string consisting entirely of
`{{seq}}` or `{{randomInt:...}}` becomes a number.

## Examples

### Smoke test without auth

```json
{
  "version": 1,
  "name": "health-smoke",
  "baseUrl": "http://localhost:5080",
  "load": { "concurrency": 5, "totalRequests": 200 },
  "requests": [
    { "name": "health", "method": "GET", "path": "/health", "expect": { "status": [200] } }
  ],
  "thresholds": { "p95Ms": 200, "errorRatePercent": 0 }
}
```

### Azure Function with a key

```json
{
  "version": 1,
  "name": "func-process",
  "baseUrl": "https://my-func.azurewebsites.net",
  "auth": { "type": "apiKey", "header": "x-functions-key", "value": "${env:FUNC_KEY}" },
  "load": { "concurrency": 10, "totalRequests": 500, "warmup": 20 },
  "requests": [
    {
      "name": "process",
      "method": "POST",
      "path": "/api/process",
      "body": { "id": "{{guid}}", "amount": "{{randomInt:1:1000}}" },
      "expect": { "status": [200, 202] }
    }
  ]
}
```

### Request mix, Entra ID via `az login`

```json
{
  "version": 1,
  "name": "orders-mix",
  "description": "70% reads, 25% creates, 5% without a token (expect 401)",
  "baseUrl": "https://localhost:5001",
  "auth": { "type": "azureIdentity", "scope": "api://orders-api/.default" },
  "load": { "concurrency": 20, "durationSec": 120, "warmup": 50 },
  "requests": [
    { "name": "list", "method": "GET", "path": "/api/orders", "weight": 70,
      "query": { "page": "{{randomInt:1:10}}" },
      "expect": { "status": [200], "maxMs": 1000 } },
    { "name": "create", "method": "POST", "path": "/api/orders", "weight": 25,
      "body": { "productId": "{{randomInt:1:100}}", "qty": 1, "clientRef": "{{guid}}" },
      "expect": { "status": [201] } },
    { "name": "no-auth", "method": "GET", "path": "/api/orders", "weight": 5,
      "auth": false, "expect": { "status": [401] } }
  ],
  "thresholds": { "p95Ms": 500, "p99Ms": 1500, "errorRatePercent": 1 }
}
```

### Traffic for Application Insights

Expected 500s are listed in `expect`, so the report shows only unexpected errors.

```json
{
  "version": 1,
  "name": "appinsights-playground",
  "baseUrl": "http://localhost:5080",
  "load": { "concurrency": 15, "durationSec": 600 },
  "requests": [
    { "name": "fast", "method": "GET", "path": "/api/fast", "weight": 60, "expect": { "status": [200] } },
    { "name": "slow", "method": "GET", "path": "/api/slow", "weight": 20, "expect": { "status": [200] } },
    { "name": "dep",  "method": "GET", "path": "/api/dep",  "weight": 10, "expect": { "status": [200] } },
    { "name": "fail", "method": "GET", "path": "/api/fail", "weight": 10, "expect": { "status": [200, 500] } }
  ]
}
```

## Test data

- Unique keys via `{{guid}}` / `{{seq}}`, otherwise conflicts (409) distort the results.
- Tag created data so you can delete it later: `"clientRef": "loadtest-{{guid}}"`.
- Do not load endpoints with side effects (emails, payments, SMS) without stubs or a test mode.
- `check` also sends real requests: one POST for each `requests[]` item.

## Common validation errors

| Message | What to do |
|---|---|
| `requests[i].body is required for POST` | add `body` or `"allowEmptyBody": true` |
| `env variable API_TOKEN is not set` | add the variable to `loadtests/.env` (an empty value counts as not set) |
| `secret-like value found in auth.token` | replace the value with `${env:...}` |
| `load: specify either totalRequests or durationSec` | keep only one field |
| `unknown template {{uuid}}` | use `{{guid}}` |
| `unknown field 'retries'` | the field is not in the format; remove it |
| `apiKey: specify exactly one of header or query` | keep only one |
| `requests[i]: use either body or bodyRaw, not both` | keep only one |
| `requests[i].path must start with '/'` | paths are relative to `baseUrl`: `"/api/orders"` |
| `auth.tokenPath '...' is not a supported JSONPath` | write it like `$.accessToken` or `$.data.token` |

Every error names a code (for example `body-required`) and a JSON path; `loadtest validate` prints a hint for each.
