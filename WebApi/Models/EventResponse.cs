namespace WebApi.Models;

/// <summary>
/// DTO ответа с данными события.
/// </summary>
public class EventResponse
{
    /// <summary>
    /// Идентификатор события.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Название события.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Описание события (опционально).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Дата и время начала.
    /// </summary>
    public DateTime StartAt { get; set; }

    /// <summary>
    /// Дата и время окончания.
    /// </summary>
    public DateTime EndAt { get; set; }

    /// <summary>Общее количество мест.</summary>
    public int TotalSeats { get; set; }

    /// <summary>Количество свободных мест.</summary>
    public int AvailableSeats { get; set; }
}
