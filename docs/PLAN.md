# План реализации

> Статус: draft. Агент перед задачей находит здесь фазу; по завершении фазы отмечает пункты.

Оценка — при работе с AI-агентом, один разработчик. Каждая фаза заканчивается рабочим, проверенным
результатом и отдельными коммитами.

## Фаза 0. Основа репозитория — 0.5 дня

- [ ] Решение `LoadKit.sln`: `LoadKit.Core`, `LoadKit.Cli`, `samples/TargetApi`, два тестовых проекта.
- [ ] `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`.
- [ ] Документация из этого набора в репозитории; `CLAUDE.md`, `AGENTS.md`, навыки.
- [ ] CI: build, format, test.
- [ ] `samples/TargetApi` с endpoint'ами из `docs/standards/TESTING.md`.

**Готово, когда:** `dotnet build/format/test` зелёные в CI, TargetApi отвечает.

## Фаза 1. Сценарии и `validate` — 0.5–1 день

- [ ] Модель сценария, `ScenarioLoader`, `EnvFileLoader`, подстановка `${env:}`.
- [ ] `schemas/scenario.schema.json`, структурная валидация.
- [ ] `ScenarioValidator` со всеми правилами из `docs/architecture/SCENARIOS.md`.
- [ ] `TemplateCompiler` и генераторы шаблонов.
- [ ] Команда `validate` с выводом всех ошибок и JSON-путей.
- [ ] Docs-тесты: примеры из `SCENARIO_REFERENCE.md` и `samples/scenarios` валидны.

**Готово, когда:** невалидный сценарий даёт понятный список ошибок и exit 2.

## Фаза 2. Движок, метрики, `run` — 1 день

- [ ] `HttpPipelineFactory`, `LoadRunner`, `WeightedRequestPicker`, `RequestFactory`.
- [ ] `ResultCollector`, `PercentileCalculator` (тесты на 2000 значений), `ThresholdEvaluator`.
- [ ] Auth: `bearer`, `apiKey`, `AuthHandler`, `SecretMasker`.
- [ ] Команда `run`: живой прогресс, итоговая таблица, коды выхода 0/1, `Ctrl+C`.
- [ ] Подтверждение для не-localhost URL, флаг `--yes`.

**Готово, когда:** `run` против TargetApi показывает p50/p95/p99 и ошибки по кодам.

## Фаза 3. Авторизация полностью и `check` — 0.5–1 день

- [ ] `TokenAuthProviderBase`: кэш, фоновое обновление, `MarkStale`.
- [ ] `azureIdentity`, `oauth2ClientCredentials`, `login`.
- [ ] Preflight, exit 3 с понятными подсказками.
- [ ] Команда `check`.
- [ ] Тесты обновления токена через `FakeTimeProvider` и `/auth/login` TargetApi.

**Готово, когда:** все пять типов работают против `/secure`, истечение токена не даёт всплесков p99.

## Фаза 4. Отчёты и `init` — 0.5 дня

- [ ] `RunReport`, `report.md`, `report.json`, гистограмма, KQL по `loadrun`.
- [ ] `tagRuns`, предупреждения (CPU, 401, прерывание).
- [ ] Интерактивный и флаговый `init`, `.env`-шаблон, `.gitignore`.

**Готово, когда:** новый пользователь проходит `docs/user/GETTING_STARTED.md` без вопросов.

## Фаза 5. AI-интеграция — 0.5–1 день

- [ ] Встраивание `ai/skills/loadtest` в `Cli/AiAssets`, CI-проверка совпадения.
- [ ] `ai install` (`--global`, `--dir`, `--agents-md`), `ai status`, предупреждение о версии.
- [ ] Проверка навыка с агентом в чистой папке с TargetApi:
  - без навыка (базовое поведение) — записать, что агент делает не так;
  - с навыком — те же запросы: «нагрузи /api/orders», «с токеном», «на staging», «50 параллельных»;
  - закрыть найденные лазейки в `SKILL.md`, повторить.
- [ ] Обязательно закрыто: агент не пишет скрипт нагрузки, не вставляет секрет в JSON, не пропускает
  `check`, спрашивает подтверждение для удалённого URL.

**Готово, когда:** `/loadtest <задача>` в чистом проекте доводит до отчёта без ручных правок сценария.

## Фаза 6. Упаковка и релиз — 0.5 дня

- [ ] `PackAsTool`, `ToolCommandName=loadtest`, версия из тега.
- [ ] Публикация во внутренний NuGet-фид (или nuget.org).
- [ ] README: установка, 5 минут до первого прогона, ссылка на навык.
- [ ] Статусы документов `draft` → `ready` там, где код совпадает.

**Итого:** 4–5 рабочих дней. Минимально полезная версия (фазы 0–2) — 2–2.5 дня.

## Кандидаты v2

- Открытая модель (фиксированный RPS) и стадии разгона.
- `loadtest compare report-a.json report-b.json`.
- HTML-отчёт с графиками.
- Цепочки запросов с передачей данных между шагами.
- `LoadKit.Desktop` поверх Core (ADR-002).
