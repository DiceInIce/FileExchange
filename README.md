# FileExchange / FileShare

Desktop-клиент для Windows и backend-сервер: общение и обмен файлами только между **взаимными друзьями**. Реалтайм — **SignalR**; файлы — **через сервер** (до 100 МБ) и/или **P2P** (WebRTC Data Channel) с сигналингом через тот же хаб.

## Возможности

- **Учётные записи:** регистрация и вход, JWT + BCrypt на сервере.
- **Друзья:** поиск пользователей, заявка, принятие/отклонение, удаление из друзей; уведомления в реальном времени (`SocialDataChanged`, `FriendRequestReceived`).
- **Чат:** история по собеседнику, непрочитанные, пометка прочитанным; текст может уходить по P2P (с догоном в БД) или через хаб `SendMessage`.
- **Файлы:** режимы **AUTO** (сначала P2P, при неудаче — сервер, если файл ≤ 100 МБ), **P2P**, **SERVER**; drag-and-drop и выбор файла; скачивание серверных вложений по `transferId` из сообщения; входящие на сервере — список **inbox**.

## Технологии

| Компонент | Стек |
|-----------|------|
| **FileShareClient** | .NET 10 (`net10.0-windows`), WinForms + Blazor WebView, SignalR Client, JS (`wwwroot/js/peerTransfer.js`, `chatFileDrop.js`, `chatComposer.js`) |
| **FileShareServer** | .NET 10 (`net10.0`), Minimal API, SignalR, EF Core + SQLite, JWT Bearer |

Решение: `FileExchange.sln`.

## Быстрый старт

### Требования

- [.NET SDK 10](https://dotnet.microsoft.com/download) (в репозитории закреплена версия в [`global.json`](global.json), сейчас `10.0.203`).
- Windows 10/11 — для запуска клиента.

Пакеты Microsoft для сервера и клиента выровнены по линии **10.0.0**; JWT-библиотеки (`System.IdentityModel.Tokens.Jwt`, `Microsoft.IdentityModel.Tokens`) остаются на совместимой линии **8.8.x** (отдельный цикл версий NuGet, не равный номеру TFM).

### Сервер

```bash
cd FileShareServer
dotnet restore
dotnet run
```

URL приложения задаётся в `FileShareServer/Properties/launchSettings.json` (`applicationUrl`). Для локальной разработки обычно удобно указать, например, `https://localhost:7217;http://localhost:5217`.

В `appsettings.json` должен быть задан секрет JWT (`Jwt:Secret` и при необходимости `Jwt:Issuer` / `Jwt:Audience` — см. `AuthService`). База SQLite создаётся рядом с приложением: `fileshare.db`; загруженные файлы — папка `uploads/` в корне сервера.

### Клиент

```bash
cd FileShareClient
dotnet restore
dotnet run
```

**Важно:** базовый URL API и хаба задаётся в `FileShareClient/Services/ApiService.cs` (`ServerUrl`). Он должен совпадать с тем, на чём реально слушает сервер (схема `https`/`http`, хост, порт). Для разработки в коде отключена проверка TLS-сертификата сервера у `HttpClient` и SignalR — только для dev.

## REST API (кратко)

Префикс: `/api`. Защищённые маршруты требуют заголовок `Authorization: Bearer <token>`. Исключение: `/api/auth/register` и `/api/auth/login`.

| Область | Методы |
|---------|--------|
| **Auth** | `POST /auth/register`, `POST /auth/login` |
| **Users** | `GET /users`, `GET /users/{id}`, `GET /users/search/{query}` |
| **Friends** | `POST /friends/request/{friendId}`, `POST /friends/accept/{friendId}`, `POST /friends/reject/{friendId}`, `DELETE /friends/remove/{friendId}`, `GET /friends/list`, `GET /friends/pending`, `GET /friends/sent` |
| **Chat** | `GET /chat/conversation/{friendId}`, `GET /chat/unread`, `POST /chat/mark-read/{messageId}`, `POST /chat/store/{friendId}`, `POST /chat/store-file/{friendId}` |
| **Files** | `POST /files/upload/{receiverId}`, `GET /files/inbox`, `GET /files/download/{id}` |

Полная таблица и цепочки вызовов: **[ARCHITECTURE_APPLICATION_LIFECYCLE.md](ARCHITECTURE_APPLICATION_LIFECYCLE.md)**.

## SignalR

- **URL хаба:** `{ServerUrl}/chathub?access_token=<JWT>` (токен в query — так настроен приём JWT для WebSocket на сервере).
- Сообщения чата, онлайн/офлайн друзей, заявки в друзья, готовность серверного файла, сигналинг WebRTC (`SendOffer` / `SendAnswer` / `SendIceCandidate`, зеркальные события на клиенте).

## Документация

| Файл | Содержание |
|------|------------|
| [ARCHITECTURE_APPLICATION_LIFECYCLE.md](ARCHITECTURE_APPLICATION_LIFECYCLE.md) | Жизненный цикл: логин → друзья → сообщения → файлы; REST и хаб; Mermaid-диаграммы |
| [PROGRAM_OVERVIEW.md](PROGRAM_OVERVIEW.md) | Обзор программы и сценарии (может частично пересекаться с архитектурным документом) |
| [PERFORMANCE_IMPROVEMENTS.md](PERFORMANCE_IMPROVEMENTS.md) | Заметки по оптимизациям передачи данных |

## Структура репозитория

```
FileExchange.sln
global.json                 # закрепление SDK для единообразной сборки
FileShareClient/            # WinForms + Blazor UI, ApiService, ChatService
FileShareServer/            # API, ChatHub, EF, SQLite, uploads/
ARCHITECTURE_APPLICATION_LIFECYCLE.md
PROGRAM_OVERVIEW.md
README.md
```
