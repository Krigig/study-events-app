using Microsoft.AspNetCore.Mvc;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Controllers;

/// <summary>
/// Эндпоинты для работы с бронями.
/// Контроллер тонкий: только вызовы сервиса и формирование HTTP-ответов.
/// </summary>
[ApiController]
[Produces("application/json")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    /// <summary>
    /// Забронировать место на событии.
    /// Бронь создаётся со статусом Pending и обрабатывается асинхронно.
    /// </summary>
    [HttpPost("events/{id:guid}/book")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Book(Guid id)
    {
        // 404 формируется middleware из NotFoundException, брошенного сервисом
        var booking = await _bookingService.CreateBookingAsync(id);
        return AcceptedAtAction(nameof(GetById), new { id = booking.Id }, booking);
    }

    /// <summary>
    /// Получить бронь по id.
    /// </summary>
    [HttpGet("bookings/{id:guid}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> GetById(Guid id)
    {
        // 404 формируется middleware из NotFoundException, брошенного сервисом
        return Ok(await _bookingService.GetBookingByIdAsync(id));
    }
}
