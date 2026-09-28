# Формат сценария LoadKit (version 1)

> Статус: draft. Справочник для людей и AI-ассистентов. Правила работы — в [SKILL.md](SKILL.md).
> Все JSON-примеры в этом файле автоматически проверяются тестами (`Category=Docs`).

## Структура

```json
{
  "$schema": "../scenario.schema.json",
  "version": 1,
  "name": "orders-smoke",
  "description": "Что проверяет сценарий",
  "baseUrl": "https://localhost:5001",
  "headers": { "Accept": "application/json" },
  "auth": { "type": "bearer", "token": "${env:API_TOKEN}" },
  "tagRuns": true,
  "load": { "concurrency": 10, "totalRequests": 500, "warmup": 20 },
  "requests": [
    { "name": "list", "method": "GET", "path": "/api/orders", "expect": { "status": [200] } }
  ],
  "thresholds": { "p95Ms": 500, "errorRatePercent": 1 }
}
```

`$schema` необязателен и даёт автодополнение в IDE. `loadtest init` кладёт схему в
`loadtests/scenario.schema.json` и прописывает относительный путь.

## Корневые поля

| Поле | Обяз. | По умолчанию | Описание |
|---|---|---|---|
| `version` | да | — | версия формата, сейчас `1` |
| `name` | да | — | имя сценария, попадает в отчёт |
| `description` | нет | — | что проверяет сценарий |
| `baseUrl` | да | — | адрес API; поддерживает `${env:...}` |
| `headers` | нет | `{}` | заголовки для всех запросов |
| `auth` | нет | без авторизации | см. «Авторизация» |
| `tagRuns` | нет | `true` | добавлять `loadrun=<id>` в query для фильтрации в Application Insights |
| `load` | да | — | параметры нагрузки |
| `requests` | да | — | минимум один запрос |
| `thresholds` | нет | — | пороги, влияющие на код выхода |

## `load`

| Поле | Обяз. | По умолчанию | Описание |
|---|---|---|---|
| `concurrency` | да | — | сколько запросов одновременно «в полёте» |
| `totalRequests` | одно из двух | — | сколько всего запросов |
| `durationSec` | одно из двух | — | длительность прогона в секундах |
| `warmup` | нет | `0` | первые N запросов не учитываются в метриках |
| `timeoutMs` | нет | `30000` | таймаут одного запроса |

`concurrency` — это не «запросов в секунду». 20 означает, что 20 воркеров шлют следующий запрос сразу
после ответа на предыдущий.

## `requests[]`

| Поле | Обяз. | По умолчанию | Описание |
|---|---|---|---|
| `name` | да | — | уникальное имя, попадает в отчёт |
| `method` | да | — | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| `path` | да | — | путь от `baseUrl`, шаблоны разрешены |
| `weight` | нет | `1` | доля в миксе (веса 70/30 = 7/3) |
| `headers` | нет | — | дополняют корневые заголовки |
| `query` | нет | — | объект query-параметров |
| `body` | для POST/PUT/PATCH | — | JSON-объект |
| `bodyRaw` | нет | — | тело строкой вместо `body` |
| `contentType` | нет | `application/json` | тип для `bodyRaw` |
| `allowEmptyBody` | нет | `false` | разрешить POST/PUT/PATCH без тела |
| `auth` | нет | `true` | `false` — отправить без авторизации |
| `expect.status` | да | — | допустимые коды, например `[200]` |
| `expect.maxMs` | нет | — | более медленный ответ считается ошибкой |

## `thresholds`

`p50Ms`, `p95Ms`, `p99Ms` — максимум для перцентиля по всем запросам; `errorRatePercent` — максимум
процента ошибок. Нарушение любого порога → код выхода `1`.

## Авторизация

Токен получается **до старта** нагрузки и обновляется в фоне, на метрики это не влияет.
Секреты — только `${env:ИМЯ}`; значения лежат в `loadtests/.env`.

| Тип | Обязательные поля | Необязательные |
|---|---|---|
| `bearer` | `token` | `header` (`Authorization`), `format` (`Bearer {token}`) |
| `apiKey` | `value` + одно из `header` / `query` | — |
| `azureIdentity` | `scope` | `source`: `azureCli` (по умолчанию) или `default` |
| `oauth2ClientCredentials` | `tokenUrl`, `clientId`, `clientSecret`, `scope` | — |
| `login` | `request`, `tokenPath` | `expiresInPath`, `header` (`Authorization`), `format` (`Bearer {token}`) |

`apiKey` лучше передавать заголовком: значение в `query` попадает в URL, а URL пишется в логи сервера.

<!-- fragment:auth -->
```json
{ "type": "bearer", "token": "${env:API_TOKEN}" }
```

<!-- fragment:auth -->
```json
{ "type": "apiKey", "header": "x-functions-key", "value": "${env:FUNC_KEY}" }
```

<!-- fragment:auth -->
```json
{ "type": "apiKey", "query": "code", "value": "${env:FUNC_KEY}" }
```

<!-- fragment:auth -->
```json
{ "type": "azureIdentity", "scope": "api://my-api/.default" }
```

Если `check` падает с AADSTS65001 и упоминанием «Microsoft Azure CLI»: API не разрешает Azure CLI получать
токены. Владелец app registration API добавляет client id `04b07795-8ddb-461a-bbee-02f9e1bf7b46`
в Expose an API → Authorized client applications.

