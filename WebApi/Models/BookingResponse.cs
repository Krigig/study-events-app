namespace WebApi.Models;

/// <summary>
/// DTO ответа с данными брони.
/// </summary>
public class BookingResponse
{
    /// <summary>
    /// Идентификатор брони.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Идентификатор события, к которому относится бронь.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Текущий статус брони.
    /// </summary>
    public BookingStatus Status { get; set; }

    /// <summary>
    /// Дата и время создания брони.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата и время обработки брони.
    /// </summary>
    public DateTime ProcessedAt { get; set; }
}
