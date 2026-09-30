using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

/// <summary>
/// Сервис для работы с бронями.
/// </summary>
public class BookingService : IBookingService
{
    private readonly List<Booking> _bookings = [];

    public Task<Booking> CreateBookingAsync(Guid eventId)
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            BookingStatus = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.MinValue
        };

        _bookings.Add(booking);
        return Task.FromResult(booking);
    }

    public Task<Booking> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = _bookings.FirstOrDefault(b => b.Id == bookingId);
        if (booking is null)
        {
            throw new NotFoundException($"Бронь с id = {bookingId} не найдена.");
        }

        return Task.FromResult(booking);
    }
}
