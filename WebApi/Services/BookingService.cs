using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

/// <summary>
/// Сервис для работы с бронями.
/// Хранилище подключается через DI.
/// </summary>
public class BookingService : IBookingService
{
    private readonly IEventStore _eventStore;
    private readonly IBookingStore _bookingStore;
    private readonly object _bookingLock = new();

    public BookingService(IEventStore eventStore, IBookingStore bookingStore)
    {
        _eventStore = eventStore;
        _bookingStore = bookingStore;
    }

    // Метод создания брони для события; бросает NotFoundException, если события нет
    public Task<BookingResponse> CreateBookingAsync(Guid eventId)
    {
        lock (_bookingLock)
        {
            var @event = _eventStore.GetById(eventId)
                ?? throw new NotFoundException($"Событие с id = {eventId} не найдено.");

            if (!@event.TryReserveSeats())
            {
                throw new NoAvailableSeatsException();
            }

            try
            {
                _eventStore.Save(@event);

                var booking = new Booking
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    BookingStatus = BookingStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.MinValue
                };

                if (!_bookingStore.TryAdd(booking))
                {
                    throw new InvalidOperationException($"Бронь с id = {booking.Id} уже существует.");
                }

                return Task.FromResult(ToResponse(booking));
            }
            catch
            {
                @event.ReleaseSeats();
                _eventStore.Save(@event);
                throw;
            }
        }
    }

    // Метод получения брони по id; бросает NotFoundException, если брони нет
    public Task<BookingResponse> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = _bookingStore.GetById(bookingId);
        if (booking is null)
        {
            throw new NotFoundException($"Бронь с id = {bookingId} не найдена.");
        }

        return Task.FromResult(ToResponse(booking));
    }

    public Task<IReadOnlyCollection<BookingResponse>> GetPendingBookingsAsync()
    {
        IReadOnlyCollection<BookingResponse> pendingBookings = _bookingStore.GetPending()
            .Select(ToResponse)
            .ToArray();

        return Task.FromResult(pendingBookings);
    }

    public Task ConfirmBookingAsync(Guid bookingId)
    {
        while (true)
        {
            var booking = _bookingStore.GetById(bookingId);
            if (booking is null)
            {
                throw new NotFoundException($"Бронь с id = {bookingId} не найдена.");
            }

            if (booking.BookingStatus != BookingStatus.Pending)
            {
                return Task.CompletedTask;
            }

            var confirmed = booking.Confirm();

            if (_bookingStore.TryUpdate(booking, confirmed))
            {
                return Task.CompletedTask;
            }
        }
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
