# Авторизация

> Статус: draft. Добавление типа — навык `adding-auth-provider`.
> Пользовательская инструкция — `docs/user/GETTING_STARTED.md`.

## Назначение

Тестируемому API ничего менять не нужно. LoadKit ведёт себя как обычный клиент и прикладывает те же
заголовки, что Postman или фронтенд. Задача модуля — получить токен до старта, держать его свежим
и подставлять без затрат в горячем пути.

## Файлы

`src/LoadKit.Core/Auth/`: `IAuthProvider`, `TokenAuthProviderBase`, `AuthHandler`,
`AuthProviderFactory`, `AuthOptionsParser`, `SecretMasker`, `Providers/*`.

## Контракты

```csharp
public interface IAuthProvider
{
    Task InitializeAsync(CancellationToken cancellationToken);                        // preflight
    ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken); // hot path
    void MarkStale();                                                                  // после 401
}

public abstract class TokenAuthProviderBase : IAuthProvider
{
    protected abstract Task<AccessTokenResult> AcquireTokenAsync(CancellationToken cancellationToken);
    // кэш, фоновое обновление на ~80% срока жизни, SemaphoreSlim, повтор при сбое обновления
}
```

Пайплайн: `HttpClient → AuthHandler (DelegatingHandler) → SocketsHttpHandler`.
Запросы с `"auth": false` помечаются через `HttpRequestMessage.Options`, и `AuthHandler` их пропускает.

## Типы

| `type` | Реализация | Срок жизни | Секрет |
|---|---|---|---|
| `bearer` | статический заголовок | не отслеживается | `${env:}` |
| `apiKey` | заголовок или query | не отслеживается | `${env:}` |
| `azureIdentity` | `DefaultAzureCredential.GetTokenAsync(scope)` | из токена | нет (`az login`) |
| `oauth2ClientCredentials` | POST на `tokenUrl` | `expires_in` | `${env:}` |
| `login` | запрос к своему endpoint, JSONPath к токену | `expiresInPath` или JWT `exp` | `${env:}` |

## Поток

1. `AuthProviderFactory` создаёт провайдер по `auth.type`.
2. `InitializeAsync` в preflight: первый токен. Ошибка → exit 3 с сообщением «что проверить»
   (`az login`, scope, переменные).
3. Во время прогона `ApplyAsync` берёт токен из поля (volatile read).
4. Фоновое обновление запускается таймером на ~80% срока жизни.
5. Ответ 401 → `MarkStale()` → внеочередное фоновое обновление (не чаще раза в 5 секунд).
   Запрос не повторяется: повторы искажают метрики.

## Безопасность

- `SecretMasker` знает заголовки `Authorization`, `x-functions-key`, `Cookie`, `api-key`, `x-api-key`
  и все значения, подставленные из `${env:}` в `auth.*`. Всё это маскируется в консоли, отчётах и логах.
- `check` показывает ответ сервера, но не отправленные секреты.

## Граничные случаи

- Bearer-токен истёк во время прогона — рост 401 в отчёте и подсказка «обновите API_TOKEN».
- JWT без `exp` и без `expiresInPath` — токен считается бессрочным, предупреждение в `check`.
- `azureIdentity` без `az login` — понятная ошибка с командой для исправления.
