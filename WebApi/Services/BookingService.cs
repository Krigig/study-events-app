using WebApi.DataAccess;
using WebApi.DataAccess.Repositories;
using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

/// <summary>Сервис для работы с бронями.</summary>
internal sealed class BookingService : IBookingService
{
    private readonly IEventRepository _events;
    private readonly IBookingRepository _bookings;

    public BookingService(IEventRepository events, IBookingRepository bookings)
    {
        _events = events;
        _bookings = bookings;
    }

    public async Task<BookingResponse> CreateBookingAsync(Guid eventId)
    {
        await DataAccessSynchronization.WriteSemaphore.WaitAsync();
        try
        {
            var @event = await _events.GetByIdAsync(eventId)
                ?? throw new NotFoundException($"Событие с id = {eventId} не найдено.");
            if (!@event.TryReserveSeats())
            {
                throw new NoAvailableSeatsException();
            }

            var booking = Booking.Create(@event);
            try
            {
                _bookings.Add(booking);
                // Бронь и изменение числа мест сохраняются одной транзакцией.
                await _bookings.SaveChangesAsync();
                return ToResponse(booking);
            }
            catch
            {
                @event.Bookings.Remove(booking);
                @event.ReleaseSeats();
                throw;
            }
        }
        finally
        {
            DataAccessSynchronization.WriteSemaphore.Release();
        }
    }

    public async Task<BookingResponse> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = await _bookings.GetByIdAsync(bookingId)
            ?? throw new NotFoundException($"Бронь с id = {bookingId} не найдена.");
        return ToResponse(booking);
    }

    public async Task<IReadOnlyCollection<BookingResponse>> GetPendingBookingsAsync()
    {
        var bookings = await _bookings.GetPendingAsync();
        return bookings.Select(ToResponse).ToArray();
    }

    public async Task ConfirmBookingAsync(Guid bookingId)
    {
        await DataAccessSynchronization.WriteSemaphore.WaitAsync();
        try
        {
            var booking = await _bookings.GetByIdAsync(bookingId)
                ?? throw new NotFoundException($"Бронь с id = {bookingId} не найдена.");
            if (booking.BookingStatus != BookingStatus.Pending)
            {
                return;
            }

            _bookings.Update(booking.Confirm());
            await _bookings.SaveChangesAsync();
        }
        finally
        {
            DataAccessSynchronization.WriteSemaphore.Release();
        }
    }

    private static BookingResponse ToResponse(Booking booking) => new()
    {
        Id = booking.Id,
        EventId = booking.EventId,
        Status = booking.BookingStatus,
        CreatedAt = booking.CreatedAt,
        ProcessedAt = booking.ProcessedAt
    };
}
