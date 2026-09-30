namespace WebApi.Models;

/// <summary>
/// Доменная модель брони.
/// </summary>
public class Booking
{
    /// <summary>
    /// Идентификатор брони. Генерируется на сервере.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Идентификатор события, к которому относится бронь.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Текущий статус брони.
    /// </summary>
    public required BookingStatus BookingStatus { get; set; }

    /// <summary>
    ///  Дата и время создания брони.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата и время обработки.
    /// </summary>
    public DateTime ProcessedAt { get; set; }
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