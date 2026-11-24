using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

public sealed class InMemoryEventStore : IEventStore
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, Event> _events = new();
    private readonly List<Guid> _order = new();

    public IReadOnlyCollection<Event> GetAll()
    {
        lock (_sync)
        {
            return _order.Select(id => _events[id]).ToArray();
        }
    }

    public Event? GetById(Guid id)
    {
        lock (_sync)
        {
            return _events.TryGetValue(id, out var @event) ? @event : null;
        }
    }

    public bool TryAdd(Event @event)
    {
        lock (_sync)
        {
            if (!_events.TryAdd(@event.Id, @event))
            {
                return false;
            }

            _order.Add(@event.Id);
            return true;
        }
    }

    public void Save(Event @event)
    {
        lock (_sync)
        {
            if (!_events.ContainsKey(@event.Id))
            {
                throw new InvalidOperationException($"Событие с id = {@event.Id} отсутствует в хранилище.");
            }

            _events[@event.Id] = @event;
        }
    }

    public bool TryRemove(Guid id)
    {
        lock (_sync)
        {
            if (!_events.Remove(id))
            {
                return false;
            }

            _order.Remove(id);
            return true;
        }
    }
}
