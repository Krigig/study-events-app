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
    public async Task<ActionResult<PaginatedResult<EventResponse>>> GetAll(
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

        return Ok(await _eventService.GetAllAsync(title, from, to, page, pageSize));
    }

    /// <summary>
    /// Получить событие по id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetById(Guid id)
    {
        // 404 формируется middleware из NotFoundException, брошенного сервисом
        return Ok(await _eventService.GetByIdAsync(id));
    }

    /// <summary>
    /// Создать событие.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventResponse>> Create([FromBody] CreateEventRequest request)
    {
        var created = await _eventService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Обновить событие целиком.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Update(Guid id, [FromBody] EventRequest request)
    {
        // 404 формируется middleware из NotFoundException, брошенного сервисом
        return Ok(await _eventService.UpdateAsync(id, request));
    }

    /// <summary>
    /// Удалить событие.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        // 404 формируется middleware из NotFoundException, брошенного сервисом
        await _eventService.DeleteAsync(id);
        return NoContent();
    }
}
