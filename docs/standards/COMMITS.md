# Коммиты

> Статус: ready

Формат: `type(scope): short description` — английский, повелительное наклонение, без точки в конце.

| type | Когда |
|---|---|
| `feat` | новая возможность |
| `fix` | исправление ошибки |
| `refactor` | без изменения поведения |
| `test` | только тесты |
| `docs` | только документация |
| `build` | сборка, CI, упаковка |
| `chore` | прочее обслуживание |

Scopes: `cli`, `engine`, `auth`, `scenarios`, `metrics`, `reporting`, `schema`, `ai-skill`, `samples`,
`docs`, `deps`, `build`.

Примеры:

```
feat(auth): add azureIdentity provider
fix(metrics): use nearest-rank for p99 on small samples
docs(ai-skill): add login auth example to reference
build(cli): pack as dotnet tool
```

Правила:
- Один scope на коммит, одна логическая задача на коммит.
- Изменение формата сценария (model + schema + reference + tests) — один коммит `feat(scenarios)`,
  это одна логическая задача.
- Запрещено: `fix bug`, `update code`, `misc changes`, `wip`, `temp-commit`.
