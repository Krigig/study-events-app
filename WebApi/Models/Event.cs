namespace WebApi.Models;

/// <summary>
/// Доменная модель события (хранится в памяти приложения).
/// </summary>
public class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
}
