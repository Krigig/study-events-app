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
    Task<PaginatedResult<EventResponse>> GetAllAsync(string? title, DateTime? from, DateTime? to, int page = 1, int pageSize = 10);

    /// <summary>
    /// Возвращает событие по id. Бросает <see cref="Exceptions.NotFoundException"/>, если оно не найдено.
    /// </summary>
    Task<EventResponse> GetByIdAsync(Guid id);

    /// <summary>Резервирует одно место в хранящемся событии. Бросает NotFoundException, если его нет.</summary>
    Task<bool> TryReserveSeatAsync(Guid id);

    /// <summary>Возвращает одно ранее зарезервированное место.</summary>
    Task ReleaseSeatAsync(Guid id);

    /// <summary>
    /// Создаёт событие и возвращает его с присвоенным id.
    /// </summary>
    Task<EventResponse> CreateAsync(CreateEventRequest request);

    /// <summary>
    /// Полностью обновляет событие по id. Бросает <see cref="Exceptions.NotFoundException"/>, если оно не найдено.
    /// </summary>
    Task<EventResponse> UpdateAsync(Guid id, EventRequest request);

    /// <summary>
    /// Удаляет событие по id. Бросает <see cref="Exceptions.NotFoundException"/>, если оно не найдено.
    /// </summary>
    Task DeleteAsync(Guid id);
}
