# Study Events API

Учебный REST API для управления событиями на **ASP.NET Core Web API (.NET 9)**.

События и брони хранятся в PostgreSQL. Схемой управляют миграции EF Core:
при запуске приложение вызывает `Database.Migrate()`. Бизнес-логика находится
в `EventService` и `BookingService`, доступ к данным — в `EventRepository`
и `BookingRepository`. Контекст, репозитории и сервисы зарегистрированы как scoped.

## Запуск

Требуются [.NET SDK 9](https://dotnet.microsoft.com/download/dotnet/9.0)
и Docker с Linux-контейнерами.

При первом запуске создайте локальный PostgreSQL (если он уже работает,
повторно создавать контейнер не нужно):

```bash
docker run -d --name eventapi-postgres --restart unless-stopped -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=eventapi -p 5432:5432 -v eventapi_pgdata:/var/lib/postgresql/data postgres:16-alpine
```

Строка `ConnectionStrings:DefaultConnection` в `WebApi/appsettings.json`:

```text
Host=localhost;Port=5432;Database=eventapi;Username=postgres;Password=postgres
```

Её можно переопределить переменной окружения `ConnectionStrings__DefaultConnection`.

```bash
# из корня репозитория
dotnet tool restore
dotnet build WebApi/WebApi.sln
dotnet run --project WebApi

# или из папки WebApi
cd WebApi
dotnet run
```

После запуска:

- Swagger UI: <http://localhost:5194/swagger>
- Базовый URL API: <http://localhost:5194>

> При запуске с профилем https адрес будет <https://localhost:7274>.

## Миграции EF Core

Начальная миграция `InitialCreate` создаёт таблицы `events`, `bookings`,
первичные ключи, индекс `bookings.event_id` и внешний ключ
`bookings.event_id → events.id` с `ON DELETE CASCADE`. Столбцы именуются в `snake_case`.
Миграции находятся в `WebApi/DataAccess/Migrations` и хранятся в Git.

Из корня репозитория:

```bash
# Восстановить локальный dotnet-ef той же версии, что EF Core
dotnet tool restore

# После изменения модели создать следующую миграцию (InitialCreate уже включена)
dotnet ef migrations add DescribeYourChange --project WebApi --startup-project WebApi --output-dir DataAccess/Migrations

# Применить миграции вручную, без запуска API
dotnet ef database update --project WebApi --startup-project WebApi

# Показать миграции
dotnet ef migrations list --project WebApi --startup-project WebApi
```

Обычный `dotnet run --project WebApi` также применяет миграции автоматически
до запуска фонового сервиса и обработки HTTP-запросов. При повторном запуске
уже применённые миграции не выполняются повторно.

База спринта 5, созданная через `EnsureCreated()`, не содержит истории миграций.
Для перехода на спринт 6 остановите API и удалите именно учебную базу `eventapi`
(все данные в ней будут потеряны):

```bash
docker exec eventapi-postgres dropdb -U postgres --if-exists --force eventapi
dotnet run --project WebApi
```

Приложение создаст базу и схему миграциями. `EnsureCreated()` не используется.
Даты в запросах передавайте в UTC, например `2026-12-01T10:00:00Z`.

## Модель события

| Поле        | Тип       | Обязательное | Описание                          |
|-------------|-----------|--------------|-----------------------------------|
| `id`        | Guid      | да           | Генерируется автоматически        |
| `title`     | string    | да           | Название события, 1–500 символов, не только пробелы |
| `description` | string | нет          | Описание события, до 4000 символов |
| `startAt`   | DateTime  | да           | Дата и время начала               |
| `endAt`     | DateTime  | да           | Дата и время окончания (позже `startAt`) |
| `totalSeats` | int | при создании | Общее количество мест, больше нуля |
| `availableSeats` | int | только ответ | Свободные места; при создании равно `totalSeats` |

## Модель брони

| Поле          | Тип             | Описание |
|---------------|-----------------|----------|
| `id`          | Guid            | Идентификатор брони, генерируется автоматически |
| `eventId`     | Guid            | Идентификатор события |
| `status`      | BookingStatus   | Текущий статус брони |
| `createdAt`   | DateTime (UTC)  | Время создания брони |
| `processedAt` | DateTime (UTC)  | Время обработки; до обработки содержит значение по умолчанию `0001-01-01T00:00:00` |

Возможные значения `BookingStatus`:

| Значение JSON | Описание |
|--------------|----------|
| `Pending`    | Бронь создана и ожидает обработки |
| `Confirmed`  | Бронь подтверждена |
| `Rejected`   | Бронь отклонена после удаления события или ошибки обработки |

В JSON статус сериализуется строкой.

## Эндпоинты

| Метод   | Маршрут       | Описание                          | Успех | Ошибка        |
|---------|---------------|-----------------------------------|-------|---------------|
| `GET`   | `/events`     | Получить список событий с фильтрацией и пагинацией | 200 | 400 (некорректная пагинация) |
| `GET`   | `/events/{id}`| Получить событие по id            | 200   | 404           |
| `POST`  | `/events`     | Создать событие                   | 201   | 400 (валидация) |
| `PUT`   | `/events/{id}`| Обновить событие целиком          | 200   | 404, 400      |
| `DELETE`| `/events/{id}`| Удалить событие                   | 204   | 404           |
| `POST`  | `/events/{id}/book` | Создать бронь для события    | 202   | 404, 409      |
| `GET`   | `/bookings/{id}` | Получить текущее состояние брони | 200 | 404           |

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
  "items": [ { "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff", "title": "Встреча команды", "description": "Еженедельный синк", "startAt": "2026-09-01T10:00:00Z", "endAt": "2026-09-01T11:00:00Z", "totalSeats": 30, "availableSeats": 30 } ],
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
curl "http://localhost:5194/events?from=2026-09-01T00:00:00Z&to=2026-09-30T23:59:59Z&page=2&pageSize=5"

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
    "startAt": "2026-09-01T10:00:00Z",
    "endAt": "2026-09-01T11:00:00Z",
    "totalSeats": 30
  }'
