# Стиль C#

> Статус: ready

## Проект

- `net10.0`, `<LangVersion>latest</LangVersion>`, `<Nullable>enable</Nullable>`,
  `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- Общие настройки — в `Directory.Build.props`, версии пакетов — в `Directory.Packages.props`
  (Central Package Management).
- Форматирование — `.editorconfig` + `dotnet format`. Спорные случаи решает `dotnet format`, а не вкус.

## Код

- File-scoped namespaces. Один публичный тип — один файл, имя файла = имя типа.
- Классы `sealed` по умолчанию. Наследование — только осознанно (`TokenAuthProviderBase`).
- Модели данных — `record` / `readonly record struct`. Изменяемое состояние — только внутри движка.
- Нет статического изменяемого состояния. `Random.Shared` и `TimeProvider` — через параметры,
  чтобы тесты были детерминированными.
- Асинхронность до конца: никаких `.Result`, `.Wait()`, `async void`.
- `CancellationToken` — последний параметр любого async-метода Core, всегда передаётся дальше.
- `ConfigureAwait(false)` в `LoadKit.Core` не обязателен (консольное приложение без контекста синхронизации).
- Исключения: ошибки пользователя (невалидный сценарий, нет переменной) — не исключения, а результат
  `ValidationResult` с понятными сообщениями. Исключения — только для действительно исключительных ситуаций.

## Именование

- Явные имена: `requestDurationTicks`, а не `d`; `scenarioFilePath`, а не `path2`.
- Асинхронные методы — с суффиксом `Async`.
- Интерфейсы — `I*`, только если есть больше одной реализации или нужна подмена в тестах.
- Коды ошибок валидации — константы `ValidationCodes.*` в `kebab-case` (`body-required`).

## Горячий путь (цикл отправки запросов)

- Без блокировок, LINQ, аллокаций строк на парсинг, `string.Format`.
- Шаблоны разобраны заранее; запрос собирается из готовых частей.
- Замер — `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime()`.

## Вывод и логирование

- `LoadKit.Core` не пишет в консоль и не зависит от Spectre.Console.
- Прогресс — через `IProgress<RunProgress>`, события — через возвращаемые объекты.
- Секреты не логируются никогда; перед выводом значения проходят через `SecretMasker`.
