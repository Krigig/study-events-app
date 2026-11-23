using System.ComponentModel.DataAnnotations;

namespace WebApi.Models;

/// <summary>DTO создания события с обязательным количеством мест.</summary>
public class CreateEventRequest : EventRequest
{
    /// <summary>Общее количество мест (обязательно, больше нуля).</summary>
    [Required(ErrorMessage = "TotalSeats обязателен")]
    [Range(1, int.MaxValue, ErrorMessage = "TotalSeats должен быть больше нуля")]
    public int? TotalSeats { get; set; }
}
