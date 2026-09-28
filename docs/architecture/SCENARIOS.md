# Scenarios: model, loading, validation

> Status: draft. User-facing format: `ai/skills/loadtest/SCENARIO_REFERENCE.md`.
> Format changes: skill `changing-scenario-format`.

## Files

`src/LoadKit.Core/Scenarios/`: `Model/*`, `ScenarioLoader`, `EnvFileLoader`, `TemplateCompiler`,
`ScenarioValidator`, `ValidationCodes`; schema for IDEs: `schemas/scenario.schema.json`.

## Validation without a third-party JSON Schema library

The source of truth is `ScenarioValidator`, which walks a `JsonDocument` and collects **all** errors
(unknown fields, types, required fields, semantics) with JSON paths. The `scenario.schema.json` file
is only for autocompletion and highlighting in IDEs.

Why not a library: popular options have restrictions for commercial use
(Newtonsoft.Json.Schema has a free validation quota, JsonSchema.Net has a maintenance fee under its new EULA),
while our own check of a fixed structure is a small amount of code and gives better error messages.
Drift between the schema and the model is caught by a consistency test (every model field is in the schema and vice versa).

## Contracts

- `ScenarioLoader.LoadAsync(path, envFilePath?, ct) → LoadResult` (a scenario or a list of errors).
- `ScenarioValidator.Validate(Scenario) → ValidationResult` — all errors at once, each with a code,
  JSON path, message and hint.
- `CompiledScenario` — a scenario with parsed templates, ready for the engine.

## Flow

1. Read JSON into a `JsonDocument` (comments and trailing commas are allowed).
2. Check `version`. Unknown version → error with a hint to update the tool.
3. Structural check: unknown fields (except `$schema`), types, required fields.
4. `secret-literal` check on **raw** values — before env substitution, otherwise a secret from `.env`
   would look like a secret written into the file.
5. Load `.env`: `--env-file` → `.env` next to the scenario → `.env` in the parent folder (`loadtests/`).
   Process environment variables take precedence over `.env`.
6. Substitute `${env:NAME}` once. Missing variables are collected into a list.
7. Semantic rules.
8. If there are no errors: deserialize into the model and compile `{{...}}` templates into segments
   (literal / generator), so there is no parsing on the hot path.

## Semantic rules

| Code | Rule | Level |
|---|---|---|
| `body-required` | POST/PUT/PATCH without `body`/`bodyRaw` and without `allowEmptyBody` | error |
| `body-on-get` | `body` on GET/DELETE | warning |
| `load-mode` | both `totalRequests` and `durationSec`, or neither | error |
| `concurrency-gt-total` | `concurrency` > `totalRequests` | warning |
| `warmup-gt-total` | `warmup` ≥ `totalRequests` | error |
| `unknown-template` | unknown `{{...}}` | error |
| `env-missing` | variable not set | error |
| `secret-literal` | `auth.token`, `auth.value`, `auth.clientSecret`, a password in `auth.request.body` or an `Authorization` header contains a literal instead of `${env:}` | error |
| `duplicate-request-name` | repeated `requests[].name` | error |
| `expect-missing` | no `expect.status` | error |
| `remote-url` | `baseUrl` is not localhost | info; `run` requires confirmation (or `--yes`) |
| `apikey-target` | `apiKey` has neither or both of `header`/`query` | error |
| `apikey-in-query` | `apiKey` via `query` | warning: the key ends up in the URL and server logs |

## Templates

| Template | Generator |
|---|---|
| `{{guid}}` | `Guid.NewGuid()` |
| `{{seq}}` | `Interlocked.Increment` per scenario |
| `{{randomInt:a:b}}` | `Random.Shared.Next(a, b + 1)` |
| `{{now}}` | `TimeProvider.GetUtcNow()` in ISO 8601 |

In `body`, a string consisting entirely of a numeric template is serialized as a number.
A new template is an `ITemplateGenerator` class + registration + a row in the reference.

## Edge cases

- BOM and CRLF in files are supported.
- `.env`: `KEY=VALUE` lines, `#` is a comment, quotes are stripped, a leading `export ` is ignored.
- A `${env:X}` value inside a string is substituted as part of the string: `"Bearer ${env:T}"`.
- `$schema` is the only field outside the format that is allowed and ignored.
- `init` copies the schema to `loadtests/scenario.schema.json` and sets
  `"$schema": "../scenario.schema.json"` in the scenario, so autocompletion works offline.
