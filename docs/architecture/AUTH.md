# Авторизация

> Статус: draft. Добавление типа — навык `adding-auth-provider`.
> Пользовательская инструкция — `docs/user/GETTING_STARTED.md`.

## Назначение

Тестируемому API ничего менять не нужно. LoadKit ведёт себя как обычный клиент (Postman, фронтенд)
и прикладывает к запросам те же заголовки. Задача модуля:
1. получить токен **до** старта нагрузки;
2. держать его свежим во время прогона;
3. подставлять его в каждый запрос без затрат времени, чтобы не искажать метрики.

## Файлы

`src/LoadKit.Core/Auth/`: `IAuthProvider`, `TokenAuthProviderBase`, `AuthHandler`,
`AuthProviderFactory`, `AuthOptionsParser`, `SecretMasker`, `Providers/*`.

## Контракты

```csharp
public interface IAuthProvider
{
    Task InitializeAsync(CancellationToken cancellationToken);                              // preflight
    ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken);  // hot path
    void MarkStale();                                                                        // после 401
}

public abstract class TokenAuthProviderBase : IAuthProvider
{
    protected abstract Task<AccessTokenResult> AcquireTokenAsync(CancellationToken cancellationToken);
    // кэш, фоновое обновление на ~80% срока жизни, SemaphoreSlim, повтор при сбое обновления
}

public readonly record struct AccessTokenResult(string Token, DateTimeOffset? ExpiresAt);
```

Пайплайн:

```
HttpClient → AuthHandler (DelegatingHandler) → SocketsHttpHandler → сеть
```

Запросы с `"auth": false` помечаются через `HttpRequestMessage.Options`, и `AuthHandler` их пропускает.

## Типы

| `type` | Как получает токен | Срок жизни | Секрет | Проверка локально |
|---|---|---|---|---|
| `bearer` | готовое значение из `${env:}` | не отслеживается | `${env:}` | `/secure` TargetApi |
| `apiKey` | готовое значение, в заголовок или query | не отслеживается | `${env:}` | `/secure` TargetApi |
| `login` | запрос к своему endpoint, токен по JSONPath | `expiresInPath` или JWT `exp` | `${env:}` | `/auth/login` TargetApi |
| `oauth2ClientCredentials` | POST на `tokenUrl` (client credentials) | `expires_in` | `${env:}` | `/oauth2/token` TargetApi |
| `azureIdentity` | `AzureCliCredential` (по умолчанию) или `DefaultAzureCredential` | из токена | нет | fake `TokenCredential` в тестах |

### Поля

- `bearer`: `token`; опционально `header` (по умолчанию `Authorization`) и `format`
  (по умолчанию `Bearer {token}`).
- `apiKey`: `value` и ровно одно из `header` / `query`. Рекомендуется `header`: значение в query
  попадает в URL, а URL пишется в логи и телеметрию сервера.
- `login`: `request` (как элемент `requests[]`, путь относительно `baseUrl`), `tokenPath`,
  опционально `expiresInPath`, `header`, `format`.
- `oauth2ClientCredentials`: `tokenUrl`, `clientId`, `clientSecret`, `scope`.
- `azureIdentity`: `scope`, опционально `source`: `azureCli` (по умолчанию) или `default`.

## Поток

```mermaid
sequenceDiagram
    participant CLI
    participant Provider as IAuthProvider
    participant IdP as Источник токена
    participant API
    CLI->>Provider: InitializeAsync (preflight)
    Provider->>IdP: получить токен
    IdP-->>Provider: токен + срок жизни
    Note over CLI: ошибка → exit 3 с подсказкой
    loop каждый запрос
        CLI->>Provider: ApplyAsync (только чтение кэша)
        CLI->>API: запрос с заголовком
    end
    Note over Provider: на ~80% срока жизни — фоновое обновление
    API-->>CLI: 401
    CLI->>Provider: MarkStale → внеочередное фоновое обновление
```

1. `AuthProviderFactory` создаёт провайдер по `auth.type`.
2. `InitializeAsync` в preflight получает первый токен. Ошибка → exit 3 с сообщением «что проверить».
3. Во время прогона `ApplyAsync` читает токен из поля (volatile read), без сетевых вызовов.
4. Фоновое обновление — таймер на ~80% срока жизни (через `TimeProvider`).
5. Ответ 401 → `MarkStale()` → внеочередное фоновое обновление, не чаще раза в 5 секунд.
   Сам запрос не повторяется: повторы искажают метрики.
6. `bearer` и `apiKey` не умеют обновляться: `MarkStale` для них только увеличивает счётчик,
   а отчёт подсказывает «токен истёк — обновите переменную».

## Безопасность

- `SecretMasker` маскирует заголовки `Authorization`, `x-functions-key`, `Cookie`, `api-key`,
  `x-api-key`, query-параметр из `apiKey.query`, а также все значения, подставленные из `${env:}`
  в `auth.*`. Маскирование действует в консоли, отчётах и логах.
- `check` показывает ответ сервера, но не отправленные секреты.
- Секрет, записанный прямо в JSON, — ошибка валидации `secret-literal` (проверяется до подстановки env).

## Граничные случаи и типовые ошибки

- **Bearer истёк во время прогона** — рост 401 и подсказка обновить переменную.
- **JWT без `exp` и без `expiresInPath`** — токен считается бессрочным, предупреждение в `check`.
- **`azureIdentity` без `az login`** — exit 3 с командой `az login`.
- **`azureIdentity`: ошибка согласия AADSTS65001** для приложения «Microsoft Azure CLI».
  API в Entra ID по умолчанию не разрешает Azure CLI получать для себя токены. Владелец app registration
  API должен добавить Azure CLI (client id `04b07795-8ddb-461a-bbee-02f9e1bf7b46`) в «Authorized client
  applications» в разделе Expose an API. Сообщение об ошибке в LoadKit содержит эту подсказку.
- **`oauth2ClientCredentials` получает токен, но API отвечает 401/403** — токен выдан приложению, а не
  пользователю (роли вместо scopes). API должен принимать app-only токены с нужной ролью.
- **`scope`** для своего API — `api://<application-id-uri>/.default`.

## Тестовые endpoint'ы TargetApi

- `/secure` принимает: статический dev-токен из конфигурации, API key в `x-api-key`, токены,
  выданные `/auth/login` и `/oauth2/token`. Иначе 401.
- `/auth/login` и `/oauth2/token` выдают токены с настраиваемым сроком жизни (по умолчанию 10 секунд),
  чтобы тесты обновления укладывались в секунды.
