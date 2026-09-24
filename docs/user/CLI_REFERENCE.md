# Справочник команд

> Статус: draft. Это публичный контракт: изменение флагов — в итоговом отчёте задачи, ломающее — ADR.

## `loadtest init <file>`

Создаёт сценарий, `.env`-шаблон и записи в `.gitignore`.

| Флаг | Описание |
|---|---|
| `--base-url <url>` | адрес API |
| `--auth <type>` | `none`, `bearer`, `apiKey`, `azureIdentity`, `oauth2ClientCredentials`, `login` |
| `--scope <scope>` | для `azureIdentity` / `oauth2ClientCredentials` |
| `--header <name>` | для `apiKey` (по умолчанию `x-functions-key`) |
| `--no-interactive` | не задавать вопросы (автоматически, если нет терминала) |

## `loadtest validate <file>`

Проверяет формат и переменные, ничего не отправляет. Код выхода `0` или `2`.

## `loadtest check <file>`

Preflight + по одному запросу на каждый элемент `requests[]`, показывает статус, время и начало тела ответа.
Код выхода `0`, `2` или `3`.

## `loadtest run <file>`

| Флаг | Описание |
|---|---|
| `--out <dir>` | папка для `report.md` и `report.json` |
| `--concurrency <n>` | переопределить `load.concurrency` |
| `--total <n>` | переопределить `load.totalRequests` |
| `--duration <sec>` | переопределить `load.durationSec` |
| `--env-file <path>` | путь к `.env` |
| `--no-tag` | не добавлять `loadrun` в query |
| `--yes` | не спрашивать подтверждение для не-localhost URL |

Коды выхода: `0` ok, `1` пороги нарушены, `2` сценарий невалиден, `3` preflight не прошёл.

## `loadtest ai install`

| Флаг | Куда |
|---|---|
| (нет) | `./.claude/skills/loadtest/` |
| `--global` | `~/.claude/skills/loadtest/` |
| `--dir <path>` | `<path>/loadtest/` |
| `--agents-md` | дополнительно блок в `./AGENTS.md` |

## `loadtest ai status`

Показывает, где установлен навык и совпадает ли его версия с версией инструмента.
