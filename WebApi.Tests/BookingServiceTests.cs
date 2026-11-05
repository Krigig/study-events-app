using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Tests;

/// <summary>
/// Юнит-тесты сервиса BookingService.
/// Данные хранятся в памяти, поэтому каждый тест работает со своими экземплярами сервисов.
/// </summary>
public class BookingServiceTests
{
    // Создаёт сервис броней поверх сервиса событий с одним созданным событием
    private static (IBookingService BookingService, IEventService EventService) CreateServices()
    {
        IEventService eventService = new EventService();
        eventService.Create(new EventRequest
        {
            Title = "Test Event",
            StartAt = new DateTime(2026, 10, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 10, 1, 12, 0, 0)
        });
        return (new BookingService(eventService), eventService);
    }

    // Тест проверяет, что создание брони устанавливает Pending, CreatedAt и id события
    [Fact]
    public async Task CreateBookingAsync_ShouldSetPendingStatusAndCreatedAt()
    {
        //Arrange
        var (service, eventService) = CreateServices();
        var eventId = eventService.GetAll(null, null, null).Items[0].Id;

        //Act
        var booking = await service.CreateBookingAsync(eventId);

        //Assert
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.InRange(booking.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    // Тест проверяет, что бронирование несуществующего события бросает NotFoundException
    [Fact]
    public async Task CreateBookingAsync_WithNonExistentEvent_ShouldThrowNotFound()
    {
        //Arrange
        var (service, _) = CreateServices();

        //Act
        //Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBookingAsync(Guid.NewGuid()));
    }

    // Тест проверяет, что GetBookingByIdAsync возвращает созданную бронь
    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnExistingBooking()
    {
        //Arrange
        var (service, eventService) = CreateServices();
        var eventId = eventService.GetAll(null, null, null).Items[0].Id;
        var created = await service.CreateBookingAsync(eventId);

        //Act
        var result = await service.GetBookingByIdAsync(created.Id);

        //Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(eventId, result.EventId);
    }

    // Тест проверяет, что GetBookingByIdAsync с несуществующим id бросает NotFoundException
    [Fact]
    public async Task GetBookingByIdAsync_WithNonExistentId_ShouldThrowNotFound()
    {
        //Arrange
        var (service, _) = CreateServices();

        //Act
        //Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetBookingByIdAsync(Guid.NewGuid()));
    }
}
