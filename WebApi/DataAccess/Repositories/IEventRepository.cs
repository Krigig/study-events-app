using WebApi.Models;

namespace WebApi.DataAccess.Repositories;

internal interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedResult<Event>> GetAllAsync(string? title, DateTime? from, DateTime? to,
        int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    void Add(Event @event);
    void Update(Event @event);
    void Remove(Event @event);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
