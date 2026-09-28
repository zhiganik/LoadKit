# ADR-002: Console application first, Core kept separate

- **Status:** accepted (ready)
- **Date:** 2026-09

## Context

Options considered: a console application, a desktop app (.NET: Avalonia/WPF), a local web application.
Requirements: fast (days, not weeks), convenient for AI assistants, usable in CI.

## Decision

v1 is a console `dotnet tool`. All logic lives in `LoadKit.Core`, which knows nothing about the console.
`LoadKit.Cli` is a thin shell.

## Why

- Desktop or web adds forms, editors, charts and screen state — that makes the timeline 2–3 times longer.
- An AI assistant works with files and commands; windows are not accessible to it.
- A console app is immediately usable in CI (exit codes, thresholds).

## Consequences

- A future shell (for example, `LoadKit.Desktop` on Avalonia) connects to Core without changing it
  and works with the same JSON scenarios.
- Rule for Core: no console output; progress goes through `IProgress<T>`, results are objects.
