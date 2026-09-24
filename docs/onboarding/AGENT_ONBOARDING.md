# Онбординг агента

> Статус: ready

Всегда: `AGENTS.md` (уже в контексте через `CLAUDE.md`). Дальше — только по типу задачи.

| Тип задачи | Читать |
|---|---|
| Непонятен контекст | `docs/architecture/OVERVIEW.md`, `docs/PLAN.md` |
| Логика Core | `docs/standards/ARCHITECTURE.md`, `STYLE_CSHARP.md`, документ компонента в `docs/architecture/` |
| Новый auth-тип | навык `adding-auth-provider`, `docs/architecture/AUTH.md` |
| Формат сценария | навык `changing-scenario-format`, `docs/architecture/SCENARIOS.md` |
| Движок, перцентили | `docs/architecture/ENGINE.md`, `docs/standards/TESTING.md` |
| CLI, отчёты | `docs/architecture/REPORTING_AND_CLI.md`, `docs/user/CLI_REFERENCE.md` |
| Навык для пользователей | `docs/architecture/AI_INTEGRATION.md`, `ai/skills/loadtest/*` |
| Тесты | `docs/standards/TESTING.md`, `VERIFICATION.md` |
| Документация | `docs/standards/DOCUMENTATION.md` |
| Коммит | `docs/standards/COMMITS.md` |

## Источник истины

`AGENTS.md` → `docs/standards` → принятые ADR → `docs/architecture` → `docs/user`, навык → код.
Расхождение документа и кода — сообщить в отчёте и исправить документ в том же PR.

## Перед нетривиальной задачей

1. Найти фазу в `docs/PLAN.md`.
2. Короткий план изменений и тест-аудит в чате.
3. Проверить, не затрагивается ли публичный контракт (`docs/standards/ARCHITECTURE.md`).
