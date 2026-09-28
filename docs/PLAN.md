# План реализации

> Статус: draft. Агент перед задачей находит здесь фазу; по завершении отмечает пункты.
> Ревизия 2: исправлены критерии готовности, валидация без сторонней JSON Schema-библиотеки,
> поведение без терминала, встраивание навыка без копирования.

Один разработчик с AI-агентом, **5 рабочих дней**. Каждая фаза заканчивается рабочим, проверенным
результатом и отдельными коммитами по `docs/standards/COMMITS.md`.

## Раскладка по дням

| День | Фазы | Результат дня |
|---|---|---|
| 1 | 0 + 1 | каркас, TargetApi, `loadtest validate` работает |
| 2 | 2 | `loadtest run` с bearer/apiKey, перцентили в консоли |
| 3 | 3 | все типы авторизации, `loadtest check` |
| 4 | 4 + начало 5 | отчёты, `init`, `ai install` |
| 5 | конец 5 + 6 | проверка навыка на агенте, упаковка, демо |

Если день затянулся — резерв берётся из фазы 4 (гистограмма, предупреждения о CPU переносятся в v2).

---

## Фаза 0. Основа репозитория — 0.5 дня

- [x] Документация, `AGENTS.md`, `CLAUDE.md`, навыки, `.gitignore`, `.gitattributes`.
- [ ] `LoadKit.sln`: `src/LoadKit.Core`, `src/LoadKit.Cli`, `samples/TargetApi`,
  `tests/LoadKit.Core.Tests`, `tests/LoadKit.IntegrationTests`.
- [ ] `Directory.Build.props` (net10.0, nullable, warnings as errors), `Directory.Packages.props`, `.editorconfig`.
- [ ] `samples/TargetApi` на `http://localhost:5080` со всеми endpoint'ами из `docs/standards/TESTING.md`,
  включая `/secure`, `/auth/login`, `/oauth2/token`.
- [ ] `samples/scenarios/`: `smoke.json`, `mix.json`, `secure-bearer.json`, `secure-login.json`.
- [ ] CI (`.github/workflows/ci.yml`): build, format, test.

**Готово, когда:** CI зелёный; `curl localhost:5080/health` → 200; `/secure` без токена → 401.

## Фаза 1. Сценарии и `validate` — 0.5 дня

- [ ] Модель сценария (records), `ScenarioLoader`, `EnvFileLoader`.
- [ ] `ScenarioValidator` поверх `JsonDocument`: неизвестные поля, типы, обязательные поля и все
  семантические правила из `docs/architecture/SCENARIOS.md`. Собирает **все** ошибки с JSON-путями.
- [ ] Проверка `secret-literal` — **до** подстановки `${env:}` (после подстановки секрет уже в значении).
- [ ] Подстановка `${env:}`, `TemplateCompiler` и генераторы шаблонов.
- [ ] `schemas/scenario.schema.json` — только для автодополнения в IDE; тест согласованности схемы и модели.
- [ ] Команда `validate`: список ошибок, exit 2.
- [ ] Docs-тесты: полные сценарии и фрагменты (`<!-- fragment:auth -->`) из `SCENARIO_REFERENCE.md`,
  `samples/scenarios/*`.

**Готово, когда:** сценарий с тремя разными ошибками показывает все три с путями и подсказками, exit 2;
все примеры из документации проходят.

## Фаза 2. Движок, метрики, `run` — 1 день

- [ ] `HttpPipelineFactory`, `LoadRunner`, `WeightedRequestPicker`, `RequestFactory`.
- [ ] `ResultCollector`; `PercentileCalculator` на **целочисленной** арифметике ранга
  (тесты: N=2000 → p95 = 1900-й, p99 = 1980-й; N=1; одинаковые значения).
- [ ] `ThresholdEvaluator`, коды выхода 0/1.
- [ ] Auth: `IAuthProvider`, `AuthHandler`, `bearer`, `apiKey` (заголовок и query), `SecretMasker`.
- [ ] `run`: живой прогресс, итоговая таблица по запросам и общая, статус-коды, примеры ошибок.
- [ ] Не-localhost URL: в терминале — вопрос; без терминала и без `--yes` — exit 4 с подсказкой.
- [ ] `Ctrl+C` → отчёт по собранным данным, пометка «прервано», exit 130.

