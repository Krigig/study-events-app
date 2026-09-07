using WebApi.Interfaces;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Tests;

/// <summary>
/// Юнит-тесты сервиса EventService.
/// Данные хранятся в памяти, поэтому каждый тест работает со своим экземпляром сервиса.
/// </summary>
public class EventServiceTests
{
    // Заполнитель события для компактного Arrange
    private static EventRequest CreateRequest(
        string title = "Test Event",
        DateTime? startAt = null,
        DateTime? endAt = null)
        => new()
        {
            Title = title,
            Description = "description",
            StartAt = startAt ?? new DateTime(2026, 10, 1, 10, 0, 0),
            EndAt = endAt ?? new DateTime(2026, 10, 1, 12, 0, 0)
        };

    // Тест проверяет, что создание события присваивает id и сохраняет все поля
    [Fact]
    public void Create_ShouldAssignIdAndFields()
    {
        //Arrange
        IEventService service = new EventService();
        var request = CreateRequest("Lecture .NET", startAt: new DateTime(2026, 10, 1, 9, 0, 0),
            endAt: new DateTime(2026, 10, 1, 11, 0, 0));

        //Act
        var result = service.Create(request);

        //Assert
        Assert.Equal(1, result.Id);
        Assert.Equal("Lecture .NET", result.Title);
        Assert.Equal("description", result.Description);
        Assert.Equal(new DateTime(2026, 10, 1, 9, 0, 0), result.StartAt);
        Assert.Equal(new DateTime(2026, 10, 1, 11, 0, 0), result.EndAt);
    }

