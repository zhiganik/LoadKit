# Implementation plan

> Status: draft. Before a task, an agent finds its phase here; when done, it checks off the items.
> Revision 2: fixed readiness criteria, validation without a third-party JSON Schema library,
> behavior without a terminal, embedding the skill without a copy.

One developer with an AI agent, **5 working days**. Each phase ends with a working, verified
result and separate commits following `docs/standards/COMMITS.md`.

## Day-by-day breakdown

| Day | Phases | Result of the day |
|---|---|---|
| 1 | 0 + 1 | skeleton, TargetApi, `loadtest validate` works |
| 2 | 2 | `loadtest run` with bearer/apiKey, percentiles in the console |
| 3 | 3 | all auth types, `loadtest check` |
| 4 | 4 + start of 5 | reports, `init`, `ai install` |
| 5 | end of 5 + 6 | skill check with an agent, packaging, demo |

If a day runs over, the buffer comes from phase 4 (histogram and CPU warnings move to v2).

---

## Phase 0. Repository foundation — 0.5 day

- [x] Documentation, `AGENTS.md`, `CLAUDE.md`, skills, `.gitignore`, `.gitattributes`.
- [x] `LoadKit.slnx`: `src/LoadKit.Core`, `src/LoadKit.Cli`, `samples/TargetApi`,
  `tests/LoadKit.Core.Tests`, `tests/LoadKit.IntegrationTests`.
- [x] `Directory.Build.props` (net10.0, nullable, warnings as errors), `Directory.Packages.props`, `.editorconfig`,
  `global.json`.
- [x] `samples/TargetApi` on `http://localhost:5080` with all endpoints from `docs/standards/TESTING.md`,
  including `/secure`, `/auth/login`, `/oauth2/token`.
- [x] `samples/scenarios/`: `smoke.json`, `mix.json`, `secure-bearer.json`, `secure-login.json`, `.env.example`.
- [x] CI (`.github/workflows/ci.yml`): build, format, test. (Green run pending the first push.)

**Done when:** CI is green; `curl localhost:5080/health` → 200; `/secure` without a token → 401.

## Phase 1. Scenarios and `validate` — 0.5 day

- [ ] Scenario model (records), `ScenarioLoader`, `EnvFileLoader`.
- [ ] `ScenarioValidator` on top of `JsonDocument`: unknown fields, types, required fields and all
  semantic rules from `docs/architecture/SCENARIOS.md`. Collects **all** errors with JSON paths.
- [ ] `secret-literal` check runs **before** `${env:}` substitution (after substitution the secret is already in the value).
- [ ] `${env:}` substitution, `TemplateCompiler` and template generators.
- [ ] `schemas/scenario.schema.json` — only for IDE autocompletion; a test that the schema matches the model.
- [ ] `validate` command: list of errors, exit 2.
- [ ] Docs tests: full scenarios and fragments (`<!-- fragment:auth -->`) from `SCENARIO_REFERENCE.md`,
  `samples/scenarios/*`.

**Done when:** a scenario with three different errors shows all three with paths and hints, exit 2;
all documentation examples pass.

## Phase 2. Engine, metrics, `run` — 1 day

- [ ] `HttpPipelineFactory`, `LoadRunner`, `WeightedRequestPicker`, `RequestFactory`.
- [ ] `ResultCollector`; `PercentileCalculator` with **integer** rank arithmetic
  (tests: N=2000 → p95 = 1900th, p99 = 1980th; N=1; identical values).
- [ ] `ThresholdEvaluator`, exit codes 0/1.
- [ ] Auth: `IAuthProvider`, `AuthHandler`, `bearer`, `apiKey` (header and query), `SecretMasker`.
- [ ] `run`: live progress, summary table per request and overall, status codes, sample errors.
- [ ] Non-localhost URL: in a terminal, ask; without a terminal and without `--yes`, exit 4 with a hint.
- [ ] `Ctrl+C` → report on the collected data, marked "interrupted", exit 130.

**Done when:** `run samples/scenarios/mix.json` against TargetApi shows p50/p95/p99,
and `/api/fail` errors are counted as expected or unexpected according to `expect.status`.

## Phase 3. Full auth and `check` — 1 day

- [ ] `TokenAuthProviderBase`: cache, background refresh at ~80% of lifetime, `MarkStale`,
  a single refresh under concurrent requests.
- [ ] `login` (against `/auth/login`), `oauth2ClientCredentials` (against TargetApi `/oauth2/token`).
- [ ] `azureIdentity`: `AzureCliCredential` by default, `source: "default"` → `DefaultAzureCredential`.
- [ ] Preflight: first token + `baseUrl` reachability, exit 3 with a specific hint.
- [ ] `check` command.
- [ ] Tests: `FakeTimeProvider` for refresh; fake `TokenCredential` for `azureIdentity`;
  a TargetApi token with a 10-second lifetime in a 30-second run → no 401s and no p99 spike.

**Done when:** `bearer`, `apiKey`, `login`, `oauth2ClientCredentials` pass against TargetApi `/secure`;
`azureIdentity` is covered by unit tests and checked manually once against a real API
(if an Entra ID API is available; otherwise marked "verified by tests only").

## Phase 4. Reports and `init` — 0.5 day

- [ ] `RunReport`, `report.md`, `report.json`, KQL by `loadrun`.
- [ ] `tagRuns`, warnings: frequent 401s, interruption, tester CPU > 85% (can move to v2).
- [ ] Histogram (can move to v2).
- [ ] `init`: interactive in a terminal, via flags without a terminal; creates the scenario, `loadtests/.env` with empty
  variables, copies `loadtests/scenario.schema.json`, appends to `.gitignore`.

**Done when:** a new user goes through `docs/user/GETTING_STARTED.md` without questions.

## Phase 5. AI integration — 1 day

- [ ] `ai/skills/loadtest/**` is embedded into `LoadKit.Cli` via a link from the `.csproj` (no copy in the repository).
- [ ] `ai install` (`--global`, `--dir`, `--agents-md`), `ai status`, skill version warning.
- [ ] Skill check with an agent in a clean folder (TargetApi running):
  - without the skill — record what the agent does wrong (baseline);
  - with the skill — "load /api/fast", "with a token via login", "on staging", "50 concurrent";
  - close the loopholes found in `SKILL.md`, repeat.
- [ ] Must be closed: the agent does not write a load script, does not put a secret into JSON, does not skip
  `check`, gets user confirmation before `--yes` for a remote URL.
- [ ] Record the check results in `docs/decisions/` or in the PR — this is material for the demo.

**Done when:** `/loadtest <task>` in a clean project gets to a report without manual scenario edits.

## Phase 6. Packaging, release, demo — 0.5 day

- [ ] `PackAsTool`, `ToolCommandName=loadtest`, `PackageId` (check uniqueness; when publishing to
  nuget.org, use a prefix, e.g. `YourName.LoadKit`).
- [ ] Install from a local feed, run through `GETTING_STARTED.md` from scratch.
- [ ] Document statuses `draft` → `ready` where the code matches.
- [ ] Demo: `/loadtest` → report → charts in Application Insights.

---

## v2 candidates

- Test data: `setup`/`teardown` in the scenario, CSV/JSON feeders (`{{data.userId}}`), `{{runId}}` template.
- Open model (fixed RPS) and ramp-up stages.
- `loadtest compare report-a.json report-b.json`, HTML report with charts.
- Request chains passing data between steps.
- `LoadKit.Desktop` on top of Core (ADR-002).
