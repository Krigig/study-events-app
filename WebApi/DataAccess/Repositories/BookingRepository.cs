using Microsoft.EntityFrameworkCore;
using WebApi.Models;

namespace WebApi.DataAccess.Repositories;

internal sealed class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _context;

    public BookingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tracked = _context.Bookings.Local.FirstOrDefault(b => b.Id == id);
        if (tracked is not null)
        {
            var entry = _context.Entry(tracked);
            await entry.ReloadAsync(cancellationToken);
            return entry.State == EntityState.Detached ? null : tracked;
        }

        return await _context.Bookings.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<Booking>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Bookings.AsNoTracking()
            .Where(b => b.BookingStatus == BookingStatus.Pending).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Bookings.AsNoTracking()
            .Where(b => b.BookingStatus == BookingStatus.Pending)
            .Select(b => b.Id).ToListAsync(cancellationToken);
    }

    public void Add(Booking booking) => _context.Bookings.Add(booking);

    public void Update(Booking booking)
    {
        var tracked = _context.Bookings.Local.FirstOrDefault(b => b.Id == booking.Id);
        if (tracked is not null)
        {
            // Confirm/Reject возвращают копию; обновляем уже отслеживаемый экземпляр.
            _context.Entry(tracked).CurrentValues.SetValues(booking);
        }
        else
        {
            _context.Entry(booking).State = EntityState.Modified;
        }
    }

    public void Remove(Booking booking) => _context.Bookings.Remove(booking);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Оба репозитория в scope используют один контекст: бронь и места атомарны.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Не оставляем неудавшиеся изменения для следующего сохранения в этом scope.
            _context.ChangeTracker.Clear();
            throw;
        }
    }
}
