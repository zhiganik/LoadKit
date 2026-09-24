# Онбординг разработчика

> Статус: draft

## Требования

- .NET 10 SDK
- Azure CLI (для проверки `azureIdentity`)
- IDE: Rider / Visual Studio / VS Code с C# Dev Kit
- Опционально: Claude Code или другой AI-агент

## Первый запуск

```bash
git clone <repo> && cd LoadKit
dotnet build
dotnet test
dotnet run --project samples/TargetApi                       # в отдельном терминале
dotnet run --project src/LoadKit.Cli -- run samples/scenarios/smoke.json
```

## Локальная установка как tool

```bash
dotnet pack src/LoadKit.Cli -c Release -o ./artifacts
dotnet tool install -g LoadKit --add-source ./artifacts
```

## Что прочитать

1. `README.md` — зачем инструмент.
2. `docs/decisions/*` — почему он такой.
3. `docs/architecture/OVERVIEW.md` — как устроен.
4. `AGENTS.md` — правила кода, тестов и коммитов.
5. `docs/PLAN.md` — что делаем сейчас.

## Работа с AI-агентом в этом репозитории

`CLAUDE.md` подключает `AGENTS.md`. Навыки `/adding-auth-provider` и `/changing-scenario-format`
доступны в Claude Code. Попросить агента: «возьми следующую задачу из фазы 2 PLAN.md» — он найдёт
нужные документы по `AGENT_ONBOARDING.md`.