```

Ответ `201 Created` с заголовком
`Location: http://localhost:5194/events/6f9619ff-8b86-d011-b42d-00cf4fc964ff`:

```json
{
  "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
  "title": "Встреча команды",
  "description": "Еженедельный синк",
  "startAt": "2026-09-01T10:00:00Z",
  "endAt": "2026-09-01T11:00:00Z",
  "totalSeats": 30,
  "availableSeats": 30
}
```

## Бронирование и фоновая обработка

`POST /events/{id}/book` резервирует одно свободное место и создаёт бронь со
статусом `Pending`. Ответ имеет код `202 Accepted`, содержит созданную бронь и
заголовок `Location` со ссылкой на `GET /bookings/{bookingId}`.
Когда свободных мест нет, запрос возвращает `409 Conflict` с `ProblemDetails`
и сообщением `No available seats for this event`. Резервирование места и
создание брони выполняются в одной критической секции.

`BookingService` использует общий статический `SemaphoreSlim`: внутри него
сервис получает событие через репозиторий, резервирует место и добавляет бронь.
Оба репозитория одного scope используют один `AppDbContext`, поэтому один
`SaveChangesAsync()` атомарно сохраняет бронь и изменение числа мест.
При ошибке сохранения изменения откатываются PostgreSQL, а неудавшиеся
изменения удаляются из change tracker. Семафор синхронизирует запросы одного
процесса приложения; для нескольких экземпляров API нужна синхронизация в БД.
Список событий сортируется по дате начала, названию и id до пагинации.

Фоновый сервис запускает обработку пакета через `Task.WhenAll`. Двухсекундная
задержка каждой брони проходит параллельно. `IServiceScopeFactory` создаёт
scope для чтения Pending-id и отдельный scope с репозиториями для каждой задачи.
Обычное подтверждение выполняется параллельно; общий семафор используется
только при возврате места после ошибки.

Зарегистрированный через `AddHostedService` фоновый сервис:

1. получает снимок броней в статусе `Pending`;
2. запускает их обработку параллельно: каждая бронь ждёт две секунды, имитируя обращение к внешней системе;
3. проверяет существование события, переводит бронь в `Confirmed` и записывает текущее UTC-время в `ProcessedAt`;
4. при непредвиденной ошибке открывает новый scope, отклоняет ещё ожидающую бронь и возвращает место существующему событию одним сохранением.

Удаление события каскадно удаляет его брони в PostgreSQL. Если при обработке
обнаружена бронь без события, она отклоняется. Данные сохраняются между
перезапусками приложения и при пересоздании контейнера с тем же Docker-томом.

### Пример сценария бронирования

Сначала создайте событие описанным выше запросом и возьмите его `id`. Затем
создайте бронь:

```bash
curl -i -X POST http://localhost:5194/events/6f9619ff-8b86-d011-b42d-00cf4fc964ff/book
```

Пример ответа:

```http
HTTP/1.1 202 Accepted
Location: http://localhost:5194/bookings/a9d8c7b6-5e4f-4321-9876-123456789abc
Content-Type: application/json
```

```json
{
  "id": "a9d8c7b6-5e4f-4321-9876-123456789abc",
  "eventId": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
  "status": "Pending",
  "createdAt": "2026-11-05T12:00:00Z",
  "processedAt": "0001-01-01T00:00:00"
}
```

Получите текущее состояние брони:

```bash
curl http://localhost:5194/bookings/a9d8c7b6-5e4f-4321-9876-123456789abc
```

Сразу после создания ответ содержит `status: "Pending"`. Через несколько
секунд повторите запрос — фоновый сервис вернёт `status: "Confirmed"` и
заполненное поле `processedAt`:

```json
{
  "id": "a9d8c7b6-5e4f-4321-9876-123456789abc",
  "eventId": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
  "status": "Confirmed",
  "createdAt": "2026-11-05T12:00:00Z",
  "processedAt": "2026-11-05T12:00:02Z"
}
```

Если событие или бронь не найдены, соответствующий запрос возвращает
`404 Not Found` с `ProblemDetails`.

