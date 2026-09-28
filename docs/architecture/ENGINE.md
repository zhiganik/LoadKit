# Движок нагрузки и метрики

> Статус: draft

## Файлы

`Engine/`: `LoadRunner`, `WeightedRequestPicker`, `RequestFactory`, `HttpPipelineFactory`.
`Metrics/`: `RequestResult`, `ResultCollector`, `PercentileCalculator`, `HistogramBuilder`, `ThresholdEvaluator`.

## Модель нагрузки

Закрытая модель: `concurrency` воркеров, каждый отправляет следующий запрос сразу после ответа.
Это «N запросов в полёте», а не «N запросов в секунду». Открытая модель — v2.

## Движок

- Один `HttpClient` на прогон. `SocketsHttpHandler`: `MaxConnectionsPerServer = concurrency`,
  `PooledConnectionLifetime = 2 мин`, автоматическая декомпрессия.
- Воркеры — `Task`'и. Номер следующего запроса — `Interlocked.Increment`; остановка при
  достижении `totalRequests` или по истечении `durationSec`.
- Первые `warmup` номеров отправляются, но в метрики не попадают.
- Выбор запроса — префиксные суммы весов + бинарный поиск.
- `Ctrl+C` → отмена; отчёт строится по собранным данным с пометкой «прервано».
- Прогресс — `IProgress<RunProgress>` раз в 250 мс (отправлено, ошибки, текущий RPS).

## Замер

- От `Stopwatch.GetTimestamp()` перед `SendAsync` до полного чтения тела.
- Не входит: сборка запроса, шаблоны, токен.
- Таймаут — `CancellationTokenSource` на запрос, классифицируется как `Timeout`.

## Сбор результатов

```csharp
readonly record struct RequestResult(
    int RequestIndex, int StatusCode, long ElapsedTicks, ErrorKind Error);
```

- `totalRequests` известен → массив выделяется заранее, запись по номеру без блокировок.
- `durationSec` → у каждого воркера свой `List<RequestResult>`, слияние в конце.
- Примеры тел ответов с ошибками: до 5 на статус-код, до 2 КБ каждое.

`ErrorKind`: `None`, `UnexpectedStatus`, `SlowResponse`, `Timeout`, `Connection`, `Tls`, `Other`.

## Перцентили

Nearest-rank: сортировка длительностей, P-й перцентиль — элемент с рангом `ceil(P/100 × N)`
(индекс `ранг − 1`). Для N = 2000: p50 — 1000-й, p95 — 1900-й, p99 — 1980-й.

Ранг считается **в целых числах**: `rank = (P × N + 99) / 100` для целого P. В `double`
`0.95 × 2000` может дать `1900.0000000000002`, и `ceil` вернёт 1901 — на один запрос дальше.

- Считается по каждому запросу сценария и по всем вместе.
- Перцентили не усредняются между группами; итог — по всем сырым данным.
- RPS = число учтённых запросов / длительность основного прогона.

## Пороги

`ThresholdEvaluator` сравнивает итоговые p50/p95/p99 и процент ошибок с `thresholds`.
Результат — список проверок (порог, факт, ok/fail) для отчёта и код выхода.

## Ограничения точности

Тестер и API на одной машине делят CPU — цифры годятся для сравнения «до/после», не как абсолют.
При загрузке CPU тестера > 85% отчёт содержит предупреждение.
