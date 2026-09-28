# Сценарии: модель, загрузка, валидация

> Статус: draft. Формат для пользователей — `ai/skills/loadtest/SCENARIO_REFERENCE.md`.
> Изменение формата — навык `changing-scenario-format`.

## Файлы

`src/LoadKit.Core/Scenarios/`: `Model/*`, `ScenarioLoader`, `EnvFileLoader`, `TemplateCompiler`,
`ScenarioValidator`, `ValidationCodes`; схема для IDE — `schemas/scenario.schema.json`.

## Валидация без сторонней JSON Schema-библиотеки

Источник истины — `ScenarioValidator`, который обходит `JsonDocument` и собирает **все** ошибки
(неизвестные поля, типы, обязательные поля, семантика) с JSON-путями. Файл `scenario.schema.json`
нужен только для автодополнения и подсветки в IDE.

Почему не библиотека: популярные варианты имеют ограничения для коммерческого использования
(Newtonsoft.Json.Schema — лимит бесплатных валидаций, JsonSchema.Net — плата за сопровождение по новой EULA),
а своя проверка фиксированной структуры — небольшой объём кода и лучшие сообщения об ошибках.
Расхождение схемы и модели ловит тест согласованности (каждое поле модели есть в схеме и наоборот).

## Контракты

- `ScenarioLoader.LoadAsync(path, envFilePath?, ct) → LoadResult` (сценарий или список ошибок).
- `ScenarioValidator.Validate(Scenario) → ValidationResult` — все ошибки сразу, у каждой: код,
  JSON-путь, сообщение, подсказка.
- `CompiledScenario` — сценарий с разобранными шаблонами, готовый для движка.

## Поток

1. Чтение JSON в `JsonDocument` (комментарии и trailing commas разрешены).
2. Проверка `version`. Неизвестная версия → ошибка с подсказкой обновить инструмент.
3. Структурная проверка: неизвестные поля (кроме `$schema`), типы, обязательные поля.
4. Проверка `secret-literal` на **сырых** значениях — до подстановки env, иначе секрет из `.env`
   выглядел бы как секрет, записанный в файл.
5. Загрузка `.env`: `--env-file` → `.env` рядом со сценарием → `.env` в родительской папке (`loadtests/`).
   Переменные окружения процесса имеют приоритет над `.env`.
6. Подстановка `${env:NAME}` один раз. Отсутствующие переменные собираются в список.
7. Семантические правила.
8. Если ошибок нет — десериализация в модель и компиляция шаблонов `{{...}}` в сегменты
   (литерал / генератор), чтобы в горячем пути не было парсинга.

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
| `secret-literal` | в `auth.token`, `auth.value`, `auth.clientSecret`, пароле в `auth.request.body` или заголовке `Authorization` стоит литерал, а не `${env:}` | ошибка |
| `duplicate-request-name` | повтор `requests[].name` | ошибка |
| `expect-missing` | нет `expect.status` | ошибка |
| `remote-url` | `baseUrl` не localhost | информация; `run` требует подтверждения (или `--yes`) |
| `apikey-target` | у `apiKey` нет или оба `header`/`query` | ошибка |
| `apikey-in-query` | `apiKey` через `query` | предупреждение: ключ попадёт в URL и логи сервера |

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
- `$schema` — единственное поле вне формата, которое разрешено и игнорируется.
- `init` копирует схему в `loadtests/scenario.schema.json` и ставит в сценарий
  `"$schema": "../scenario.schema.json"`, чтобы автодополнение работало без интернета.
