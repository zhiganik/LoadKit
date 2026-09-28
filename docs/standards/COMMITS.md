# Commits

> Status: ready

Format: `type(scope): short description` — English, imperative mood, no trailing period.

| type | When |
|---|---|
| `feat` | new capability |
| `fix` | bug fix |
| `refactor` | no behavior change |
| `test` | tests only |
| `docs` | documentation only |
| `build` | build, CI, packaging |
| `chore` | other maintenance |

Scopes: `cli`, `engine`, `auth`, `scenarios`, `metrics`, `reporting`, `schema`, `ai-skill`, `samples`,
`docs`, `deps`, `build`.

Examples:

```
feat(auth): add azureIdentity provider
fix(metrics): use nearest-rank for p99 on small samples
docs(ai-skill): add login auth example to reference
build(cli): pack as dotnet tool
```

Rules:
- One scope per commit, one logical task per commit.
- A scenario format change (model + schema + reference + tests) is one `feat(scenarios)` commit,
  since it is one logical task.
- Forbidden: `fix bug`, `update code`, `misc changes`, `wip`, `temp-commit`.
