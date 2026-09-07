using System.ComponentModel.DataAnnotations;
using WebApi.Models;

namespace WebApi.Tests;

/// <summary>
/// Тесты валидации DTO EventRequest (IValidatableObject).
/// В проекте валидация выполняется в DTO (запускается [ApiController] до сервиса),
/// поэтому некорректные данные проверяются напрямую через Validate().
/// </summary>
public class EventRequestTests
{
    // Вспомогательный запуск валидации как это делает [ApiController]
    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(request);
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    // Тест проверяет, что корректный запрос проходит валидацию без ошибок
    [Fact]
    public void Validate_WithCorrectRequest_ShouldHaveNoErrors()
    {
        //Arrange
        var request = new EventRequest
        {
            Title = "Lecture .NET",
            StartAt = new DateTime(2026, 10, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 10, 1, 12, 0, 0)
        };

        //Act
        var results = Validate(request);

        //Assert
        Assert.Empty(results);
    }

    // Тест проверяет, что Title из одних пробелов не проходит валидацию
    [Fact]
    public void Validate_WithWhitespaceTitle_ShouldFail()
    {
        //Arrange
        var request = new EventRequest
        {
            Title = "   ",
            StartAt = new DateTime(2026, 10, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 10, 1, 12, 0, 0)
        };

        //Act
        var results = Validate(request);

        //Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Title"));
    }

    // Тест проверяет, что отсутствие Title не проходит валидацию
    [Fact]
    public void Validate_WithMissingTitle_ShouldFail()
    {
        //Arrange
        var request = new EventRequest
        {
            Title = string.Empty,
            StartAt = new DateTime(2026, 10, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 10, 1, 12, 0, 0)
        };

        //Act
        var results = Validate(request);

        //Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(EventRequest.Title)));
    }

    // Тест проверяет, что EndAt раньше StartAt не проходит валидацию
    [Fact]
    public void Validate_WithEndAtBeforeStartAt_ShouldFail()
    {
        //Arrange
        var request = new EventRequest
        {
            Title = "Broken event",
            StartAt = new DateTime(2026, 10, 2, 10, 0, 0),
            EndAt = new DateTime(2026, 10, 1, 12, 0, 0) // окончание раньше начала
        };

        //Act
        var results = Validate(request);

        //Assert
        Assert.Contains(results,
            r => r.MemberNames.Contains(nameof(EventRequest.EndAt)) &&
                 r.ErrorMessage!.Contains("позже StartAt"));
    }

    // Тест проверяет, что EndAt равный StartAt тоже не проходит валидацию (строго позже)
    [Fact]
    public void Validate_WithEndAtEqualToStartAt_ShouldFail()
    {
        //Arrange
        var sameMoment = new DateTime(2026, 10, 1, 10, 0, 0);
        var request = new EventRequest { Title = "Zero-length event", StartAt = sameMoment, EndAt = sameMoment };

        //Act
        var results = Validate(request);

        //Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(EventRequest.EndAt)));
    }

    // Тест проверяет, что отсутствие дат не проходит валидацию
    [Theory]
    [InlineData(null, "2026-10-01 12:00:00")] // нет StartAt
    [InlineData("2026-10-01 10:00:00", null)] // нет EndAt
    public void Validate_WithMissingDates_ShouldFail(string? startAt, string? endAt)
    {
        //Arrange
        var request = new EventRequest
        {
            Title = "Event",
            StartAt = startAt is null ? null : DateTime.Parse(startAt),
            EndAt = endAt is null ? null : DateTime.Parse(endAt)
        };

        //Act
        var results = Validate(request);

        //Assert
        Assert.NotEmpty(results);
    }
}
