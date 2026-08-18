using Microsoft.AspNetCore.Mvc;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Controllers;

/// <summary>
/// Эндпоинты для работы с событиями.
/// Контроллер тонкий: только вызовы сервиса и формирование HTTP-ответов.
/// </summary>
[ApiController]
[Route("events")]
[Produces("application/json")]
public class EventController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventController(IEventService eventService)
    {
        _eventService = eventService;
    }

    /// <summary>
    /// Получить список всех событий.
    /// </summary>
    [HttpGet]
    public ActionResult<List<EventResponse>> GetAll()
    {
        return Ok(_eventService.GetAll());
    }

    /// <summary>
    /// Получить событие по id.
    /// </summary>
    [HttpGet("{id:int}")]
    public ActionResult<EventResponse> GetById(int id)
    {
        var @event = _eventService.GetById(id);
        return @event is null ? NotFound() : Ok(@event);
    }

    /// <summary>
    /// Создать событие.
    /// </summary>
    [HttpPost]
    public ActionResult<EventResponse> Create([FromBody] EventRequest request)
    {
        var created = _eventService.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Обновить событие целиком.
    /// </summary>
    [HttpPut("{id:int}")]
    public ActionResult<EventResponse> Update(int id, [FromBody] EventRequest request)
    {
        var updated = _eventService.Update(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>
    /// Удалить событие.
    /// </summary>
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        return _eventService.Delete(id) ? NoContent() : NotFound();
    }
}
