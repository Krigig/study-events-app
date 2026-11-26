using Microsoft.Extensions.DependencyInjection;
using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Tests;

/// <summary>
/// Юнит-тесты сервиса BookingService.
/// Каждый тест использует отдельную базу EF Core InMemory.
/// </summary>
public class BookingServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    public void Dispose() => _database.Dispose();

    private static async Task<Guid> CreateTestEvent(IEventService eventService, int totalSeats) =>
        (await eventService.CreateAsync(new CreateEventRequest
        {
            Title = "Test Event",
            TotalSeats = totalSeats,
            StartAt = new DateTime(2026, 10, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 10, 1, 12, 0, 0)
        })).Id;

    // Создаёт сервис броней поверх сервиса событий с одним созданным событием
    private async Task<(IBookingService BookingService, IEventService EventService, Guid EventId)> CreateServices(int totalSeats = 1000)
    {
        IEventService eventService = _database.CreateEventService();
        var eventId = await CreateTestEvent(eventService, totalSeats);
        return (_database.CreateBookingService(), eventService, eventId);
    }

    // Тест проверяет, что создание брони устанавливает Pending, CreatedAt и id события
    [Fact]
    public async Task CreateBookingAsync_ShouldSetPendingStatusAndCreatedAt()
    {
        //Arrange
        var (service, eventService, eventId) = await CreateServices(totalSeats: 5);

        //Act
        var booking = await service.CreateBookingAsync(eventId);

        //Assert
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.InRange(booking.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.Equal(4, (await eventService.GetByIdAsync(eventId)).AvailableSeats);
    }

    // Тест проверяет, что несколько броней одного события получают разные id
    [Fact]
    public async Task CreateBookingAsync_MultipleBookingsForSameEvent_ShouldHaveUniqueIds()
    {
        // Arrange
        var (service, eventService, eventId) = await CreateServices(totalSeats: 3);

        // Act
        var bookings = new[]
        {
            await service.CreateBookingAsync(eventId),
            await service.CreateBookingAsync(eventId),
            await service.CreateBookingAsync(eventId)
        };

        // Assert
        Assert.Equal(bookings.Length, bookings.Select(booking => booking.Id).Distinct().Count());
        Assert.All(bookings, booking =>
        {
            Assert.NotEqual(Guid.Empty, booking.Id);
            Assert.Equal(eventId, booking.EventId);
            Assert.Equal(BookingStatus.Pending, booking.Status);
        });
        Assert.Equal(0, (await eventService.GetByIdAsync(eventId)).AvailableSeats);
        await Assert.ThrowsAsync<NoAvailableSeatsException>(() => service.CreateBookingAsync(eventId));
    }

    // Тест проверяет, что бронирование несуществующего события бросает NotFoundException
    [Fact]
    public async Task CreateBookingAsync_WithNonExistentEvent_ShouldThrowNotFound()
    {
        //Arrange
        var (service, _, _) = await CreateServices();

        //Act
        //Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBookingAsync(Guid.NewGuid()));
    }

    // Тест проверяет, что для удалённого события нельзя создать бронь
    [Fact]
    public async Task CreateBookingAsync_WithDeletedEvent_ShouldThrowNotFound()
    {
        // Arrange
        var (service, eventService, eventId) = await CreateServices();
        await eventService.DeleteAsync(eventId);

        // Act
        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBookingAsync(eventId));
    }

    // Тест проверяет, что GetBookingByIdAsync возвращает созданную бронь
    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnExistingBooking()
    {
        //Arrange
        var (service, _, eventId) = await CreateServices();
        var created = await service.CreateBookingAsync(eventId);

        //Act
        using var scope = _database.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var result = await reader.GetBookingByIdAsync(created.Id);

        //Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(created.Status, result.Status);
        Assert.Equal(created.CreatedAt, result.CreatedAt);
    }

    // Тест проверяет, что GetBookingByIdAsync с несуществующим id бросает NotFoundException
    [Fact]
    public async Task GetBookingByIdAsync_WithNonExistentId_ShouldThrowNotFound()
    {
        //Arrange
        var (service, _, _) = await CreateServices();

        //Act
        //Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetBookingByIdAsync(Guid.NewGuid()));
    }

    // Тест проверяет, что получение брони отражает смену статуса после подтверждения
    [Fact]
    public async Task GetBookingByIdAsync_AfterConfirm_ShouldReturnConfirmedBooking()
    {
        // Arrange
        var (service, _, eventId) = await CreateServices();
        var created = await service.CreateBookingAsync(eventId);

        // Act
        await service.ConfirmBookingAsync(created.Id);
        var result = await service.GetBookingByIdAsync(created.Id);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, result.Status);
        Assert.InRange(result.ProcessedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task ConcurrentCreateAndRead_ShouldRetainAllBookings()
    {
        var (service, _, eventId) = await CreateServices();
        const int bookingCount = 1000;

        var createTasks = Enumerable.Range(0, bookingCount)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = _database.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                return await bookingService.CreateBookingAsync(eventId);
            }))
            .ToArray();
        var readTask = Task.Run(async () =>
        {
            using var scope = _database.CreateScope();
            var reader = scope.ServiceProvider.GetRequiredService<IBookingService>();
            while (createTasks.Any(task => !task.IsCompleted))
            {
                await reader.GetPendingBookingsAsync();
                await Task.Yield();
            }
        });

        var created = await Task.WhenAll(createTasks);
        await readTask;

        var pending = await service.GetPendingBookingsAsync();
        Assert.Equal(bookingCount, pending.Count);
        Assert.Equal(bookingCount, created.Select(booking => booking.Id).Distinct().Count());
    }

    [Fact]
    public async Task CreateBookingAsync_WhenEventIsFull_ShouldThrowAndNotCreateBooking()
    {
        var (service, eventService, eventId) = await CreateServices(totalSeats: 1);
        await service.CreateBookingAsync(eventId);

        var exception = await Assert.ThrowsAsync<NoAvailableSeatsException>(
            () => service.CreateBookingAsync(eventId));

        Assert.Equal("No available seats for this event", exception.Message);
        Assert.Equal(0, (await eventService.GetByIdAsync(eventId)).AvailableSeats);
        Assert.Single(await service.GetPendingBookingsAsync());
    }

    [Fact]
    public async Task CreateBookingAsync_WhenSeatWasReservedElsewhere_ShouldThrowNoAvailableSeats()
    {
        var (service, eventService, eventId) = await CreateServices(totalSeats: 1);
        Assert.True(await eventService.TryReserveSeatAsync(eventId));

        await Assert.ThrowsAsync<NoAvailableSeatsException>(() => service.CreateBookingAsync(eventId));

        Assert.Equal(0, (await eventService.GetByIdAsync(eventId)).AvailableSeats);
        Assert.Empty(await service.GetPendingBookingsAsync());
    }

    [Fact]
    public async Task CreateBookingAsync_WhenBookingCannotBeSaved_ShouldReleaseSeat()
    {
        var interceptor = new BookingSaveFailureInterceptor { FailCreation = true };
        using var database = new TestDatabase(interceptor);
        IEventService eventService = database.CreateEventService();
        var eventId = await CreateTestEvent(eventService, totalSeats: 1);
        var service = database.CreateBookingService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBookingAsync(eventId));

        Assert.Equal(1, (await eventService.GetByIdAsync(eventId)).AvailableSeats);
        Assert.Empty(await database.CreateBookingService().GetPendingBookingsAsync());
        interceptor.FailCreation = false;
        await service.CreateBookingAsync(eventId);
        Assert.Equal(0, (await eventService.GetByIdAsync(eventId)).AvailableSeats);
    }

    [Fact]
    public async Task ConcurrentBookings_ShouldNeverExceedSeatCount()
    {
        const int seatCount = 5;
        var (service, eventService, eventId) = await CreateServices(totalSeats: seatCount);

        var attempts = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            using var scope = _database.CreateScope();
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            try
            {
                await bookingService.CreateBookingAsync(eventId);
                return true;
            }
            catch (NoAvailableSeatsException)
            {
                return false;
            }
        }));

        var results = await Task.WhenAll(attempts);
        Assert.Equal(seatCount, results.Count(created => created));
        Assert.Equal(15, results.Count(created => !created));
        Assert.Equal(0, (await eventService.GetByIdAsync(eventId)).AvailableSeats);
        Assert.Equal(seatCount, (await service.GetPendingBookingsAsync()).Count);
    }

    [Fact]
    public async Task ConcurrentBookings_WithinLimit_ShouldHaveUniqueIds()
    {
        var (service, eventService, eventId) = await CreateServices(totalSeats: 10);

        var bookings = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = _database.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                return await bookingService.CreateBookingAsync(eventId);
            })));

        Assert.Equal(10, bookings.Select(booking => booking.Id).Distinct().Count());
        Assert.All(bookings, booking => Assert.NotEqual(Guid.Empty, booking.Id));
        Assert.Equal(0, (await eventService.GetByIdAsync(eventId)).AvailableSeats);
    }

    [Fact]
    public async Task RejectedBooking_AfterReleasingSeat_AllowsNewBooking()
    {
        IEventService eventService = _database.CreateEventService();
        var eventId = await CreateTestEvent(eventService, totalSeats: 1);
        IBookingService service = _database.CreateBookingService();
        var first = await service.CreateBookingAsync(eventId);
        var context = _database.CreateContext();
        var pending = (await context.Bookings.FindAsync(first.Id))!;

        var rejected = pending.Reject();
        context.Entry(pending).CurrentValues.SetValues(rejected);
        await context.SaveChangesAsync();
        await eventService.ReleaseSeatAsync(eventId);
        Assert.Equal(1, (await eventService.GetByIdAsync(eventId)).AvailableSeats);
        var second = await service.CreateBookingAsync(eventId);

        Assert.Equal(BookingStatus.Rejected, (await service.GetBookingByIdAsync(first.Id)).Status);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(BookingStatus.Pending, second.Status);
        Assert.Equal(0, (await eventService.GetByIdAsync(eventId)).AvailableSeats);
    }

}
