using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

/// <summary>
/// Сервис для работы с событиями.
/// Данные хранятся в памяти приложения, поэтому сервис
/// регистрируется в DI-контейнере как Singleton.
/// </summary>
public class EventService : IEventService
{
    private readonly IEventStore _eventStore;

    public EventService() : this(new InMemoryEventStore())
    {
    }

    public EventService(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    // Метод получения событий с опциональной фильтрацией и пагинацией.
    // Фильтры комбинируются (логическое И), применяются только если переданы.
    // Пагинация применяется после фильтрации.
    public PaginatedResult<EventResponse> GetAll(
        string? title, DateTime? from, DateTime? to, int page = 1, int pageSize = 10)
    {
        IEnumerable<Event> query = _eventStore.GetAll();

        // Частичное совпадение, регистронезависимо.
        // Пустая строка или пробелы игнорируются — как и отсутствие параметра.
        if (!string.IsNullOrWhiteSpace(title))
        {
            query = query.Where(e => e.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        }

        // События, начинающиеся не раньше указанной даты
        if (from.HasValue)
        {
            query = query.Where(e => e.StartAt >= from.Value);
        }

        // События, заканчивающиеся не позже указанной даты
        if (to.HasValue)
        {
            query = query.Where(e => e.EndAt <= to.Value);
        }

        // Общее количество берём до Skip/Take — с учётом фильтров
        var totalItems = query.Count();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToResponse)
            .ToList();

        return new PaginatedResult<EventResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    // Метод получения события по id; бросает NotFoundException, если события нет
    public EventResponse GetById(Guid id)
    {
        var @event = _eventStore.GetById(id);
        return @event is null
            ? throw new NotFoundException($"Событие с id = {id} не найдено.")
            : ToResponse(@event);
    }

    public bool TryReserveSeat(Guid id)
    {
        var @event = _eventStore.GetById(id)
            ?? throw new NotFoundException($"Событие с id = {id} не найдено.");
        // Event хранится по ссылке: TryReserveSeats обновляет объект в хранилище.
        return @event.TryReserveSeats();
    }

    public void ReleaseSeat(Guid id)
    {
        var @event = _eventStore.GetById(id)
            ?? throw new NotFoundException($"Событие с id = {id} не найдено.");
        @event.ReleaseSeats();
    }

    // Метод создания события
    public EventResponse Create(CreateEventRequest request)
    {
        // Не-null гарантированы валидацией [ApiController] до вызова сервиса.
        var @event = Event.Create(request.Title, request.Description,
            request.StartAt!.Value, request.EndAt!.Value, request.TotalSeats!.Value);

        if (!_eventStore.TryAdd(@event))
        {
            throw new InvalidOperationException($"Событие с id = {@event.Id} уже существует.");
        }
        return ToResponse(@event);
    }

    // Метод полного обновления события по id; бросает NotFoundException, если события нет
    public EventResponse Update(Guid id, EventRequest request)
    {
        var @event = _eventStore.GetById(id);
        if (@event is null)
        {
            throw new NotFoundException($"Событие с id = {id} не найдено.");
        }

        @event.Title = request.Title;
        @event.Description = request.Description;
        // Не-null гарантированы валидацией [ApiController] до вызова сервиса
        @event.StartAt = request.StartAt!.Value;
        @event.EndAt = request.EndAt!.Value;

        return ToResponse(@event);
    }

    // Метод удаления события по id; бросает NotFoundException, если события нет
    public void Delete(Guid id)
    {
        if (!_eventStore.TryRemove(id))
        {
            throw new NotFoundException($"Событие с id = {id} не найдено.");
        }
    }

    // Маппинг доменной модели в DTO ответа
    private static EventResponse ToResponse(Event @event) => new()
    {
        Id = @event.Id,
        Title = @event.Title,
        Description = @event.Description,
        StartAt = @event.StartAt,
        EndAt = @event.EndAt,
        TotalSeats = @event.TotalSeats,
        AvailableSeats = @event.AvailableSeats
    };
}
