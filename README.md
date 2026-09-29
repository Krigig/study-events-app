# Study Events API

Учебный REST API для управления событиями на **ASP.NET Core Web API (.NET 9)**.

Данные хранятся в памяти приложения (`List<Event>`), бизнес-логика вынесена в сервис
`EventService`, подключённый через DI-контейнер.

## Запуск

Требуется [.NET SDK 9](https://dotnet.microsoft.com/download/dotnet/9.0) (подойдёт и .NET 8).

```bash
# из корня репозитория
dotnet run --project WebApi

# или из папки WebApi
cd WebApi
dotnet run
```

После запуска:

- Swagger UI: <http://localhost:5194/swagger>
- Базовый URL API: <http://localhost:5194/events>

> При запуске с профилем https адрес будет <https://localhost:7274>.

## Модель события

| Поле        | Тип       | Обязательное | Описание                          |
|-------------|-----------|--------------|-----------------------------------|
| `id`        | integer   | да           | Генерируется автоматически        |
| `title`     | string    | да           | Название события, 1–500 символов, не только пробелы |
| `description` | string | нет          | Описание события, до 4000 символов |
| `startAt`   | DateTime  | да           | Дата и время начала               |
| `endAt`     | DateTime  | да           | Дата и время окончания (позже `startAt`) |

## Эндпоинты

| Метод   | Маршрут       | Описание                          | Успех | Ошибка        |
|---------|---------------|-----------------------------------|-------|---------------|
| `GET`   | `/events`     | Получить список событий с фильтрацией и пагинацией | 200 | 400 (некорректная пагинация) |
| `GET`   | `/events/{id}`| Получить событие по id            | 200   | 404           |
| `POST`  | `/events`     | Создать событие                   | 201   | 400 (валидация) |
| `PUT`   | `/events/{id}`| Обновить событие целиком          | 200   | 404, 400      |
| `DELETE`| `/events/{id}`| Удалить событие                   | 204   | 404           |

### Фильтрация и пагинация GET /events

Метод `GET /events` принимает опциональные query-параметры:

| Параметр   | Тип      | По умолчанию | Описание                                       |
|------------|----------|--------------|------------------------------------------------|
| `title`    | string   | —            | Поиск по названию: частичное совпадение, без учёта регистра. Пустая строка игнорируется |
| `from`     | DateTime | —            | События, начинающиеся не раньше этой даты (`startAt >= from`) |
| `to`       | DateTime | —            | События, заканчивающиеся не позже этой даты (`endAt <= to`)   |
| `page`     | int      | 1            | Номер страницы (>= 1)                          |
| `pageSize` | int      | 10           | Элементов на странице (1–100)                  |

Все переданные фильтры комбинируются логическим И; пагинация применяется после фильтрации. `totalItems` и `totalPages` в ответе рассчитываются с учётом фильтров.

Ответ `200 OK` — `PaginatedResult<EventResponse>`:

```json
{
  "items": [ { "id": 1, "title": "Встреча команды", "description": "Еженедельный синк", "startAt": "2026-09-01T10:00:00", "endAt": "2026-09-01T11:00:00" } ],
  "page": 1,
  "pageSize": 10,
  "totalItems": 1,
  "totalPages": 1
}
```

Примеры запросов:

```bash
# Поиск по названию (регистронезависимо, частичное совпадение)
curl "http://localhost:5194/events?title=встреча"

# События в диапазоне дат, вторая страница по 5 элементов
curl "http://localhost:5194/events?from=2026-09-01T00:00:00&to=2026-09-30T23:59:59&page=2&pageSize=5"

# Комбинированный фильтр: название И диапазон дат
curl "http://localhost:5194/events?title=лекция&from=2026-09-01&to=2026-10-01"
```

Некорректные параметры пагинации (`page < 1` или `pageSize` вне 1–100) возвращают `400 Bad Request` с `ProblemDetails`.

### Пример создания события

```bash
curl -X POST http://localhost:5194/events \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Встреча команды",
    "description": "Еженедельный синк",
    "startAt": "2026-09-01T10:00:00",
    "endAt": "2026-09-01T11:00:00"
  }'
```

Ответ `201 Created` с заголовком `Location: http://localhost:5194/events/1`:

```json
{
  "id": 1,
  "title": "Встреча команды",
  "description": "Еженедельный синк",
  "startAt": "2026-09-01T10:00:00",
  "endAt": "2026-09-01T11:00:00"
}
```

### Пример обновления события

```bash
curl -X PUT http://localhost:5194/events/1 \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Встреча команды (обновлено)",
    "description": null,
    "startAt": "2026-09-01T10:00:00",
    "endAt": "2026-09-01T12:00:00"
  }'
```

### Валидация

Ошибки валидации возвращаются с кодом `400 Bad Request` и телом `ValidationProblemDetails`:

- `title`, `startAt`, `endAt` обязательны;
- `endAt` должен быть позже `startAt`.

Пример — пустой `title`:

```json
{ "title": "", "startAt": "2026-09-01T12:00:00", "endAt": "2026-09-01T13:00:00" }
```

Ответ:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Title": ["Title обязателен"]
  }
}
```

Пример — `endAt` раньше `startAt`:

```json
{ "title": "Лекция", "startAt": "2026-09-01T12:00:00", "endAt": "2026-09-01T11:00:00" }
```

Ответ:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "EndAt": ["EndAt должен быть позже StartAt"]
  }
}
```

## Обработка ошибок (глобальный middleware)

Все необработанные исключения перехватывает `GlobalExceptionHandlingMiddleware`,
логирует их через `ILogger` и возвращает единый JSON-формат `ProblemDetails`:

| Исключение          | Код ответа |
|---------------------|------------|
| `ValidationException` | 400 Bad Request |
| `NotFoundException`  | 404 Not Found    |
| любое другое          | 500 Internal Server Error |

Пример ответа от middleware (например, `GET /events/999` — сервис бросает `NotFoundException`):

```json
{
  "status": 404,
  "detail": "Событие с id = 999 не найдено."
}
```

## Тесты

Юнит-тесты на **xUnit** покрывают CRUD, фильтрацию (title/from/to), пагинацию,
комбинированные сценарии и валидацию `EventRequest`:

```bash
# из папки WebApi (тестовый проект включён в WebApi.sln)
cd WebApi
dotnet test
```

## Структура проекта

```
WebApi/
├── Controllers/EventController.cs  # Эндпоинты REST API (тонкие, без логики)
├── Services/EventService.cs        # Бизнес-логика и хранение данных в памяти
├── Interfaces/IEventService.cs     # Контракт сервиса
├── Middleware/GlobalExceptionHandlingMiddleware.cs  # Глобальная обработка ошибок
├── Exceptions/NotFoundException.cs # 404 при отсутствии события
└── Models/                         # Доменная модель и DTO
    ├── Event.cs                    # Модель события
    ├── EventRequest.cs             # DTO создания/обновления (с валидацией)
    ├── EventResponse.cs            # DTO ответа
    └── PaginatedResult.cs          # Обёртка ответа для пагинации

WebApi.Tests/                       # Юнит-тесты (xUnit)
```
