# C# style

> Status: ready

## Project

- `net10.0`, `<LangVersion>latest</LangVersion>`, `<Nullable>enable</Nullable>`,
  `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- Shared settings live in `Directory.Build.props`, package versions in `Directory.Packages.props`
  (Central Package Management).
- Formatting: `.editorconfig` + `dotnet format`. Disputed cases are decided by `dotnet format`, not by taste.

## Code

- File-scoped namespaces. One public type per file, file name = type name.
- Classes are `sealed` by default. Inheritance only when deliberate (`TokenAuthProviderBase`).
- Data models are `record` / `readonly record struct`. Mutable state only inside the engine.
- No static mutable state. `Random.Shared` and `TimeProvider` are passed as parameters
  so tests are deterministic.
- Async all the way: no `.Result`, `.Wait()`, `async void`.
- `CancellationToken` is the last parameter of every async method in Core and is always passed on.
- `ConfigureAwait(false)` is not required in `LoadKit.Core` (console app without a synchronization context).
- Exceptions: user errors (invalid scenario, missing variable) are not exceptions but a
  `ValidationResult` with clear messages. Exceptions are only for truly exceptional situations.

## Naming

- Explicit names: `requestDurationTicks`, not `d`; `scenarioFilePath`, not `path2`.
- Async methods have the `Async` suffix.
- Interfaces `I*` only if there is more than one implementation or a test substitute is needed.
- Validation error codes are `ValidationCodes.*` constants in `kebab-case` (`body-required`).

## Hot path (request sending loop)

- No locks, LINQ, string allocations for parsing, `string.Format`.
- Templates are parsed in advance; a request is assembled from prepared parts.
- Timing uses `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime()`.

## Output and logging

- `LoadKit.Core` does not write to the console and does not depend on Spectre.Console.
- Progress goes through `IProgress<RunProgress>`, events through returned objects.
- Secrets are never logged; values pass through `SecretMasker` before output.
