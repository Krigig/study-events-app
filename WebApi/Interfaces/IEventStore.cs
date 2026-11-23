using WebApi.Models;

namespace WebApi.Interfaces;

public interface IEventStore
{
    IReadOnlyCollection<Event> GetAll();
    Event? GetById(Guid id);
    bool TryAdd(Event @event);
    void Save(Event @event);
    bool TryRemove(Guid id);
}
