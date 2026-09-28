# Verification

> Status: ready

## Commands

```bash
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

Fast variants:

```bash
dotnet test --filter Category!=Integration   # unit + docs only
dotnet test --filter Category=Docs           # documentation examples only
```

## What to run by change type

| Change | build | format | tests |
|---|---|---|---|
| Core logic | ✓ | ✓ | full suite + new targeted test |
| Scenario format | ✓ | ✓ | full suite, including Docs |
| CLI output | ✓ | ✓ | integration |
| Documentation only | — | — | `Category=Docs` + manual audit |
| Refactoring | ✓ | ✓ | full suite |

## CI

`.github/workflows/ci.yml` (GitHub Actions, `ubuntu-latest`) on every push to `main` and every PR:
1. `dotnet build -warnaserror`
2. `dotnet format --verify-no-changes`
3. `dotnet test` (including integration), TRX results; failed tests become annotations on the run page
4. `loadtest validate samples/scenarios/*.json` with the built tool
5. `dotnet pack` the tool, install it from the local package, smoke-test `--version`, `validate`, `ai install`
6. Upload the `.nupkg` as the `loadkit-nupkg` artifact

`SkillInstallerTests` check that the built CLI embeds exactly the `ai/skills/loadtest/**` files and that the skill
version matches the tool version.

CI sets `GITHUB_ACTIONS=true`. Spectre.Console reacts to CI variables, so CLI output tests must pass with them:
`PlainOutputTests` cover that. To reproduce CI locally with Docker:

```bash
docker run --rm -e GITHUB_ACTIONS=true -v "$PWD:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0   bash -c "dotnet build -warnaserror && dotnet test"
```

## Release

`.github/workflows/release.yml` runs on a tag `vX.Y.Z` equal to `<Version>` in `Directory.Build.props`:
build, test, pack, `dotnet nuget push` to nuget.org (repository secret `NUGET_API_KEY`), and a GitHub release with the
`.nupkg`. Steps: bump `<Version>` (and `loadkit-version` in `ai/skills/loadtest/SKILL.md` for a new major.minor — a test
enforces it), commit, `git tag v1.0.0 && git push origin v1.0.0`.

## Pre-commit (optional)

`dotnet format` on changed files via Husky.Net or a git hook.