### Пример исчерпания мест

Создайте событие с `"totalSeats": 3` через `POST /events` и подставьте его `id`
в следующие запросы. Первые три бронирования вернут `202 Accepted`:

```bash
curl -i -X POST http://localhost:5194/events/<eventId>/book
curl -i -X POST http://localhost:5194/events/<eventId>/book
curl -i -X POST http://localhost:5194/events/<eventId>/book
```

После этого `GET /events/<eventId>` покажет `"availableSeats": 0`. Четвёртый
запрос `POST /events/<eventId>/book` вернёт `409 Conflict`:

```json
{
  "status": 409,
  "detail": "No available seats for this event"
}
```

Через несколько секунд `GET /bookings/<bookingId>` для каждой из первых трёх
броней вернёт `"status": "Confirmed"`.

### Пример обновления события

```bash
curl -X PUT http://localhost:5194/events/6f9619ff-8b86-d011-b42d-00cf4fc964ff \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Встреча команды (обновлено)",
    "description": null,
    "startAt": "2026-09-01T10:00:00Z",
    "endAt": "2026-09-01T12:00:00Z"
  }'
```

Обновление меняет название, описание и даты события. Количество мест и уже
созданные брони остаются прежними.

### Валидация

Ошибки валидации возвращаются с кодом `400 Bad Request` и телом `ValidationProblemDetails`:

- `title`, `startAt`, `endAt` обязательны; `totalSeats` обязателен только при создании;
- `endAt` должен быть позже `startAt`.
- `totalSeats` при создании должен быть больше нуля.

Пример — пустой `title`:

```json
{ "title": "", "startAt": "2026-09-01T12:00:00Z", "endAt": "2026-09-01T13:00:00Z", "totalSeats": 30 }
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
{ "title": "Лекция", "startAt": "2026-09-01T12:00:00Z", "endAt": "2026-09-01T11:00:00Z", "totalSeats": 30 }
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

Пример ответа от middleware (например,
`GET /events/6f9619ff-8b86-d011-b42d-00cf4fc96400` — сервис бросает
`NotFoundException`):

```json
{
  "status": 404,
  "detail": "Событие с id = 6f9619ff-8b86-d011-b42d-00cf4fc96400 не найдено."
}
```

## Тесты

Юнит-тесты на **xUnit** используют EF Core InMemory и покрывают сервисы,
валидацию, конкурентное бронирование, фоновую обработку и отмену.

`WebApi.IntegrationTests` проверяет все методы обоих репозиториев на реальном
PostgreSQL через `Testcontainers.PostgreSql`: CRUD, все сочетания фильтров,
пагинацию, строковый статус, ограничения, каскадное удаление, атомарность
сохранения и конкурентные запросы через разные scopes.

**Перед запуском интеграционных тестов должен работать Docker.** Testcontainers
автоматически создаёт один PostgreSQL-контейнер на всю коллекцию тестов и удаляет
его после завершения. Connection string берётся из контейнера, порт назначается
динамически. Тесты коллекции последовательны; перед каждым тестом выполняются
`EnsureDeletedAsync()` и `MigrateAsync()`. Локальная база `eventapi` не затрагивается.

```bash
# из корня репозитория
# Все тесты, включая интеграционные (Docker обязателен)
dotnet test WebApi/WebApi.sln

# Только юнит-тесты (Docker не нужен)
dotnet test WebApi.Tests/WebApi.Tests.csproj

# Только интеграционные тесты (Docker обязателен)
dotnet test WebApi.IntegrationTests/WebApi.IntegrationTests.csproj
```

## Структура проекта

```
WebApi/
├── Controllers/                    # Эндпоинты событий и броней
├── Services/
│   ├── EventService.cs             # Бизнес-логика событий
│   ├── BookingService.cs           # Логика создания и подтверждения броней
│   └── BookingProcessingBackgroundService.cs # Параллельная обработка броней
├── Interfaces/                     # Контракты сервисов
├── DataAccess/
│   ├── AppDbContext.cs             # Контекст EF Core
│   ├── Configurations/             # Fluent API
│   ├── Repositories/               # Интерфейсы и реализации репозиториев
│   └── Migrations/                 # InitialCreate и snapshot модели
├── Middleware/GlobalExceptionHandlingMiddleware.cs  # Глобальная обработка ошибок
├── Exceptions/NotFoundException.cs # 404 при отсутствии ресурса
└── Models/                         # Доменная модель и DTO
    ├── Event.cs, Booking.cs        # Доменные модели
    ├── EventRequest.cs             # DTO обновления события
    ├── CreateEventRequest.cs       # DTO создания с количеством мест
    ├── EventResponse.cs            # DTO ответа события
    ├── BookingResponse.cs          # DTO ответа брони
    └── PaginatedResult.cs          # Обёртка пагинации

WebApi.Tests/                       # Юнит-тесты (xUnit)
WebApi.IntegrationTests/            # Репозитории + PostgreSQL Testcontainers
dotnet-tools.json                   # Локальный dotnet-ef 9
```
