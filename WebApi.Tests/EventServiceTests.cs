using Microsoft.Extensions.DependencyInjection;
using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Tests;

/// <summary>
/// Юнит-тесты сервиса EventService.
/// Каждый тест использует отдельную базу EF Core InMemory.
/// </summary>
public class EventServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    public void Dispose() => _database.Dispose();

    // Заполнитель события для компактного Arrange
    private static CreateEventRequest CreateRequest(
        string title = "Test Event",
        DateTime? startAt = null,
        DateTime? endAt = null)
        => new()
        {
            Title = title,
            Description = "description",
            TotalSeats = 10,
            StartAt = startAt ?? new DateTime(2026, 10, 1, 10, 0, 0),
            EndAt = endAt ?? new DateTime(2026, 10, 1, 12, 0, 0)
        };

    // Тест проверяет, что создание события присваивает id и сохраняет все поля
    [Fact]
    public async Task Create_ShouldAssignIdAndFields()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        var request = CreateRequest("Lecture .NET", startAt: new DateTime(2026, 10, 1, 9, 0, 0),
            endAt: new DateTime(2026, 10, 1, 11, 0, 0));

        //Act
        var result = await service.CreateAsync(request);

        //Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Lecture .NET", result.Title);
        Assert.Equal("description", result.Description);
        Assert.Equal(new DateTime(2026, 10, 1, 9, 0, 0), result.StartAt);
        Assert.Equal(new DateTime(2026, 10, 1, 11, 0, 0), result.EndAt);
        Assert.Equal(10, result.TotalSeats);
        Assert.Equal(10, result.AvailableSeats);
    }

    // Тест проверяет, что GetAll возвращает все созданные события
    [Fact]
    public async Task GetAll_ShouldReturnAllEvents()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        await service.CreateAsync(CreateRequest("Event 1"));
        await service.CreateAsync(CreateRequest("Event 2"));
        await service.CreateAsync(CreateRequest("Event 3"));

        //Act
        var result = await service.GetAllAsync(null, null, null);

        //Assert
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, item => Assert.False(string.IsNullOrEmpty(item.Title)));
    }

    // Тест проверяет, что GetById возвращает событие по существующему id
    [Fact]
    public async Task GetById_ShouldReturnEvent()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        var created = await service.CreateAsync(CreateRequest("Event 1"));

        //Act
        using var scope = _database.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IEventService>();
        var result = await reader.GetByIdAsync(created.Id);

        //Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Event 1", result.Title);
    }

    // Тест проверяет, что обновление изменяет поля существующего события
    [Fact]
    public async Task Update_ShouldUpdateExistingEvent()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        var created = await service.CreateAsync(CreateRequest("Old Title"));
        var updatedRequest = new EventRequest
        {
            Title = "New Title",
            Description = "description",
            StartAt = new DateTime(2026, 11, 1, 9, 0, 0),
            EndAt = new DateTime(2026, 11, 1, 18, 0, 0)
        };

        //Act
        var result = await service.UpdateAsync(created.Id, updatedRequest);

        //Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("New Title", result.Title);
        Assert.Equal(new DateTime(2026, 11, 1, 9, 0, 0), result.StartAt);
        Assert.Equal(new DateTime(2026, 11, 1, 18, 0, 0), result.EndAt);
        Assert.Equal(10, result.TotalSeats);
        Assert.Equal(10, result.AvailableSeats);

        // Изменение видно и через GetById
        var fromGet = await service.GetByIdAsync(created.Id);
        Assert.Equal("New Title", fromGet!.Title);
    }

    // Тест проверяет, что удаление убирает событие из хранилища
    [Fact]
    public async Task Delete_ShouldRemoveEvent()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        var created = await service.CreateAsync(CreateRequest("Event 1"));

        //Act
        await service.DeleteAsync(created.Id);

        //Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(created.Id));
    }

    // Тест проверяет, что фильтрация по названию — частичное совпадение без учёта регистра
    [Fact]
    public async Task GetAll_FilterByTitle_ShouldBeCaseInsensitivePartial()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        await service.CreateAsync(CreateRequest("Lecture .NET Basics"));
        await service.CreateAsync(CreateRequest("Workshop ADVANCED C#"));
        await service.CreateAsync(CreateRequest("Meetup dotnet community"));
        var expectedTitles = new List<string> { "Lecture .NET Basics", "Meetup dotnet community" };

        //Act
        var result = await service.GetAllAsync("NET", null, null);

        //Assert
        Assert.Equal(expectedTitles, result.Items.Select(e => e.Title));
        Assert.DoesNotContain(result.Items, e => e.Title == "Workshop ADVANCED C#");
    }

    // Тест проверяет, что фильтр from включает события, начинающиеся ровно с указанной даты (не раньше)
    [Fact]
    public async Task GetAll_FilterByFrom_ShouldIncludeBoundary()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        await service.CreateAsync(CreateRequest("Early", startAt: new DateTime(2026, 9, 1, 10, 0, 0),
            endAt: new DateTime(2026, 9, 1, 12, 0, 0)));
        await service.CreateAsync(CreateRequest("Boundary", startAt: new DateTime(2026, 10, 1, 10, 0, 0),
            endAt: new DateTime(2026, 10, 1, 12, 0, 0)));
        await service.CreateAsync(CreateRequest("Late", startAt: new DateTime(2026, 11, 1, 10, 0, 0),
            endAt: new DateTime(2026, 11, 1, 12, 0, 0)));
        var from = new DateTime(2026, 10, 1, 10, 0, 0);

        //Act
        var result = await service.GetAllAsync(null, from, null);

        //Assert
        Assert.Equal(new List<string> { "Boundary", "Late" }, result.Items.Select(e => e.Title));
    }

    // Тест проверяет, что фильтр to включает события, заканчивающиеся ровно в указанную дату (не позже)
    [Fact]
    public async Task GetAll_FilterByTo_ShouldIncludeBoundary()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        await service.CreateAsync(CreateRequest("Early", startAt: new DateTime(2026, 9, 1, 10, 0, 0),
            endAt: new DateTime(2026, 9, 1, 12, 0, 0)));
        await service.CreateAsync(CreateRequest("Boundary", startAt: new DateTime(2026, 10, 1, 10, 0, 0),
            endAt: new DateTime(2026, 10, 1, 12, 0, 0)));
        await service.CreateAsync(CreateRequest("Late", startAt: new DateTime(2026, 11, 1, 10, 0, 0),
            endAt: new DateTime(2026, 11, 1, 12, 0, 0)));
        var to = new DateTime(2026, 10, 1, 12, 0, 0);

        //Act
        var result = await service.GetAllAsync(null, null, to);

        //Assert
        Assert.Equal(new List<string> { "Early", "Boundary" }, result.Items.Select(e => e.Title));
    }

    // Тест проверяет, что пагинация возвращает нужную страницу, а totalItems считается до Skip/Take
    [Fact]
    public async Task GetAll_Pagination_ShouldSkipAndTake()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        for (var i = 1; i <= 7; i++)
        {
            await service.CreateAsync(CreateRequest($"Event {i}"));
        }

        //Act
        var result = await service.GetAllAsync(null, null, null, page: 2, pageSize: 3);

        //Assert
        Assert.Equal(7, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Page);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(new List<string> { "Event 4", "Event 5", "Event 6" },
            result.Items.Select(e => e.Title));
    }

    // Тест проверяет, что последняя неполная страница содержит остаток элементов
    [Fact]
    public async Task GetAll_Pagination_LastPage_ShouldContainRemainder()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        for (var i = 1; i <= 7; i++)
        {
            await service.CreateAsync(CreateRequest($"Event {i}"));
        }

        //Act
        var result = await service.GetAllAsync(null, null, null, page: 3, pageSize: 3);

        //Assert
        Assert.Single(result.Items);
        Assert.Equal("Event 7", result.Items[0].Title);
    }

    // Тест проверяет, что все фильтры работают совместно (логическое И) вместе с пагинацией
    [Fact]
    public async Task GetAll_CombinedFilters_ShouldApplyAllTogether()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        await service.CreateAsync(CreateRequest("dotnet lecture", startAt: new DateTime(2026, 10, 1, 10, 0, 0),
            endAt: new DateTime(2026, 10, 1, 12, 0, 0)));
        await service.CreateAsync(CreateRequest("dotnet workshop", startAt: new DateTime(2026, 11, 5, 9, 0, 0),
            endAt: new DateTime(2026, 11, 5, 17, 0, 0)));
        await service.CreateAsync(CreateRequest("dotnet meetup", startAt: new DateTime(2026, 12, 10, 18, 0, 0),
            endAt: new DateTime(2026, 12, 10, 21, 0, 0)));
        await service.CreateAsync(CreateRequest("java conference", startAt: new DateTime(2026, 11, 6, 9, 0, 0),
            endAt: new DateTime(2026, 11, 6, 17, 0, 0)));

        //Act
        var result = await service.GetAllAsync(
            "dotnet",
            from: new DateTime(2026, 11, 1),
            to: new DateTime(2026, 12, 1),
            page: 1,
            pageSize: 10);

        //Assert
        Assert.Equal(1, result.TotalItems);
        Assert.Single(result.Items);
        Assert.Equal("dotnet workshop", result.Items[0].Title);
    }

    // Тест проверяет, что пустой или пробельный title игнорируется — как и отсутствие параметра
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAll_EmptyTitle_ShouldBeIgnored(string title)
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        await service.CreateAsync(CreateRequest("Event 1"));
        await service.CreateAsync(CreateRequest("Event 2"));

        //Act
        var result = await service.GetAllAsync(title, null, null);

        //Assert
        Assert.Equal(2, result.TotalItems);
    }

    // Тест проверяет, что GetById с несуществующим id бросает NotFoundException
    [Fact]
    public async Task GetById_WithNonExistentId_ShouldThrowNotFound()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        await service.CreateAsync(CreateRequest("Event 1"));
        var nonExistentId = Guid.NewGuid();

        //Act
        //Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(nonExistentId));
        Assert.Contains(nonExistentId.ToString(), ex.Message);
    }

    // Тест проверяет, что Update с несуществующим id бросает NotFoundException и не создаёт новое событие
    [Fact]
    public async Task Update_WithNonExistentId_ShouldThrowNotFound()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        await service.CreateAsync(CreateRequest("Event 1"));

        //Act
        //Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(Guid.NewGuid(), new EventRequest
        {
            Title = "New Title",
            StartAt = new DateTime(2026, 10, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 10, 1, 12, 0, 0)
        }));
        Assert.Single((await service.GetAllAsync(null, null, null)).Items);
    }

    // Тест проверяет, что Delete с несуществующим id бросает NotFoundException
    [Fact]
    public async Task Delete_WithNonExistentId_ShouldThrowNotFound()
    {
        //Arrange
        IEventService service = _database.CreateEventService();
        await service.CreateAsync(CreateRequest("Event 1"));

        //Act
        //Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(Guid.NewGuid()));
    }
}
