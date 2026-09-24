# Документация LoadKit

> Статус: ready

Документация устроена в три слоя; при конфликте побеждает верхний (полный порядок — в `AGENTS.md`).

1. **Навигация** — `AGENTS.md`, `CLAUDE.md`.
2. **Правила и устройство** — `docs/`.
3. **Продукт для пользователей** — `docs/user/`, `ai/skills/loadtest/`.

Правила оформления: `standards/DOCUMENTATION.md`.

## Индекс

### Онбординг
- `onboarding/AGENT_ONBOARDING.md` — что читать агенту под тип задачи.
- `onboarding/HUMAN_ONBOARDING.md` — быстрый старт разработчика.

### Стандарты
- `standards/STYLE_CSHARP.md` — стиль C#, горячий путь.
- `standards/ARCHITECTURE.md` — слои, модули, публичные контракты, лицензии зависимостей.
- `standards/DOCUMENTATION.md` — куда класть документ, язык, статус, навыки.
- `standards/TESTING.md` — уровни тестов, обязательные тесты, TargetApi.
- `standards/VERIFICATION.md` — команды проверки, CI.
- `standards/COMMITS.md` — формат коммитов.

### Архитектура
- `architecture/OVERVIEW.md` — компоненты, поток выполнения, структура решения.
- `architecture/SCENARIOS.md` — модель, загрузка, валидация, шаблоны.
- `architecture/AUTH.md` — провайдеры, обновление токенов, безопасность.
- `architecture/ENGINE.md` — движок, замер, перцентили, пороги.
- `architecture/REPORTING_AND_CLI.md` — отчёты, команды, интерактивный init.
- `architecture/AI_INTEGRATION.md` — навык `loadtest`, поставка, версионирование.

### Решения
- `decisions/ADR-001-own-load-engine.md` — свой движок вместо NBomber, k6 и др.
- `decisions/ADR-002-console-first.md` — консоль сначала, Core отдельно.
- `decisions/ADR-003-ai-skill-distribution.md` — навык внутри инструмента.

### Для пользователей
- `user/GETTING_STARTED.md` — первый запуск и авторизация.
- `user/CLI_REFERENCE.md` — команды и флаги.
- `../ai/skills/loadtest/SCENARIO_REFERENCE.md` — формат сценария.

### Прочее
- `PLAN.md` — фазы реализации и статус.
- `glossary/TERMS.md` — термины.
