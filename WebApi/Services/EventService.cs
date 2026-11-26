using Microsoft.EntityFrameworkCore;
using WebApi.DataAccess;
using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

/// <summary>Сервис для работы с событиями.</summary>
internal sealed class EventService : IEventService
{
    private readonly AppDbContext _context;

    public EventService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<EventResponse>> GetAllAsync(
        string? title, DateTime? from, DateTime? to, int page = 1, int pageSize = 10)
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

        var totalItems = await query.CountAsync();
        var events = await query.OrderBy(e => e.StartAt).ThenBy(e => e.Title).ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedResult<EventResponse>
        {
            Items = events.Select(ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<EventResponse> GetByIdAsync(Guid id)
    {
        var @event = await _context.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException($"Событие с id = {id} не найдено.");
        return ToResponse(@event);
    }

    public async Task<bool> TryReserveSeatAsync(Guid id)
    {
        await DataAccessSynchronization.WriteSemaphore.WaitAsync();
        try
        {
            var @event = await FindForUpdateAsync(id);
            if (!@event.TryReserveSeats())
            {
                return false;
            }

            await _context.SaveChangesAsync();
            return true;
        }
        finally
        {
            DataAccessSynchronization.WriteSemaphore.Release();
        }
    }

    public async Task ReleaseSeatAsync(Guid id)
    {
        await DataAccessSynchronization.WriteSemaphore.WaitAsync();
        try
        {
            var @event = await FindForUpdateAsync(id);
            @event.ReleaseSeats();
            await _context.SaveChangesAsync();
        }
        finally
        {
            DataAccessSynchronization.WriteSemaphore.Release();
        }
    }

    public async Task<EventResponse> CreateAsync(CreateEventRequest request)
    {
        var @event = Event.Create(request.Title, request.Description,
            request.StartAt!.Value, request.EndAt!.Value, request.TotalSeats!.Value);
        _context.Events.Add(@event);
        await _context.SaveChangesAsync();
        return ToResponse(@event);
    }

    public async Task<EventResponse> UpdateAsync(Guid id, EventRequest request)
    {
        await DataAccessSynchronization.WriteSemaphore.WaitAsync();
        try
        {
            var @event = await FindForUpdateAsync(id);
            @event.Title = request.Title;
            @event.Description = request.Description;
            @event.StartAt = request.StartAt!.Value;
            @event.EndAt = request.EndAt!.Value;
            await _context.SaveChangesAsync();
            return ToResponse(@event);
        }
        finally
        {
            DataAccessSynchronization.WriteSemaphore.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await DataAccessSynchronization.WriteSemaphore.WaitAsync();
        try
        {
            var @event = await FindForUpdateAsync(id);
            _context.Events.Remove(@event);
            await _context.SaveChangesAsync();
        }
        finally
        {
            DataAccessSynchronization.WriteSemaphore.Release();
        }
    }

    private async Task<Event> FindForUpdateAsync(Guid id)
    {
        var tracked = _context.Events.Local.FirstOrDefault(e => e.Id == id);
        if (tracked is not null)
        {
            await _context.Entry(tracked).ReloadAsync();
        }

        return await _context.Events.FindAsync(id)
            ?? throw new NotFoundException($"Событие с id = {id} не найдено.");
    }

    private static EventResponse ToResponse(Event @event) => new()
    {
        Id = @event.Id,
        Title = @event.Title,
        Description = @event.Description,
        StartAt = @event.StartAt,
        EndAt = @event.EndAt,
        TotalSeats = @event.TotalSeats,
        AvailableSeats = @event.AvailableSeats
    };
}
