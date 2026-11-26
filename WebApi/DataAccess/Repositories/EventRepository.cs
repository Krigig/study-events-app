using Microsoft.EntityFrameworkCore;
using WebApi.Models;

namespace WebApi.DataAccess.Repositories;

internal sealed class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;

    public EventRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tracked = _context.Events.Local.FirstOrDefault(e => e.Id == id);
        if (tracked is not null)
        {
            var entry = _context.Entry(tracked);
            await entry.ReloadAsync(cancellationToken);
            return entry.State == EntityState.Detached ? null : tracked;
        }

        return await _context.Events.FindAsync([id], cancellationToken);
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(string? title, DateTime? from, DateTime? to,
        int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = _context.Events.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(title))
        {
            var normalizedTitle = title.ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(normalizedTitle));
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.StartAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.EndAt <= to.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var events = await query.OrderBy(e => e.StartAt).ThenBy(e => e.Title).ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PaginatedResult<Event>
        {
            Items = events,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public void Add(Event @event) => _context.Events.Add(@event);

    public void Update(Event @event) => _context.Entry(@event).State = EntityState.Modified;

    public void Remove(Event @event) => _context.Events.Remove(@event);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
