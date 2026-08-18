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

    // Метод получения всех событий
    public List<EventResponse> GetAll()
    {
        return _events.Select(ToResponse).ToList();
    }

    // Метод получения события по id
    public EventResponse? GetById(int id)
    {
        var @event = _events.FirstOrDefault(e => e.Id == id);
        return @event is null ? null : ToResponse(@event);
    }

    // Метод создания события
    public EventResponse Create(EventRequest request)
    {
        var @event = new Event
        {
            Id = _nextId++,
            Title = request.Title,
            Description = request.Description,
            StartAt = request.StartAt,
            EndAt = request.EndAt
        };

        _events.Add(@event);
        return ToResponse(@event);
    }

    // Метод полного обновления события по id
    public EventResponse? Update(int id, EventRequest request)
    {
        var @event = _events.FirstOrDefault(e => e.Id == id);
        if (@event is null)
        {
            return null;
        }

        @event.Title = request.Title;
        @event.Description = request.Description;
        @event.StartAt = request.StartAt;
        @event.EndAt = request.EndAt;

        return ToResponse(@event);
    }

    // Метод удаления события по id
    public bool Delete(int id)
    {
        var eventToDelete = _events.FirstOrDefault(e => e.Id == id);
        if (eventToDelete is null)
        {
            return false;
        }

        _events.Remove(eventToDelete);
        return true;
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
