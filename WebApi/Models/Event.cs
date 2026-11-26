using System.ComponentModel.DataAnnotations;

namespace WebApi.Models;

/// <summary>
/// Доменная модель события.
/// </summary>
public class Event
{
    private readonly object _seatsLock = new();

    private Event()
    {
    }

    /// <summary>
    /// Идентификатор события. Генерируется на сервере.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Название события.
    /// </summary>
    public string Title { get; set; } = null!;

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

    /// <summary>Общее количество мест.</summary>
    public int TotalSeats { get; private set; }

    /// <summary>Количество свободных мест.</summary>
    public int AvailableSeats { get; private set; }

    /// <summary>Брони события.</summary>
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    internal static Event Create(string title, string? description, DateTime startAt, DateTime endAt, int totalSeats)
    {
        if (totalSeats <= 0)
        {
            throw new ValidationException("TotalSeats должен быть больше нуля.");
        }

        return new Event
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            StartAt = startAt,
            EndAt = endAt,
            TotalSeats = totalSeats,
            AvailableSeats = totalSeats
        };
    }

    internal bool TryReserveSeats(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        lock (_seatsLock)
        {
            if (AvailableSeats < count)
            {
                return false;
            }

            AvailableSeats -= count;
            return true;
        }
    }

    internal void ReleaseSeats(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        lock (_seatsLock)
        {
            if (count > TotalSeats - AvailableSeats)
            {
                throw new InvalidOperationException("Нельзя освободить больше мест, чем было занято.");
            }

            AvailableSeats += count;
        }
    }
}
