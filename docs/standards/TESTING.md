# Testing

> Status: ready

## Tools

- xUnit. Assertions: built-in `Assert` (or Shouldly). FluentAssertions v8+ is not used (license).
- Test doubles: hand-written fake classes. A mocking library only if the fake would be more complex than the test.
- `TimeProvider` (FakeTimeProvider) for token refresh and run duration tests.

## Levels

| Level | Project | Covers |
|---|---|---|
| Unit | `LoadKit.Core.Tests` | validation, templates, `.env`, percentiles, thresholds, masking, auth providers |
| Integration | `LoadKit.IntegrationTests` | CLI commands against `samples/TargetApi`, exit codes, report contents |
| Docs | `Category=Docs` | JSON examples in `SCENARIO_REFERENCE.md`, `docs/user/*`, `samples/scenarios/*` are valid (taking `fragment:auth` into account); schema matches the model |

## Mandatory tests

- **Percentiles** — on known arrays: for 2000 values p95 is the 1900th in order, p99 the 1980th.
  Edge cases: 1 value, identical values, empty set. A separate test for N where
  computing in `double` would give an off-by-one rank error.
- **Validation** — for every rule: a positive and a negative case, checking the JSON path and error text.
- **Auth** — `ApplyAsync` makes no network calls; the token is refreshed exactly once
  with 50 concurrent requests; secrets are masked in the report.
- **Exit codes** — 0/1/2/3/4 in integration tests (4: remote URL without a terminal and without `--yes`).
- **Tokens** — a TargetApi token with a 10 s lifetime in a 30-second run: no 401s, no p99 spike.

## TargetApi for tests

`samples/TargetApi` is a minimal ASP.NET Core API. In integration tests it runs on
**real** Kestrel with port 0 (a random free port). `WebApplicationFactory` with the built-in
TestServer does not fit: the CLI makes real network calls, while TestServer lives in memory.

| Endpoint | Behavior |
|---|---|
| `/health` | 200 immediately |
| `/api/fast` | 200 immediately |
| `/api/slow` | 200 with a delay (configurable) |
| `/api/fail` | 500 with a given probability |
| `/api/dep` | calls an external fake service |
| `/secure` | 200 with a dev token, `x-api-key`, or a token from `/auth/login` or `/oauth2/token`; otherwise 401 |
| `/auth/login` | POST `{email, password}` → `{accessToken, expiresIn}`, default lifetime 10 s |
| `/oauth2/token` | POST form (client credentials) → `{access_token, expires_in}`, default lifetime 10 s |

## Test audit before a task

Before a non-trivial task, record in the chat:
1. which tests already cover the area;
2. which tests are added now;
3. what is deferred and why.

## Not tested

- Absolute latency values (machine-dependent). We check order and ratios.
- External services (Entra ID) — only through a fake `TokenCredential`.