**Готово, когда:** `run samples/scenarios/mix.json` против TargetApi показывает p50/p95/p99,
ошибки `/api/fail` посчитаны как ожидаемые или неожиданные по `expect.status`.

## Фаза 3. Авторизация полностью и `check` — 1 день

- [ ] `TokenAuthProviderBase`: кэш, фоновое обновление на ~80% срока жизни, `MarkStale`,
  одно обновление при параллельных запросах.
- [ ] `login` (против `/auth/login`), `oauth2ClientCredentials` (против `/oauth2/token` TargetApi).
- [ ] `azureIdentity`: `AzureCliCredential` по умолчанию, `source: "default"` → `DefaultAzureCredential`.
- [ ] Preflight: первый токен + доступность `baseUrl`, exit 3 с конкретной подсказкой.
- [ ] Команда `check`.
- [ ] Тесты: `FakeTimeProvider` для обновления; fake `TokenCredential` для `azureIdentity`;
  токен TargetApi со сроком 10 секунд в 30-секундном прогоне → нет 401 и нет всплеска p99.

**Готово, когда:** `bearer`, `apiKey`, `login`, `oauth2ClientCredentials` проходят против `/secure`
TargetApi; `azureIdentity` покрыт unit-тестами и один раз проверен вручную на реальном API
(если есть API в Entra ID; иначе — отмечено как «проверено только тестами»).

## Фаза 4. Отчёты и `init` — 0.5 дня

- [ ] `RunReport`, `report.md`, `report.json`, KQL по `loadrun`.
- [ ] `tagRuns`, предупреждения: частые 401, прерывание, CPU тестера > 85% (можно перенести в v2).
- [ ] Гистограмма (можно перенести в v2).
- [ ] `init`: интерактивно в терминале, флагами без терминала; создаёт сценарий, `loadtests/.env` с пустыми
  переменными, копирует `loadtests/scenario.schema.json`, дописывает `.gitignore`.

**Готово, когда:** новый пользователь проходит `docs/user/GETTING_STARTED.md` без вопросов.

## Фаза 5. AI-интеграция — 1 день

- [ ] `ai/skills/loadtest/**` встраивается в `LoadKit.Cli` ссылкой из `.csproj` (без копии в репозитории).
- [ ] `ai install` (`--global`, `--dir`, `--agents-md`), `ai status`, предупреждение о версии навыка.
- [ ] Проверка навыка с агентом в чистой папке (TargetApi запущен):
  - без навыка — записать, что агент делает не так (базовая линия);
  - с навыком — «нагрузи /api/fast», «с токеном через логин», «на staging», «50 параллельных»;
  - закрыть найденные лазейки в `SKILL.md`, повторить.
- [ ] Обязательно закрыто: агент не пишет скрипт нагрузки, не вставляет секрет в JSON, не пропускает
  `check`, получает подтверждение пользователя перед `--yes` для удалённого URL.
- [ ] Записать результаты проверки в `docs/decisions/` или в PR — это материал для демо.

**Готово, когда:** `/loadtest <задача>` в чистом проекте доводит до отчёта без ручных правок сценария.

## Фаза 6. Упаковка, релиз, демо — 0.5 дня

- [ ] `PackAsTool`, `ToolCommandName=loadtest`, `PackageId` (проверить уникальность; при публикации на
  nuget.org — с префиксом, например `YourName.LoadKit`).
- [ ] Установка из локального фида, прогон `GETTING_STARTED.md` с нуля.
- [ ] Статусы документов `draft` → `ready` там, где код совпадает.
- [ ] Демо: `/loadtest` → отчёт → графики в Application Insights.

---

## Кандидаты v2

- Тестовые данные: `setup`/`teardown` в сценарии, фидеры из CSV/JSON (`{{data.userId}}`), шаблон `{{runId}}`.
- Открытая модель (фиксированный RPS) и стадии разгона.
- `loadtest compare report-a.json report-b.json`, HTML-отчёт с графиками.
- Цепочки запросов с передачей данных между шагами.
- `LoadKit.Desktop` поверх Core (ADR-002).
