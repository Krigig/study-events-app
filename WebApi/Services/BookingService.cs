using Microsoft.EntityFrameworkCore;
using WebApi.DataAccess;
using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

/// <summary>Сервис для работы с бронями.</summary>
internal sealed class BookingService : IBookingService
{
    private readonly AppDbContext _context;

    public BookingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<BookingResponse> CreateBookingAsync(Guid eventId)
    {
        await DataAccessSynchronization.WriteSemaphore.WaitAsync();
        try
        {
            var tracked = _context.Events.Local.FirstOrDefault(e => e.Id == eventId);
            if (tracked is not null)
            {
                await _context.Entry(tracked).ReloadAsync();
            }

            var @event = await _context.Events.FindAsync(eventId)
                ?? throw new NotFoundException($"Событие с id = {eventId} не найдено.");
            if (!@event.TryReserveSeats())
            {
                throw new NoAvailableSeatsException();
            }

            var booking = Booking.Create(@event);
            try
            {
                _context.Bookings.Add(booking);
                // Бронь и изменение числа мест сохраняются одной транзакцией.
                await _context.SaveChangesAsync();
                return ToResponse(booking);
            }
            catch
            {
                _context.Entry(booking).State = EntityState.Detached;
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
        var booking = await _context.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new NotFoundException($"Бронь с id = {bookingId} не найдена.");
        return ToResponse(booking);
    }

    public async Task<IReadOnlyCollection<BookingResponse>> GetPendingBookingsAsync()
    {
        var bookings = await _context.Bookings.AsNoTracking()
            .Where(b => b.BookingStatus == BookingStatus.Pending).ToListAsync();
        return bookings.Select(ToResponse).ToArray();
    }

    public async Task ConfirmBookingAsync(Guid bookingId)
    {
        await DataAccessSynchronization.WriteSemaphore.WaitAsync();
        try
        {
            var tracked = _context.Bookings.Local.FirstOrDefault(b => b.Id == bookingId);
            if (tracked is not null)
            {
                await _context.Entry(tracked).ReloadAsync();
            }

            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new NotFoundException($"Бронь с id = {bookingId} не найдена.");
            if (booking.BookingStatus != BookingStatus.Pending)
            {
                return;
            }

            _context.Entry(booking).CurrentValues.SetValues(booking.Confirm());
            await _context.SaveChangesAsync();
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
