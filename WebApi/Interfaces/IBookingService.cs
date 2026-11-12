using WebApi.Models;

namespace WebApi.Interfaces;

/// <summary>
/// Контракт сервиса для работы с бронями.
/// </summary>
public interface IBookingService
{
    /// <summary>
    /// Создаёт бронь для указанного события. Бросает <see cref="Exceptions.NotFoundException"/>, если событие не найдено.
    /// </summary>
    Task<BookingResponse> CreateBookingAsync(Guid eventId);

    /// <summary>
    /// Возвращает бронь по идентификатору. Бросает <see cref="Exceptions.NotFoundException"/>, если бронь не найдена.
    /// </summary>
    Task<BookingResponse> GetBookingByIdAsync(Guid bookingId);

    /// <summary>
    /// Возвращает снимок всех броней, ожидающих обработки.
    /// </summary>
    Task<IReadOnlyCollection<BookingResponse>> GetPendingBookingsAsync();

    /// <summary>
    /// Подтверждает бронь и фиксирует время её обработки.
    /// </summary>
    Task ConfirmBookingAsync(Guid bookingId);
}
