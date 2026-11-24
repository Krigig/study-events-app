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
    private static Guid CreateTestEvent(IEventService eventService, int totalSeats) =>
        eventService.Create(new CreateEventRequest
        {
            Title = "Test Event",
            TotalSeats = totalSeats,
            StartAt = new DateTime(2026, 10, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 10, 1, 12, 0, 0)
        }).Id;

    // Создаёт сервис броней поверх сервиса событий с одним созданным событием
    private static (IBookingService BookingService, IEventService EventService, Guid EventId) CreateServices(int totalSeats = 1000)
    {
        var eventStore = new InMemoryEventStore();
        IEventService eventService = new EventService(eventStore);
        var eventId = CreateTestEvent(eventService, totalSeats);
        return (new BookingService(eventStore, new InMemoryBookingStore()), eventService, eventId);
    }

    // Тест проверяет, что создание брони устанавливает Pending, CreatedAt и id события
    [Fact]
    public async Task CreateBookingAsync_ShouldSetPendingStatusAndCreatedAt()
    {
        //Arrange
        var (service, eventService, eventId) = CreateServices(totalSeats: 5);

        //Act
        var booking = await service.CreateBookingAsync(eventId);

        //Assert
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.InRange(booking.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.Equal(4, eventService.GetById(eventId).AvailableSeats);
    }

    // Тест проверяет, что несколько броней одного события получают разные id
    [Fact]
    public async Task CreateBookingAsync_MultipleBookingsForSameEvent_ShouldHaveUniqueIds()
    {
        // Arrange
        var (service, eventService, eventId) = CreateServices(totalSeats: 3);

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
        Assert.Equal(0, eventService.GetById(eventId).AvailableSeats);
        await Assert.ThrowsAsync<NoAvailableSeatsException>(() => service.CreateBookingAsync(eventId));
    }

    // Тест проверяет, что бронирование несуществующего события бросает NotFoundException
    [Fact]
    public async Task CreateBookingAsync_WithNonExistentEvent_ShouldThrowNotFound()
    {
        //Arrange
        var (service, _, _) = CreateServices();

        //Act
        //Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBookingAsync(Guid.NewGuid()));
    }

    // Тест проверяет, что для удалённого события нельзя создать бронь
    [Fact]
    public async Task CreateBookingAsync_WithDeletedEvent_ShouldThrowNotFound()
    {
        // Arrange
        var (service, eventService, eventId) = CreateServices();
        eventService.Delete(eventId);

        // Act
        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBookingAsync(eventId));
    }

    // Тест проверяет, что GetBookingByIdAsync возвращает созданную бронь
    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnExistingBooking()
    {
        //Arrange
        var (service, _, eventId) = CreateServices();
        var created = await service.CreateBookingAsync(eventId);

        //Act
        var result = await service.GetBookingByIdAsync(created.Id);

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
        var (service, _, _) = CreateServices();

        //Act
        //Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetBookingByIdAsync(Guid.NewGuid()));
    }

    // Тест проверяет, что получение брони отражает смену статуса после подтверждения
    [Fact]
    public async Task GetBookingByIdAsync_AfterConfirm_ShouldReturnConfirmedBooking()
    {
        // Arrange
        var (service, _, eventId) = CreateServices();
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
        var (service, _, eventId) = CreateServices();
        const int bookingCount = 1000;

        var createTasks = Enumerable.Range(0, bookingCount)
            .Select(_ => Task.Run(() => service.CreateBookingAsync(eventId)))
            .ToArray();
        var readTask = Task.Run(async () =>
        {
            while (createTasks.Any(task => !task.IsCompleted))
            {
                await service.GetPendingBookingsAsync();
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
        var (service, eventService, eventId) = CreateServices(totalSeats: 1);
        await service.CreateBookingAsync(eventId);

        var exception = await Assert.ThrowsAsync<NoAvailableSeatsException>(
            () => service.CreateBookingAsync(eventId));

        Assert.Equal("No available seats for this event", exception.Message);
        Assert.Equal(0, eventService.GetById(eventId).AvailableSeats);
        Assert.Single(await service.GetPendingBookingsAsync());
    }

    [Fact]
    public async Task CreateBookingAsync_WhenSeatWasReservedElsewhere_ShouldThrowNoAvailableSeats()
    {
        var (service, eventService, eventId) = CreateServices(totalSeats: 1);
        Assert.True(eventService.TryReserveSeat(eventId));

        await Assert.ThrowsAsync<NoAvailableSeatsException>(() => service.CreateBookingAsync(eventId));

        Assert.Equal(0, eventService.GetById(eventId).AvailableSeats);
        Assert.Empty(await service.GetPendingBookingsAsync());
    }

    [Fact]
    public async Task CreateBookingAsync_WhenBookingCannotBeSaved_ShouldReleaseSeat()
    {
        var eventStore = new InMemoryEventStore();
        IEventService eventService = new EventService(eventStore);
        var eventId = CreateTestEvent(eventService, totalSeats: 1);
        var service = new BookingService(eventStore, new RejectingBookingStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBookingAsync(eventId));

        Assert.Equal(1, eventService.GetById(eventId).AvailableSeats);
    }

    [Fact]
    public async Task ConcurrentBookings_ShouldNeverExceedSeatCount()
    {
        const int seatCount = 5;
        var (service, eventService, eventId) = CreateServices(totalSeats: seatCount);

        var attempts = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            try
            {
                await service.CreateBookingAsync(eventId);
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
        Assert.Equal(0, eventService.GetById(eventId).AvailableSeats);
        Assert.Equal(seatCount, (await service.GetPendingBookingsAsync()).Count);
    }

    [Fact]
    public async Task ConcurrentBookings_WithinLimit_ShouldHaveUniqueIds()
    {
        var (service, eventService, eventId) = CreateServices(totalSeats: 10);

        var bookings = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => service.CreateBookingAsync(eventId))));

        Assert.Equal(10, bookings.Select(booking => booking.Id).Distinct().Count());
        Assert.All(bookings, booking => Assert.NotEqual(Guid.Empty, booking.Id));
        Assert.Equal(0, eventService.GetById(eventId).AvailableSeats);
    }

    [Fact]
    public async Task RejectedBooking_AfterReleasingSeat_AllowsNewBooking()
    {
        var eventStore = new InMemoryEventStore();
        IEventService eventService = new EventService(eventStore);
        var eventId = CreateTestEvent(eventService, totalSeats: 1);
        var bookingStore = new InMemoryBookingStore();
        IBookingService service = new BookingService(eventStore, bookingStore);
        var first = await service.CreateBookingAsync(eventId);
        var pending = bookingStore.GetById(first.Id)!;

        var rejected = pending.Reject();
        Assert.True(bookingStore.TryUpdate(pending, rejected));
        eventService.ReleaseSeat(eventId);
        Assert.Equal(1, eventService.GetById(eventId).AvailableSeats);
        var second = await service.CreateBookingAsync(eventId);

        Assert.Equal(BookingStatus.Rejected, (await service.GetBookingByIdAsync(first.Id)).Status);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(BookingStatus.Pending, second.Status);
        Assert.Equal(0, eventService.GetById(eventId).AvailableSeats);
    }

    private sealed class RejectingBookingStore : IBookingStore
    {
        public bool TryAdd(Booking booking) => false;
        public Booking? GetById(Guid id) => null;
        public IReadOnlyCollection<Booking> GetPending() => [];
        public bool TryUpdate(Booking current, Booking updated) => false;
    }
}
