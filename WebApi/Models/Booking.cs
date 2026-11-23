namespace WebApi.Models;

/// <summary>
/// Доменная модель брони.
/// </summary>
public class Booking
{
    /// <summary>
    /// Идентификатор брони. Генерируется на сервере.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Идентификатор события, к которому относится бронь.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Текущий статус брони.
    /// </summary>
    public required BookingStatus BookingStatus { get; init; }

    /// <summary>
    ///  Дата и время создания брони.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Дата и время обработки.
    /// </summary>
    public DateTime ProcessedAt { get; init; }

    internal Booking Confirm() => WithStatus(BookingStatus.Confirmed);

    internal Booking Reject() => WithStatus(BookingStatus.Rejected);

    private Booking WithStatus(BookingStatus status) => new()
    {
        Id = Id,
        EventId = EventId,
        BookingStatus = status,
        CreatedAt = CreatedAt,
        ProcessedAt = DateTime.UtcNow
    };
}

public enum BookingStatus
{
    /// <summary>
    /// Бронь создана, ожидает обработки.
    /// </summary>
    Pending,

    /// <summary>
    /// Бронь подтверждена.
    /// </summary>
    Confirmed,

    /// <summary>
    /// Бронь отклонена.
    /// </summary>
    Rejected
}
