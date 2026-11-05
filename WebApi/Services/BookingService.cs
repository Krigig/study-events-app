using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

/// <summary>
/// Сервис для работы с бронями.
/// Данные хранятся в памяти приложения, поэтому сервис
/// регистрируется в DI-контейнере как Singleton.
/// </summary>
public class BookingService : IBookingService
{
    private readonly IEventService _eventService;
    private readonly List<Booking> _bookings = [];

    public BookingService(IEventService eventService)
    {
        _eventService = eventService;
    }

    // Метод создания брони для события; бросает NotFoundException, если события нет
    public Task<BookingResponse> CreateBookingAsync(Guid eventId)
    {
        // Проверяем существование события: NotFoundException даст 404 через middleware
        _eventService.GetById(eventId);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            BookingStatus = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.MinValue
        };

        _bookings.Add(booking);
        return Task.FromResult(ToResponse(booking));
    }

    // Метод получения брони по id; бросает NotFoundException, если брони нет
    public Task<BookingResponse> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = _bookings.FirstOrDefault(b => b.Id == bookingId);
        if (booking is null)
        {
            throw new NotFoundException($"Бронь с id = {bookingId} не найдена.");
        }

        return Task.FromResult(ToResponse(booking));
    }

    // Маппинг доменной модели в DTO ответа
    private static BookingResponse ToResponse(Booking booking) => new()
    {
        Id = booking.Id,
        EventId = booking.EventId,
        Status = booking.BookingStatus,
        CreatedAt = booking.CreatedAt,
        ProcessedAt = booking.ProcessedAt
    };
}
