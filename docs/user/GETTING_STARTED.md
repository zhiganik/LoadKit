# Первый запуск за 5 минут

> Статус: draft

## 1. Установка

```bash
dotnet tool install -g LoadKit
loadtest --version
```

## 2. Создать сценарий

```bash
loadtest init loadtests/scenarios/my-api.json
```

`init` задаст 2–3 вопроса: адрес API и как он защищён. Результат:

```
loadtests/
  scenarios/my-api.json   ← сценарий
  scenario.schema.json    ← автодополнение в IDE
  .env                    ← нужные переменные (пустые), в .gitignore
  reports/                ← в .gitignore
```

## 3. Авторизация: откуда взять токен

LoadKit ведёт себя как Postman: прикладывает к каждому запросу заголовок с токеном.
API менять не нужно. Нужно только ответить, **откуда взять токен**.

### Без авторизации

Ничего не делать.

### Я копирую токен вручную (Swagger, Postman, DevTools)

1. Скопируй токен так же, как для Postman.
2. Вставь в `loadtests/.env`: `API_TOKEN=eyJhbGciOi...`
3. В сценарии: `"auth": { "type": "bearer", "token": "${env:API_TOKEN}" }`

Токен истекает (обычно через час) — тогда вставь новый. Для прогонов на несколько минут это нормально.

### Azure Function с ключом

1. Портал → Function App → App keys → скопировать ключ.
2. `loadtests/.env`: `FUNC_KEY=...`
3. `"auth": { "type": "apiKey", "header": "x-functions-key", "value": "${env:FUNC_KEY}" }`

Ключ не истекает.

### API защищён Entra ID, я залогинен через `az login`

1. Один раз: `az login`.
2. `"auth": { "type": "azureIdentity", "scope": "api://my-api/.default" }`

`scope` — Application ID URI API из app registration + `/.default`. Токен LoadKit получает сам
через Azure CLI и сам обновляет. Секретов в файлах нет. Нужно, чтобы у твоей учётки был доступ к API.

Частая ошибка при первом запуске — **AADSTS65001** с упоминанием «Microsoft Azure CLI». Это значит,
что API не разрешает Azure CLI получать для себя токены. Владелец app registration API один раз добавляет
client id `04b07795-8ddb-461a-bbee-02f9e1bf7b46` в Expose an API → Authorized client applications.

### Остальное

Сервисный клиент (`oauth2ClientCredentials`) и свой endpoint логина (`login`) —
см. `ai/skills/loadtest/SCENARIO_REFERENCE.md`.

## 4. Проверка

```bash
loadtest validate loadtests/scenarios/my-api.json   # формат и переменные
loadtest check loadtests/scenarios/my-api.json      # по одному запросу на endpoint
```

`check` показывает ответы. `200` — всё настроено. `401` — токен не тот или истёк, узнаёшь это до нагрузки.
Запросы настоящие: если в сценарии есть POST, `check` создаст одну запись на каждый такой запрос.

## 5. Нагрузка

```bash
loadtest run loadtests/scenarios/my-api.json --out loadtests/reports/
```

Если `baseUrl` не localhost, LoadKit спросит подтверждение. В скриптах и CI добавь `--yes`.

## 6. Работа через AI-ассистента

```bash
loadtest ai install
```

После этого в Claude Code: `/loadtest нагрузи GET /api/orders, 20 параллельных, 1 минута`
или просто «проведи нагрузочный тест создания заказов». Ассистент сам напишет сценарий, проверит его,
попросит добавить секреты в `.env` и перескажет отчёт.

## 7. Найти прогон в Application Insights

В отчёте есть `loadrun` id и готовый запрос:

```kusto
requests
| where url contains "loadrun=<id>"
| summarize p50=percentile(duration,50), p95=percentile(duration,95), p99=percentile(duration,99)
```
