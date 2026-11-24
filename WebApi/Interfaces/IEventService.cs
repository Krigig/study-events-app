using WebApi.Models;

namespace WebApi.Interfaces;

/// <summary>
/// Контракт сервиса для работы с событиями.
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Возвращает события с опциональной фильтрацией и пагинацией.
    /// </summary>
    PaginatedResult<EventResponse> GetAll(string? title, DateTime? from, DateTime? to, int page = 1, int pageSize = 10);

    /// <summary>
    /// Возвращает событие по id. Бросает <see cref="Exceptions.NotFoundException"/>, если оно не найдено.
    /// </summary>
    EventResponse GetById(Guid id);

    /// <summary>Резервирует одно место в хранящемся событии. Бросает NotFoundException, если его нет.</summary>
    bool TryReserveSeat(Guid id);

    /// <summary>Возвращает одно ранее зарезервированное место.</summary>
    void ReleaseSeat(Guid id);

    /// <summary>
    /// Создаёт событие и возвращает его с присвоенным id.
    /// </summary>
    EventResponse Create(CreateEventRequest request);

    /// <summary>
    /// Полностью обновляет событие по id. Бросает <see cref="Exceptions.NotFoundException"/>, если оно не найдено.
    /// </summary>
    EventResponse Update(Guid id, EventRequest request);

    /// <summary>
    /// Удаляет событие по id. Бросает <see cref="Exceptions.NotFoundException"/>, если оно не найдено.
    /// </summary>
    void Delete(Guid id);
}
