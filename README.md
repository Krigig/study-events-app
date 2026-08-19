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
| `GET`   | `/events`     | Получить список всех событий      | 200   | —             |
| `GET`   | `/events/{id}`| Получить событие по id            | 200   | 404           |
| `POST`  | `/events`     | Создать событие                   | 201   | 400 (валидация) |
| `PUT`   | `/events/{id}`| Обновить событие целиком          | 200   | 404, 400      |
| `DELETE`| `/events/{id}`| Удалить событие                   | 204   | 404           |

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

## Структура проекта

```
WebApi/
├── Controllers/EventController.cs  # Эндпоинты REST API (тонкие, без логики)
├── Services/EventService.cs        # Бизнес-логика и хранение данных в памяти
├── Interfaces/IEventService.cs     # Контракт сервиса
└── Models/                         # Доменная модель и DTO
    ├── Event.cs                    # Модель события
    ├── EventRequest.cs             # DTO создания/обновления (с валидацией)
    └── EventResponse.cs            # DTO ответа
```
