# Сценарии: модель, загрузка, валидация

> Статус: draft. Формат для пользователей — `ai/skills/loadtest/SCENARIO_REFERENCE.md`.
> Изменение формата — навык `changing-scenario-format`.

## Файлы

`src/LoadKit.Core/Scenarios/`: `Model/*`, `ScenarioLoader`, `EnvFileLoader`, `TemplateCompiler`,
`ScenarioValidator`, `ValidationCodes`; схема — `schemas/scenario.schema.json`.

## Контракты

- `ScenarioLoader.LoadAsync(path, envFilePath?, ct) → LoadResult` (сценарий или список ошибок).
- `ScenarioValidator.Validate(Scenario) → ValidationResult` — все ошибки сразу, у каждой: код,
  JSON-путь, сообщение, подсказка.
- `CompiledScenario` — сценарий с разобранными шаблонами, готовый для движка.

## Поток

1. Чтение JSON (`System.Text.Json`, комментарии и trailing commas разрешены).
2. Проверка `version`. Неизвестная версия → ошибка с подсказкой обновить инструмент.
3. Структурная валидация по JSON Schema (`additionalProperties: false`).
4. Загрузка `.env`: `--env-file` → `.env` рядом со сценарием → `.env` в `loadtests/`.
   Переменные окружения процесса имеют приоритет над `.env`.
5. Подстановка `${env:NAME}` один раз. Отсутствующие переменные собираются в список.
6. Семантическая валидация.
7. Компиляция шаблонов `{{...}}` в список сегментов (литерал / генератор) — без парсинга в горячем пути.

## Семантические правила

| Код | Правило | Уровень |
|---|---|---|
| `body-required` | POST/PUT/PATCH без `body`/`bodyRaw` и без `allowEmptyBody` | ошибка |
| `body-on-get` | `body` у GET/DELETE | предупреждение |
| `load-mode` | `totalRequests` и `durationSec` одновременно или ни одного | ошибка |
| `concurrency-gt-total` | `concurrency` > `totalRequests` | предупреждение |
| `warmup-gt-total` | `warmup` ≥ `totalRequests` | ошибка |
| `unknown-template` | неизвестный `{{...}}` | ошибка |
| `env-missing` | переменная не задана | ошибка |
| `secret-literal` | похожее на секрет значение в `auth.*` или `Authorization` (`eyJ…`, длинная base64-строка) | ошибка |
| `duplicate-request-name` | повтор `requests[].name` | ошибка |
| `expect-missing` | нет `expect.status` | ошибка |
| `remote-url` | `baseUrl` не localhost | информация (показывается в `check`/`run`) |

## Шаблоны

| Шаблон | Генератор |
|---|---|
| `{{guid}}` | `Guid.NewGuid()` |
| `{{seq}}` | `Interlocked.Increment` по сценарию |
| `{{randomInt:a:b}}` | `Random.Shared.Next(a, b + 1)` |
| `{{now}}` | `TimeProvider.GetUtcNow()` в ISO 8601 |

В `body` строка, целиком состоящая из числового шаблона, сериализуется как число.
Новый шаблон — класс `ITemplateGenerator` + регистрация + строка в справочнике.

## Граничные случаи

- BOM и CRLF в файлах — поддерживаются.
- `.env`: строки `KEY=VALUE`, `#` — комментарий, кавычки снимаются, `export ` в начале игнорируется.
- Значение `${env:X}` внутри строки подставляется как часть строки: `"Bearer ${env:T}"`.
