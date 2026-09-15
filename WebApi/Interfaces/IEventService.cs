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
    EventResponse GetById(int id);

    /// <summary>
    /// Создаёт событие и возвращает его с присвоенным id.
    /// </summary>
    EventResponse Create(EventRequest request);

    /// <summary>
    /// Полностью обновляет событие по id. Бросает <see cref="Exceptions.NotFoundException"/>, если оно не найдено.
    /// </summary>
    EventResponse Update(int id, EventRequest request);

    /// <summary>
    /// Удаляет событие по id. Бросает <see cref="Exceptions.NotFoundException"/>, если оно не найдено.
    /// </summary>
    void Delete(int id);
}
