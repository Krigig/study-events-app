using WebApi.DataAccess;
using WebApi.DataAccess.Repositories;
using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

/// <summary>Сервис для работы с событиями.</summary>
internal sealed class EventService : IEventService
{
    private readonly IEventRepository _events;

    public EventService(IEventRepository events)
    {
        _events = events;
    }

    public async Task<PaginatedResult<EventResponse>> GetAllAsync(
        string? title, DateTime? from, DateTime? to, int page = 1, int pageSize = 10)
    {
        var result = await _events.GetAllAsync(title, from, to, page, pageSize);
        return new PaginatedResult<EventResponse>
        {
            Items = result.Items.Select(ToResponse).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages
        };
    }

    public async Task<EventResponse> GetByIdAsync(Guid id)
    {
        var @event = await _events.GetByIdAsync(id)
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

            await _events.SaveChangesAsync();
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
            await _events.SaveChangesAsync();
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
        _events.Add(@event);
        await _events.SaveChangesAsync();
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
            _events.Update(@event);
            await _events.SaveChangesAsync();
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
            _events.Remove(@event);
            await _events.SaveChangesAsync();
        }
        finally
        {
            DataAccessSynchronization.WriteSemaphore.Release();
        }
    }

    private async Task<Event> FindForUpdateAsync(Guid id) =>
        await _events.GetByIdAsync(id)
            ?? throw new NotFoundException($"Событие с id = {id} не найдено.");

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
