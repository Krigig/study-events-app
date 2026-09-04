using WebApi.Models;

namespace WebApi.Interfaces;

/// <summary>
/// Контракт сервиса для работы с событиями.
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Возвращает все события.
    /// </summary>
    List<EventResponse> GetAll(string? title, DateTime? from, DateTime? to);

    /// <summary>
    /// Возвращает событие по id или null, если оно не найдено.
    /// </summary>
    EventResponse? GetById(int id);

    /// <summary>
    /// Создаёт событие и возвращает его с присвоенным id.
    /// </summary>
    EventResponse Create(EventRequest request);

    /// <summary>
    /// Полностью обновляет событие по id. Возвращает null, если оно не найдено.
    /// </summary>
    EventResponse? Update(int id, EventRequest request);

    /// <summary>
    /// Удаляет событие по id. Возвращает false, если оно не найдено.
    /// </summary>
    bool Delete(int id);
}
