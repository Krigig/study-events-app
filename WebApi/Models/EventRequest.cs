using System.ComponentModel.DataAnnotations;

namespace WebApi.Models;

/// <summary>
/// DTO для создания и обновления события.
/// </summary>
public class EventRequest : IValidatableObject
{
    [Required(ErrorMessage = "Title обязателен")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "StartAt обязателен")]
    public DateTime StartAt { get; set; }

    [Required(ErrorMessage = "EndAt обязателен")]
    public DateTime EndAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndAt <= StartAt)
        {
            yield return new ValidationResult(
                "EndAt должен быть позже StartAt",
                new[] { nameof(EndAt) });
        }
    }
}
