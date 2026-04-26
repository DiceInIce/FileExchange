# FileShare

FileShare — оптимизированное desktop-приложение для общения и обмена файлами между друзьями (Windows, Blazor Hybrid + ASP.NET Core). Поддерживает P2P-передачу через WebRTC и серверный relay с минимальным потреблением памяти.

## Ключевые возможности

- **Авторизация**: Регистрация/логин с JWT + bcrypt
- **Система друзей**: Поиск, заявки, принятие, удаление (реалтайм)
- **Чат**: История сообщений с непрочитанными счетчиками
- **Передача файлов** (до 100 MB):
  - `AUTO`: P2P → fallback на SERVER
  - `P2P only`: Прямой канал через WebRTC DataChannel
  - `SERVER only`: Через сервер
  - Визуальная метка источника: `P2P` или `SERVER`
- **Оптимизированная передача**: Потоковые загрузки, бинарный формат (вместо Base64)

## Технологии

- **Client**: .NET 9, WinForms + BlazorWebView, SignalR, WebRTC DataChannel (JS interop)
- **Server**: ASP.NET Core 9.0, Minimal API, SignalR Hub, Entity Framework Core, SQLite
- **Auth**: JWT Bearer + bcrypt
- **Оптимизация**: Потоковые загрузки (File.OpenRead), binary transfers (ArrayBuffer), 64KB чанкирование

## Как запустить

### Требования

- .NET SDK (рекомендуется установленный у вас SDK, которым проект уже собирается).
- Windows 10/11 (для клиента).

### 1) Сервер

```bash
cd FileShareServer
dotnet restore
dotnet run
```

Сервер слушает:
- `https://localhost:7217`
- `http://localhost:5217`

### 2) Клиент

```bash
cd FileShareClient
dotnet restore
dotnet run
```

## Актуальные API (основные)

### Auth
- `POST /api/auth/register`
- `POST /api/auth/login`

### Users
- `GET /api/users`
- `GET /api/users/{id}`
- `GET /api/users/search/{query}`

### Friends
- `POST /api/friends/request/{friendId}`
- `POST /api/friends/accept/{friendId}`
- `POST /api/friends/reject/{friendId}`
- `DELETE /api/friends/remove/{friendId}`
- `GET /api/friends/list`
- `GET /api/friends/pending`

### Chat
- `GET /api/chat/conversation/{friendId}`
- `GET /api/chat/unread`
- `POST /api/chat/mark-read/{messageId}`
- `POST /api/chat/store/{friendId}` (сохранение текстового сообщения)
- `POST /api/chat/store-file/{friendId}` (сохранение файлового сообщения-метаданных)

### Files
- `POST /api/files/upload/{receiverId}` (server relay)
- `GET /api/files/download/{id}`

### SignalR
- Hub: `/chathub`
- Сигналинг WebRTC: offer/answer/ice.
Архитектура передачи файлов

### Режимы
- **AUTO**: P2P DataChannel → fallback на SERVER (если недоступен)
- **P2P only**: Прямой WebRTC DataChannel (без сервера)
- **SERVER only**: HTTP multipart upload/download через сервер

### Оптимизации (см. PERFORMANCE_IMPROVEMENTS.md)
- **P2P**: Бина

- **PROGRAM_OVERVIEW.md** — архитектура, потоки данных, сценарии
- **PERFORMANCE_IMPROVEMENTS.md** — детали всех оптимизаций, метрики памяти
- P2P (500 MB): 666 MB → ~500 MB памяти
- SERVER (500 MB): 500 MB → 64 KB буфера
- P2P сообщения: 31,250 → 7,812 (75% снижение)
- Сообщение в чате показывает источник (`P2P`/`SERVER`), чтобы пользователь понимал, как файл был доставлен.

## Документация для презентации

Подробный документ с архитектурой, потоками данных и сценариями:

- `PROGRAM_OVERVIEW.md`
