using WebApi.Models;

namespace WebApi.Interfaces;

/// <summary>
/// Контракт сервиса для работы с бронями.
/// </summary>
public interface IBookingService
{
    /// <summary>
    /// Создаёт бронь для указанного события.
    /// </summary>
    Task<Booking> CreateBookingAsync(Guid eventId);

    /// <summary>
    /// Возвращает бронь по идентификатору. Бросает <see cref="Exceptions.NotFoundException"/>, если бронь не найдена.
    /// </summary>
    Task<Booking> GetBookingByIdAsync(Guid bookingId);
}
