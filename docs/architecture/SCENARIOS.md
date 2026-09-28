# Scenarios: model, loading, validation

> Status: ready. User-facing format: `ai/skills/loadtest/SCENARIO_REFERENCE.md`.
> Format changes: skill `changing-scenario-format`.

## Files

`src/LoadKit.Core/Scenarios/`:
- `ScenarioLoader`, `EnvFileLoader`, `ScenarioValidator`, `ScenarioCompiler`, `ValidationIssue`, `ValidationCodes`,
  `LocalAddress`, `CompiledScenario` / `CompiledRequest`;
- `Model/*` — records (`Scenario`, `LoadOptions`, `RequestDefinition`, auth variants) and `ScenarioDefaults`;
- `Validation/*` (internal) — `ScenarioFormat` (the format as data), `StructureValidator`, `SecretLiteralValidator`,
  `EnvSubstitution`, `SemanticValidator`, `ScenarioBinder`;
- `Templates/*` — `TemplateCompiler`, `CompiledTemplate`, `ITemplateGenerator` and generators.

Schema for IDEs: `schemas/scenario.schema.json`.

## Validation without a third-party JSON Schema library

The source of truth is `ScenarioValidator`, which walks a `JsonDocument` and collects **all** errors
(unknown fields, types, required fields, semantics) with JSON paths. The `scenario.schema.json` file
is only for autocompletion and highlighting in IDEs.

Why not a library: popular options have restrictions for commercial use
(Newtonsoft.Json.Schema has a free validation quota, JsonSchema.Net has a maintenance fee under its new EULA),
while our own check of a fixed structure is a small amount of code and gives better error messages.
The allowed fields live in one place, `Validation/ScenarioFormat`. `FormatConsistencyTests` check that the schema
and the model records describe exactly the same fields and required flags.

## Contracts

- `ScenarioLoader.LoadAsync(path, envFilePath?, ct) → ScenarioLoadResult`: `Scenario` (a `CompiledScenario`) when
  there are no errors, plus all `Issues` (errors, warnings, info).
- `ScenarioLoader.LoadFromJson(json, envFileVariables)` — the same without files (tests, docs examples).
- `ScenarioValidator.Validate(JsonObject, resolveVariable) → IReadOnlyList<ValidationIssue>` — all issues at once, each with
  severity, code, JSON path (`requests[0].body`, `$` for the file), message and hint. Substitutes `${env:}` in place.
- `CompiledScenario` — the model plus templates compiled into segments, ready for the engine.

## Flow

1. Read JSON into a `JsonNode` tree (comments, trailing commas and a BOM are allowed; duplicate keys are `invalid-json`).
2. Check `version`. Unknown version → error with a hint to update the tool.
3. Structural check: unknown fields (except `$schema`), types, required fields.
4. `secret-literal` check on **raw** values — before env substitution, otherwise a secret from `.env`
   would look like a secret written into the file.
5. Load `.env`: `--env-file` → `.env` next to the scenario → `.env` in the parent folder (`loadtests/`).
   Only the first file found is used. Process environment variables take precedence over `.env`.
6. Substitute `${env:NAME}` once (a value is not substituted again). An unset or empty variable is `env-missing`,
   reported once per variable at its first path.
7. Semantic rules. They skip fields that already failed the structural check.
8. If there are no errors: bind to the model (defaults from `ScenarioDefaults`) and compile `{{...}}` templates into
   segments (literal / generator), so there is no parsing on the hot path.

## Input and structural codes

| Code | When |
|---|---|
| `file-not-found` | scenario file does not exist |
| `env-file-not-found` | the file given with `--env-file` does not exist |
| `invalid-json` | syntax error or duplicate key |
| `unsupported-version` | `version` is not `1`; validation stops |
| `unknown-field` | field not in the format; hint suggests a close name |
| `invalid-type` | wrong JSON type, including `null` |
| `required-field` | required field is missing |
| `invalid-value` | right type, wrong value: method, path without `/`, non-http `baseUrl`/`tokenUrl`, numbers below minimum, status outside 100–599, `errorRatePercent` outside 0–100, unknown `auth.type`/`source`, empty `requests`, malformed `${env:`, unsupported JSONPath in `tokenPath`/`expiresInPath` |

`method` is accepted in any case and stored upper case; the IDE schema suggests upper case only.

## Semantic rules

| Code | Rule | Level |
|---|---|---|
| `body-required` | POST/PUT/PATCH without `body`/`bodyRaw` and without `allowEmptyBody` | error |
| `body-on-get` | `body` or `bodyRaw` on GET/DELETE | warning |
| `body-conflict` | both `body` and `bodyRaw` | error |
| `load-mode` | both `totalRequests` and `durationSec`, or neither | error |
| `concurrency-gt-total` | `concurrency` > `totalRequests` | warning |
| `warmup-gt-total` | `warmup` ≥ `totalRequests` | error |
| `unknown-template` | unknown `{{...}}`; hint suggests the closest template | error |
| `invalid-template` | known template with wrong arguments, or unclosed `{{` | error |
| `env-missing` | variable not set | error |
| `secret-literal` | `auth.token`, `auth.value`, `auth.clientSecret`, a password in `auth.request.body` or an `Authorization` header contains a literal instead of `${env:}` | error |
| `duplicate-request-name` | repeated `requests[].name` | error |
| `expect-missing` | no `expect`, no `expect.status` or an empty list | error |
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

Templates are checked in `path`, root and request `headers`, `query`, `body` (all nested strings) and `bodyRaw`.
In `body`, a string consisting entirely of a numeric template (`{{seq}}`, `{{randomInt}}`) is serialized as a number.
A JSON body compiles into one `CompiledTemplate` over its compact JSON text, so rendering only concatenates.
Generated values are inserted into JSON strings without escaping; generators must return JSON-safe text.
A new template is an `ITemplateGenerator` class + registration in `TemplateRegistry.CreateDefault` + a row in the reference.

## Edge cases

- BOM and CRLF in files are supported.
- `.env`: `KEY=VALUE` lines, `#` is a comment, quotes are stripped, a leading `export ` is ignored.
- A `${env:X}` value inside a string is substituted as part of the string: `"Bearer ${env:T}"`.
- `$schema` is the only field outside the format that is allowed and ignored.
- `init` copies the schema to `loadtests/scenario.schema.json` and sets
  `"$schema": "../scenario.schema.json"` in the scenario, so autocompletion works offline.
