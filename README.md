# LoadKit

> Статус: draft

Простой CLI-инструмент для нагрузочного тестирования HTTP API при локальной разработке.
Сценарий описывается в JSON-файле, запускается одной командой, результат — отчёт с перцентилями
(p50 / p95 / p99), ошибками по статус-кодам и примерами ответов.

```bash
loadtest run scenarios/orders.json
```

> Название `LoadKit` рабочее, можно заменить.

---

## Какую проблему решает

1. **Нагрузочные тесты пишутся кодом каждый раз заново.** Под каждую задачу создаётся свой проект,
   свой цикл запросов, свой подсчёт метрик и ручная сборка отчёта. Это долго и не переиспользуется.
2. **Тесты сложно передать коллеге.** Чтобы повторить прогон, нужно разбираться в чужом коде.
3. **Для мониторинга нужна реалистичная нагрузка.** Чтобы увидеть осмысленные данные в Application
   Insights (дашборды, воркбуки, перцентили, Application Map), нужен микс запросов, ошибки и авторизация,
   а не один endpoint в цикле.
4. **Промышленные инструменты избыточны для локальной задачи.** k6, Azure Load Testing и JMeter
   хороши для полноценных тестов, но для «быстро прогнать локально перед коммитом» они тяжеловаты.
   Подробное сравнение — в [ADR-001](docs/decisions/ADR-001-own-load-engine.md).

## Как возникла идея

В рамках задачи по настройке мониторинга Web App и Function App в Azure (Application Insights,
дашборды, воркбуки, алерты) понадобилось регулярно генерировать нагрузку и проверять, что метрики
и перцентили отображаются корректно. Писать для этого код каждый раз оказалось неудобно. Отсюда идея:
**один инструмент, в котором нагрузка объявляется сценарием, а не программируется.**

Дополнительная цель — сценарии должен уметь писать AI-ассистент. Для этого есть JSON Schema,
команда `validate` и навык [`loadtest`](ai/skills/loadtest/SKILL.md) со справочником [SCENARIO_REFERENCE.md](ai/skills/loadtest/SCENARIO_REFERENCE.md).

## Что умеет

- Сценарий в JSON с автодополнением в IDE (через JSON Schema).
- Метод, путь, заголовки, query, body; микс запросов с весами.
- Параллельность, общее число запросов или длительность, прогрев (warmup), таймаут.
- Авторизация: bearer-токен, API key / function key, Azure Identity (`az login`),
  OAuth2 client credentials, логин через свой endpoint.
- Секреты только из переменных окружения или `.env`, никогда в самом сценарии.
- Шаблоны для разнообразных данных: `{{guid}}`, `{{randomInt:1:100}}`, `{{seq}}`, `{{now}}`.
- Отчёт: RPS, p50/p95/p99, min/max, ошибки по статус-кодам, примеры тел ответов с ошибками.
- Экспорт отчёта в Markdown и JSON.
- Пороги (thresholds): если p95 или процент ошибок выше заданного, утилита возвращает ненулевой код
  выхода. Это позволяет использовать её в CI.
- Метка прогона `loadrun=<id>` в query, чтобы отфильтровать свой прогон в Application Insights.

## Быстрый старт

```bash
# 1. Установка (после публикации пакета)
dotnet tool install -g LoadKit

# 2. Создать сценарий (задаст 2–3 вопроса: адрес API и как он защищён)
loadtest init loadtests/scenarios/my-api.json

# 3. Заполнить секреты в loadtests/.env (файл уже в .gitignore)
#    API_TOKEN=eyJ...

# 4. Проверить сценарий без нагрузки
loadtest validate loadtests/scenarios/my-api.json

# 5. Один запрос на каждый endpoint: проверка доступности и авторизации
loadtest check loadtests/scenarios/my-api.json

# 6. Нагрузка
loadtest run loadtests/scenarios/my-api.json --out loadtests/reports/
```

Пример сценария:

```json
{
  "$schema": "../scenario.schema.json",
  "version": 1,
  "name": "orders-smoke",
  "baseUrl": "https://localhost:5001",
  "auth": { "type": "bearer", "token": "${env:API_TOKEN}" },
  "load": { "concurrency": 20, "totalRequests": 2000, "warmup": 50 },
  "requests": [
    { "name": "list", "method": "GET", "path": "/api/orders", "weight": 70,
      "expect": { "status": [200] } },
    { "name": "create", "method": "POST", "path": "/api/orders", "weight": 30,
      "body": { "productId": "{{randomInt:1:100}}", "qty": 1 },
      "expect": { "status": [201] } }
  ],
  "thresholds": { "p95Ms": 500, "errorRatePercent": 1 }
}
```

## Чем инструмент НЕ является

- Не замена k6 / Azure Load Testing для продакшн-нагрузки и больших объёмов.
- Не распределённый: нагрузка идёт с одной машины.
- Не для цепочек запросов с передачей данных между шагами (в v1).

## Работа через AI-ассистента

```bash
loadtest ai install
```

Команда ставит навык `loadtest` в проект (`.claude/skills/loadtest/`). После этого в Claude Code:

```
/loadtest нагрузи создание заказов, 30 параллельных, 2 минуты
```

или просто «проведи нагрузочный тест GET /api/orders». Ассистент сам найдёт маршруты, напишет
сценарий, проверит его, попросит добавить секреты в `.env`, запустит и перескажет отчёт.
Для других AI-инструментов: `loadtest ai install --dir <путь>` или `--agents-md`.

## Документация

| Для кого | Куда смотреть |
|---|---|
| Пользователь | [docs/user/GETTING_STARTED.md](docs/user/GETTING_STARTED.md), [docs/user/CLI_REFERENCE.md](docs/user/CLI_REFERENCE.md) |
| AI в вашем репозитории | [ai/skills/loadtest/](ai/skills/loadtest/SKILL.md) |
| Разработчик LoadKit | [AGENTS.md](AGENTS.md), [docs/README.md](docs/README.md) |
| Почему так | [docs/decisions/](docs/decisions/) |

## Коды выхода

| Код | Значение |
|---|---|
| 0 | Прогон успешен, пороги соблюдены |
| 1 | Прогон завершён, но пороги нарушены |
| 2 | Ошибка сценария (валидация) |
| 3 | Preflight не прошёл (недоступен сервер или не получен токен) |
| 4 | Нужно подтверждение для не-localhost URL (нет терминала и нет `--yes`) |
| 130 | Прервано `Ctrl+C` (отчёт по собранным данным всё равно создаётся) |