<!-- fragment:auth -->
```json
{
  "type": "oauth2ClientCredentials",
  "tokenUrl": "https://login.microsoftonline.com/${env:TENANT_ID}/oauth2/v2.0/token",
  "clientId": "${env:CLIENT_ID}",
  "clientSecret": "${env:CLIENT_SECRET}",
  "scope": "api://my-api/.default"
}
```

<!-- fragment:auth -->
```json
{
  "type": "login",
  "request": {
    "method": "POST",
    "path": "/api/auth/login",
    "body": { "email": "${env:TEST_USER}", "password": "${env:TEST_PASSWORD}" }
  },
  "tokenPath": "$.accessToken",
  "expiresInPath": "$.expiresIn",
  "format": "Bearer {token}"
}
```

## Подстановки

| Синтаксис | Когда вычисляется | Пример |
|---|---|---|
| `${env:NAME}` | один раз при загрузке | значение из окружения или `.env` |
| `{{guid}}` | на каждый запрос | `3f2b8c1e-...` |
| `{{seq}}` | на каждый запрос | `1`, `2`, `3` |
| `{{randomInt:MIN:MAX}}` | на каждый запрос | `42` (границы включительно) |
| `{{now}}` | на каждый запрос | `2026-09-24T10:15:30Z` |

Работают в `path`, `query`, `headers`, `body`, `bodyRaw`. В `body` строка, целиком состоящая из
`{{seq}}` или `{{randomInt:...}}`, становится числом.

## Примеры

### Смоук без авторизации

```json
{
  "version": 1,
  "name": "health-smoke",
  "baseUrl": "http://localhost:5080",
  "load": { "concurrency": 5, "totalRequests": 200 },
  "requests": [
    { "name": "health", "method": "GET", "path": "/health", "expect": { "status": [200] } }
  ],
  "thresholds": { "p95Ms": 200, "errorRatePercent": 0 }
}
```

### Azure Function с ключом

```json
{
  "version": 1,
  "name": "func-process",
  "baseUrl": "https://my-func.azurewebsites.net",
  "auth": { "type": "apiKey", "header": "x-functions-key", "value": "${env:FUNC_KEY}" },
  "load": { "concurrency": 10, "totalRequests": 500, "warmup": 20 },
  "requests": [
    {
      "name": "process",
      "method": "POST",
      "path": "/api/process",
      "body": { "id": "{{guid}}", "amount": "{{randomInt:1:1000}}" },
      "expect": { "status": [200, 202] }
    }
  ]
}
```

### Микс запросов, Entra ID через `az login`

```json
{
  "version": 1,
  "name": "orders-mix",
  "description": "70% чтение, 25% создание, 5% без токена (ожидаем 401)",
  "baseUrl": "https://localhost:5001",
  "auth": { "type": "azureIdentity", "scope": "api://orders-api/.default" },
  "load": { "concurrency": 20, "durationSec": 120, "warmup": 50 },
  "requests": [
    { "name": "list", "method": "GET", "path": "/api/orders", "weight": 70,
      "query": { "page": "{{randomInt:1:10}}" },
      "expect": { "status": [200], "maxMs": 1000 } },
    { "name": "create", "method": "POST", "path": "/api/orders", "weight": 25,
      "body": { "productId": "{{randomInt:1:100}}", "qty": 1, "clientRef": "{{guid}}" },
      "expect": { "status": [201] } },
    { "name": "no-auth", "method": "GET", "path": "/api/orders", "weight": 5,
      "auth": false, "expect": { "status": [401] } }
  ],
  "thresholds": { "p95Ms": 500, "p99Ms": 1500, "errorRatePercent": 1 }
}
```

### Трафик для Application Insights

Ожидаемые 500-е указаны в `expect`, чтобы отчёт показывал только неожиданные ошибки.

```json
{
  "version": 1,
  "name": "appinsights-playground",
  "baseUrl": "http://localhost:5080",
  "load": { "concurrency": 15, "durationSec": 600 },
  "requests": [
    { "name": "fast", "method": "GET", "path": "/api/fast", "weight": 60, "expect": { "status": [200] } },
    { "name": "slow", "method": "GET", "path": "/api/slow", "weight": 20, "expect": { "status": [200] } },
    { "name": "dep",  "method": "GET", "path": "/api/dep",  "weight": 10, "expect": { "status": [200] } },
    { "name": "fail", "method": "GET", "path": "/api/fail", "weight": 10, "expect": { "status": [200, 500] } }
  ]
}
```

## Тестовые данные

- Уникальные ключи — через `{{guid}}` / `{{seq}}`, иначе конфликты (409) исказят результат.
- Помечайте создаваемые данные, чтобы потом удалить: `"clientRef": "loadtest-{{guid}}"`.
- Не нагружайте endpoint'ы с побочными эффектами (письма, платежи, SMS) без заглушек или тестового режима.
- `check` тоже отправляет настоящие запросы: один POST на каждый элемент `requests[]`.

## Частые ошибки валидации

| Сообщение | Что делать |
|---|---|
| `requests[i].body is required for POST` | добавить `body` или `"allowEmptyBody": true` |
| `env variable API_TOKEN is not set` | добавить переменную в `loadtests/.env` |
| `secret-like value found in auth.token` | заменить значение на `${env:...}` |
| `load: specify either totalRequests or durationSec` | оставить одно поле |
| `unknown template {{uuid}}` | использовать `{{guid}}` |
| `unknown field 'retries'` | поля нет в формате; убрать |
| `apiKey: specify exactly one of header or query` | оставить одно |
