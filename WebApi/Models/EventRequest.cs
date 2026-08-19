using System.ComponentModel.DataAnnotations;

namespace WebApi.Models;

/// <summary>
/// DTO для создания и обновления события.
/// </summary>
public class EventRequest : IValidatableObject
{
    /// <summary>
    /// Название события (обязательно, не пустое, до 500 символов).
    /// </summary>
    [Required(ErrorMessage = "Title обязателен")]
    [StringLength(500, ErrorMessage = "Title не может быть длиннее 500 символов")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Описание события (опционально, до 4000 символов).
    /// </summary>
    [StringLength(4000, ErrorMessage = "Description не может быть длиннее 4000 символов")]
    public string? Description { get; set; }

    /// <summary>
    /// Дата и время начала события (обязательны).
    /// Nullable, чтобы [Required] реагировал на отсутствие поля в запросе.
    /// </summary>
    [Required(ErrorMessage = "StartAt обязателен")]
    public DateTime? StartAt { get; set; }

    /// <summary>
    /// Дата и время окончания события (обязательны, позже StartAt).
    /// </summary>
    [Required(ErrorMessage = "EndAt обязателен")]
    public DateTime? EndAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // [Required] пропускает строку из одних пробелов — ловим вручную
        if (string.IsNullOrWhiteSpace(Title))
        {
            yield return new ValidationResult(
                "Title не может состоять из одних пробелов",
                new[] { nameof(Title) });
        }

        // Атрибуты уже гарантируют наличие значений (Validate вызывается после них),
        // но на всякий случай проверяем явно
        if (StartAt.HasValue && EndAt.HasValue && EndAt.Value <= StartAt.Value)
        {
            yield return new ValidationResult(
                "EndAt должен быть позже StartAt",
                new[] { nameof(EndAt) });
        }
    }
}
