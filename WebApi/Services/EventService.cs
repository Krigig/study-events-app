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
    // Коллекция событий в памяти приложения
    private readonly List<Event> _events = [];

    // Счётчик для генерации идентификаторов
    private int _nextId = 1;

    // Метод получения событий с опциональной фильтрацией и пагинацией.
    // Фильтры комбинируются (логическое И), применяются только если переданы.
    // Пагинация применяется после фильтрации.
    public PaginatedResult<EventResponse> GetAll(
        string? title, DateTime? from, DateTime? to, int page = 1, int pageSize = 10)
    {
        IEnumerable<Event> query = _events;

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
    public EventResponse GetById(int id)
    {
        var @event = _events.FirstOrDefault(e => e.Id == id);
        return @event is null
            ? throw new NotFoundException($"Событие с id = {id} не найдено.")
            : ToResponse(@event);
    }

    // Метод создания события
    public EventResponse Create(EventRequest request)
    {
        var @event = new Event
        {
            Id = _nextId++,
            Title = request.Title,
            Description = request.Description,
            // Не-null гарантированы валидацией [ApiController] до вызова сервиса
            StartAt = request.StartAt!.Value,
            EndAt = request.EndAt!.Value
        };

        _events.Add(@event);
        return ToResponse(@event);
    }

    // Метод полного обновления события по id; бросает NotFoundException, если события нет
    public EventResponse Update(int id, EventRequest request)
    {
        var @event = _events.FirstOrDefault(e => e.Id == id);
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
    public void Delete(int id)
    {
        var eventToDelete = _events.FirstOrDefault(e => e.Id == id);
        if (eventToDelete is null)
        {
            throw new NotFoundException($"Событие с id = {id} не найдено.");
        }

        _events.Remove(eventToDelete);
    }

    // Маппинг доменной модели в DTO ответа
    private static EventResponse ToResponse(Event @event) => new()
    {
        Id = @event.Id,
        Title = @event.Title,
        Description = @event.Description,
        StartAt = @event.StartAt,
        EndAt = @event.EndAt
    };
}
