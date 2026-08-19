namespace WebApi.Models;

/// <summary>
/// Доменная модель события (хранится в памяти приложения).
/// </summary>
public class Event
{
    /// <summary>
    /// Идентификатор события. Генерируется на сервере.
    /// </summary>
    public int Id { get; set; }

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
    /// Дата и время окончания (позже StartAt).
    /// </summary>
    public DateTime EndAt { get; set; }
}