    // Тест проверяет, что GetAll возвращает все созданные события
    [Fact]
    public void GetAll_ShouldReturnAllEvents()
    {
        //Arrange
        IEventService service = new EventService();
        service.Create(CreateRequest("Event 1"));
        service.Create(CreateRequest("Event 2"));
        service.Create(CreateRequest("Event 3"));

        //Act
        var result = service.GetAll(null, null, null);

        //Assert
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, item => Assert.False(string.IsNullOrEmpty(item.Title)));
    }

    // Тест проверяет, что GetById возвращает событие по существующему id
    [Fact]
    public void GetById_ShouldReturnEvent()
    {
        //Arrange
        IEventService service = new EventService();
        var created = service.Create(CreateRequest("Event 1"));

        //Act
        var result = service.GetById(created.Id);

        //Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Event 1", result.Title);
    }

    // Тест проверяет, что обновление изменяет поля существующего события
    [Fact]
    public void Update_ShouldUpdateExistingEvent()
    {
        //Arrange
        IEventService service = new EventService();
        var created = service.Create(CreateRequest("Old Title"));
        var updatedRequest = CreateRequest("New Title",
            startAt: new DateTime(2026, 11, 1, 9, 0, 0),
            endAt: new DateTime(2026, 11, 1, 18, 0, 0));

        //Act
        var result = service.Update(created.Id, updatedRequest);

        //Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("New Title", result.Title);
        Assert.Equal(new DateTime(2026, 11, 1, 9, 0, 0), result.StartAt);
        Assert.Equal(new DateTime(2026, 11, 1, 18, 0, 0), result.EndAt);

        // Изменение видно и через GetById
        var fromGet = service.GetById(created.Id);
        Assert.Equal("New Title", fromGet!.Title);
    }

    // Тест проверяет, что удаление убирает событие из хранилища
    [Fact]
    public void Delete_ShouldRemoveEvent()
    {
        //Arrange
        IEventService service = new EventService();
        var created = service.Create(CreateRequest("Event 1"));

        //Act
        var result = service.Delete(created.Id);

        //Assert
        Assert.True(result);
        Assert.Null(service.GetById(created.Id));
    }

    // Тест проверяет, что фильтрация по названию — частичное совпадение без учёта регистра
    [Fact]
    public void GetAll_FilterByTitle_ShouldBeCaseInsensitivePartial()
    {
        //Arrange
        IEventService service = new EventService();
        service.Create(CreateRequest("Lecture .NET Basics"));
        service.Create(CreateRequest("Workshop ADVANCED C#"));
        service.Create(CreateRequest("Meetup dotnet community"));
        var expectedTitles = new List<string> { "Lecture .NET Basics", "Meetup dotnet community" };

        //Act
        var result = service.GetAll("NET", null, null);

        //Assert
        Assert.Equal(expectedTitles, result.Items.Select(e => e.Title));
        Assert.DoesNotContain(result.Items, e => e.Title == "Workshop ADVANCED C#");
    }

    // Тест проверяет, что фильтр from включает события, начинающиеся ровно с указанной даты (не раньше)
    [Fact]
    public void GetAll_FilterByFrom_ShouldIncludeBoundary()
    {
        //Arrange
        IEventService service = new EventService();
        service.Create(CreateRequest("Early", startAt: new DateTime(2026, 9, 1, 10, 0, 0),
            endAt: new DateTime(2026, 9, 1, 12, 0, 0)));
        service.Create(CreateRequest("Boundary", startAt: new DateTime(2026, 10, 1, 10, 0, 0),
            endAt: new DateTime(2026, 10, 1, 12, 0, 0)));
        service.Create(CreateRequest("Late", startAt: new DateTime(2026, 11, 1, 10, 0, 0),
            endAt: new DateTime(2026, 11, 1, 12, 0, 0)));
        var from = new DateTime(2026, 10, 1, 10, 0, 0);

        //Act
        var result = service.GetAll(null, from, null);

        //Assert
        Assert.Equal(new List<string> { "Boundary", "Late" }, result.Items.Select(e => e.Title));
    }

    // Тест проверяет, что фильтр to включает события, заканчивающиеся ровно в указанную дату (не позже)
    [Fact]
    public void GetAll_FilterByTo_ShouldIncludeBoundary()
    {
        //Arrange
        IEventService service = new EventService();
        service.Create(CreateRequest("Early", startAt: new DateTime(2026, 9, 1, 10, 0, 0),
            endAt: new DateTime(2026, 9, 1, 12, 0, 0)));
        service.Create(CreateRequest("Boundary", startAt: new DateTime(2026, 10, 1, 10, 0, 0),
            endAt: new DateTime(2026, 10, 1, 12, 0, 0)));
        service.Create(CreateRequest("Late", startAt: new DateTime(2026, 11, 1, 10, 0, 0),
            endAt: new DateTime(2026, 11, 1, 12, 0, 0)));
        var to = new DateTime(2026, 10, 1, 12, 0, 0);

        //Act
        var result = service.GetAll(null, null, to);

        //Assert
        Assert.Equal(new List<string> { "Early", "Boundary" }, result.Items.Select(e => e.Title));
    }

    // Тест проверяет, что пагинация возвращает нужную страницу, а totalItems считается до Skip/Take
    [Fact]
    public void GetAll_Pagination_ShouldSkipAndTake()
    {
        //Arrange
        IEventService service = new EventService();
        for (var i = 1; i <= 7; i++)
        {
            service.Create(CreateRequest($"Event {i}"));
        }

        //Act
        var result = service.GetAll(null, null, null, page: 2, pageSize: 3);

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
    public void GetAll_Pagination_LastPage_ShouldContainRemainder()
    {
        //Arrange
        IEventService service = new EventService();
        for (var i = 1; i <= 7; i++)
        {
            service.Create(CreateRequest($"Event {i}"));
        }

        //Act
        var result = service.GetAll(null, null, null, page: 3, pageSize: 3);

        //Assert
        Assert.Single(result.Items);
        Assert.Equal("Event 7", result.Items[0].Title);
    }

    // Тест проверяет, что все фильтры работают совместно (логическое И) вместе с пагинацией
    [Fact]
    public void GetAll_CombinedFilters_ShouldApplyAllTogether()
    {
        //Arrange
        IEventService service = new EventService();
        service.Create(CreateRequest("dotnet lecture", startAt: new DateTime(2026, 10, 1, 10, 0, 0),
            endAt: new DateTime(2026, 10, 1, 12, 0, 0)));
        service.Create(CreateRequest("dotnet workshop", startAt: new DateTime(2026, 11, 5, 9, 0, 0),
            endAt: new DateTime(2026, 11, 5, 17, 0, 0)));
        service.Create(CreateRequest("dotnet meetup", startAt: new DateTime(2026, 12, 10, 18, 0, 0),
            endAt: new DateTime(2026, 12, 10, 21, 0, 0)));
        service.Create(CreateRequest("java conference", startAt: new DateTime(2026, 11, 6, 9, 0, 0),
            endAt: new DateTime(2026, 11, 6, 17, 0, 0)));

        //Act
        var result = service.GetAll(
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

    // Тест проверяет, что GetById с несуществующим id возвращает null
    [Fact]
    public void GetById_WithNonExistentId_ShouldReturnNull()
    {
        //Arrange
        IEventService service = new EventService();
        service.Create(CreateRequest("Event 1"));
        var nonExistentId = 999;

        //Act
        var result = service.GetById(nonExistentId);

        //Assert
        Assert.Null(result);
    }

    // Тест проверяет, что Update с несуществующим id возвращает null и не создаёт новое событие
    [Fact]
    public void Update_WithNonExistentId_ShouldReturnNull()
    {
        //Arrange
        IEventService service = new EventService();
        service.Create(CreateRequest("Event 1"));

        //Act
        var result = service.Update(999, CreateRequest("New Title"));

        //Assert
        Assert.Null(result);
        Assert.Single(service.GetAll(null, null, null).Items);
    }

    // Тест проверяет, что Delete с несуществующим id возвращает false
    [Fact]
    public void Delete_WithNonExistentId_ShouldReturnFalse()
    {
        //Arrange
        IEventService service = new EventService();
        service.Create(CreateRequest("Event 1"));

        //Act
        var result = service.Delete(999);

        //Assert
        Assert.False(result);
    }
}
