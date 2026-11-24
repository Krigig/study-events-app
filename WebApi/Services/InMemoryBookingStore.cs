using System.Collections.Concurrent;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

public sealed class InMemoryBookingStore : IBookingStore
{
    private readonly ConcurrentDictionary<Guid, Booking> _bookings = new();

    public bool TryAdd(Booking booking) => _bookings.TryAdd(booking.Id, booking);

    public Booking? GetById(Guid id) => _bookings.TryGetValue(id, out var booking) ? booking : null;

    public IReadOnlyCollection<Booking> GetPending() => _bookings.Values
        .Where(booking => booking.BookingStatus == BookingStatus.Pending)
        .ToArray();

    public bool TryUpdate(Booking current, Booking updated) =>
        _bookings.TryUpdate(current.Id, updated, current);
}
