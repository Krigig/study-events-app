using WebApi.Models;

namespace WebApi.Interfaces;

public interface IBookingStore
{
    bool TryAdd(Booking booking);
    Booking? GetById(Guid id);
    IReadOnlyCollection<Booking> GetPending();
    bool TryUpdate(Booking current, Booking updated);
}
