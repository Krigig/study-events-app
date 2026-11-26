using WebApi.Models;

namespace WebApi.DataAccess.Repositories;

internal interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Booking>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default);
    void Add(Booking booking);
    void Update(Booking booking);
    void Remove(Booking booking);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
