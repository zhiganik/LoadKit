# Проверки

> Статус: ready

## Команды

```bash
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

Быстрые варианты:

```bash
dotnet test --filter Category!=Integration   # только unit + docs
dotnet test --filter Category=Docs           # только примеры в документации
```

## Что запускать по типу изменения

| Изменение | build | format | тесты |
|---|---|---|---|
| Логика Core | ✓ | ✓ | полный набор + новый целевой тест |
| Формат сценария | ✓ | ✓ | полный набор, включая Docs |
| Вывод CLI | ✓ | ✓ | интеграционные |
| Только документация | — | — | `Category=Docs` + ручной аудит |
| Рефакторинг | ✓ | ✓ | полный набор |

## CI

GitHub Actions / Azure Pipelines на каждый PR:
1. `dotnet build -warnaserror`
2. `dotnet format --verify-no-changes`
3. `dotnet test` (с интеграционными)
4. `loadtest validate samples/scenarios/*.json` собранным инструментом
5. Проверка, что `src/LoadKit.Cli/AiAssets` совпадает с `ai/skills/loadtest` (скрипт сравнения)

## Pre-commit (опционально)

`dotnet format` на изменённых файлах через Husky.Net или git hook.
