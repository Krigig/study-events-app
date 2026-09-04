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
    /// Получить постраничный список событий.
    /// Поддерживает опциональную фильтрацию: по названию (частичное совпадение,
    /// без учёта регистра), по дате начала (не раньше) и дате окончания (не позже).
    /// </summary>
    /// <param name="title">Поиск по названию (регистронезависимый, частичное совпадение).</param>
    /// <param name="from">События, начинающиеся не раньше этой даты.</param>
    /// <param name="to">События, заканчивающиеся не позже этой даты.</param>
    /// <param name="page">Номер страницы (по умолчанию 1).</param>
    /// <param name="pageSize">Количество элементов на странице (по умолчанию 10).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<PaginatedResult<EventResponse>> GetAll(
        [FromQuery] string? title,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = "Параметры пагинации некорректны: page должен быть >= 1, pageSize — от 1 до 100."
            });
        }

        return Ok(_eventService.GetAll(title, from, to, page, pageSize));
    }

    /// <summary>
    /// Получить событие по id.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<EventResponse> GetById(int id)
    {
        var @event = _eventService.GetById(id);
        return @event is null ? NotFound() : Ok(@event);
    }

    /// <summary>
    /// Создать событие.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<EventResponse> Create([FromBody] EventRequest request)
    {
        var created = _eventService.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Обновить событие целиком.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<EventResponse> Update(int id, [FromBody] EventRequest request)
    {
        var updated = _eventService.Update(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>
    /// Удалить событие.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Delete(int id)
    {
        return _eventService.Delete(id) ? NoContent() : NotFound();
    }
}
