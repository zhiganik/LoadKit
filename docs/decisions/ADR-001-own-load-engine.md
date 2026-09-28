# ADR-001: Own load engine instead of existing tools

- **Status:** accepted (ready)
- **Date:** 2026-09

## Context

While setting up monitoring for a Web App and a Function App in Azure (Application Insights,
dashboards, workbooks, alerts), we regularly need to generate load on an API in order to:

- check that telemetry, percentiles and errors are displayed correctly;
- compare performance before and after changes during local development;
- reproduce problems (slow endpoints, errors under load).

Today, load test code is written from scratch every time, and the report is assembled by hand.
This is slow, not reusable and hard to hand over to colleagues.

Requirements for the solution:

1. Load is described declaratively (a scenario file), not in code.
2. Auth support, including tokens with a limited lifetime.
3. A report with p50 / p95 / p99 and an error breakdown.
4. An AI assistant can write scenarios.
5. Usable inside an organization without licensing restrictions or costs.
6. Runs locally, installs with one command; the team writes .NET.
7. Can be built in 1–3 days.

## Options considered

### NBomber

A .NET load testing framework; scenarios are written in C#.

- ➕ Mature, .NET-native, good reports.
- ➖ **License:** since v5, NBomber is free only for personal use.
  Use within an organization requires a commercial subscription.
- ➖ v4 remains under Apache 2.0, but it is outdated and no longer developed.
- ➖ Scenarios are C# code. A declarative JSON format would have to be built on top anyway.

**Conclusion:** fails requirement 5; requirement 1 would need our own wrapper anyway.

### k6

An open-source tool with scenarios in JavaScript.

- ➕ An excellent tool, free, good documentation, load stages, thresholds.
- ➖ A separate runtime and language (JS), not .NET.
- ➖ A scenario is a script, not data; overkill for simple cases.

**Conclusion:** remains the recommended tool for full-scale load tests.
Heavier than needed for a quick local check.

### Azure Load Testing

A managed cloud service; supports JMeter and Locust.

- ➕ Scale, Azure Monitor integration, runs from CI.
- ➖ Paid, runs in the cloud, requires setting up a resource.
- ➖ Not designed for quick local runs during development.

**Conclusion:** for pre-production and large tests; overkill for local development.

### Apache JMeter

- ➕ Free, very feature-rich.
- ➖ Heavy (Java, GUI, XML scenarios), steep learning curve, awkward for AI to generate scenarios.

**Conclusion:** overkill.

### Postman (Performance testing)

- ➕ Many people already use Postman.
- ➖ Tied to the Postman ecosystem and account, limited configuration and export options.

**Conclusion:** not suitable as a shared team tool.

### oha / bombardier / hey

Console utilities: "one command — one URL".

- ➕ Instant start, show percentiles.
- ➖ One endpoint per run, no weighted request mix, no token refresh, no thresholds,
  no scenarios in files.

**Conclusion:** good for a one-off measurement, but do not cover requirements 1–4.

### Own CLI tool (LoadKit)

- ➕ Declarative JSON scenario with JSON Schema and validation.
- ➕ Auth for our cases (Entra ID, function keys, custom login).
- ➕ No licensing restrictions: dependencies only under MIT / Apache 2.0.
- ➕ Instructions and a schema for AI; scenarios generated without programming.
- ➕ Small size: the engine core is about 200–300 lines.
- ➖ We have to maintain the code ourselves.
- ➖ Not suitable for large and distributed loads.
- ➖ Load from a single machine: the tester and the application may share resources.

## Decision

We build our own CLI tool, LoadKit, with a minimal feature set (see README).
The load engine is hidden behind an interface so it can be replaced if needed
(for example, with NBomber if a license is bought) without changing the scenario format.

For production load and large volumes we still use k6 or Azure Load Testing.

## Consequences

- Development: 1–3 days for v1.
- Maintenance: the team owns the tool; dependencies are updated together with other projects.
- Load scenarios are stored in service repositories next to the code.
- Local run results are used for "before / after" comparison, not as absolute numbers
  for production performance.
