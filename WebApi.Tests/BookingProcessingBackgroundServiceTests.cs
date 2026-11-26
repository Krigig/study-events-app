using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Tests;

public class BookingProcessingBackgroundServiceTests
{
    [Fact]
    public async Task ProcessingBatch_ShouldSaveConcurrentlyUsingSeparateContexts()
    {
        var interceptor = new ConcurrentConfirmationInterceptor();
        using var database = new TestDatabase(interceptor);
        var eventId = (await database.CreateEventService().CreateAsync(new CreateEventRequest
        {
            Title = "Parallel processing",
            StartAt = new DateTime(2026, 12, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 12, 1, 11, 0, 0),
            TotalSeats = 2
        })).Id;
        var service = database.CreateBookingService();
        var first = await service.CreateBookingAsync(eventId);
        var second = await service.CreateBookingAsync(eventId);

        using var background = new BookingProcessingBackgroundService(
            database.ScopeFactory, NullLogger<BookingProcessingBackgroundService>.Instance);
        await background.StartAsync(CancellationToken.None);
        try
        {
            var reader = database.CreateBookingService();
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline && (await reader.GetPendingBookingsAsync()).Count > 0)
            {
                await Task.Delay(50);
            }

            Assert.Equal(2, interceptor.ContextCount);
            Assert.Equal(BookingStatus.Confirmed, (await reader.GetBookingByIdAsync(first.Id)).Status);
            Assert.Equal(BookingStatus.Confirmed, (await reader.GetBookingByIdAsync(second.Id)).Status);
        }
        finally
        {
            await background.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ProcessingBatch_ShouldConfirmInParallelAndRejectFailedBookings()
    {
        var interceptor = new BookingSaveFailureInterceptor();
        using var database = new TestDatabase(interceptor);
        var eventService = database.CreateEventService();
        var bookingService = database.CreateBookingService();

        async Task<Guid> CreateEvent(int seats) => (await eventService.CreateAsync(new CreateEventRequest
        {
            Title = "Test Event",
            StartAt = new DateTime(2026, 12, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 12, 1, 11, 0, 0),
            TotalSeats = seats
        })).Id;

        var activeEventId = await CreateEvent(2);
        var deletedEventId = await CreateEvent(1);
        var failedEventId = await CreateEvent(1);
        var first = await bookingService.CreateBookingAsync(activeEventId);
        var second = await bookingService.CreateBookingAsync(activeEventId);
        var deleted = await bookingService.CreateBookingAsync(deletedEventId);
        var failed = await bookingService.CreateBookingAsync(failedEventId);
        interceptor.FailConfirmationFor = failed.Id;
        await eventService.DeleteAsync(deletedEventId);

        using var background = new BookingProcessingBackgroundService(
            database.ScopeFactory, NullLogger<BookingProcessingBackgroundService>.Instance);
        await background.StartAsync(CancellationToken.None);
        try
        {
            var reader = database.CreateBookingService();
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline && (await reader.GetPendingBookingsAsync()).Count > 0)
            {
                await Task.Delay(50);
            }

            var firstResult = await reader.GetBookingByIdAsync(first.Id);
            var secondResult = await reader.GetBookingByIdAsync(second.Id);
            Assert.Equal(BookingStatus.Confirmed, firstResult.Status);
            Assert.Equal(BookingStatus.Confirmed, secondResult.Status);
            Assert.True((firstResult.ProcessedAt - secondResult.ProcessedAt).Duration() < TimeSpan.FromMilliseconds(1500));
            // InMemory не выполняет каскадное удаление на стороне БД: проверяем обработку отсутствующего события.
            Assert.Equal(BookingStatus.Rejected, (await reader.GetBookingByIdAsync(deleted.Id)).Status);
            Assert.Equal(BookingStatus.Rejected, (await reader.GetBookingByIdAsync(failed.Id)).Status);
            Assert.Equal(1, (await eventService.GetByIdAsync(failedEventId)).AvailableSeats);
        }
        finally
        {
            await background.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task CancellationDuringDelay_ShouldLeaveBookingPending()
    {
        using var database = new TestDatabase();
        var eventService = database.CreateEventService();
        var bookingService = database.CreateBookingService();
        var eventId = (await eventService.CreateAsync(new CreateEventRequest
        {
            Title = "Test Event",
            StartAt = new DateTime(2026, 12, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 12, 1, 11, 0, 0),
            TotalSeats = 1
        })).Id;
        var booking = await bookingService.CreateBookingAsync(eventId);

        using var background = new BookingProcessingBackgroundService(
            database.ScopeFactory, NullLogger<BookingProcessingBackgroundService>.Instance);
        await background.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await background.StopAsync(CancellationToken.None);

        Assert.Equal(BookingStatus.Pending, (await bookingService.GetBookingByIdAsync(booking.Id)).Status);
        Assert.Equal(0, (await eventService.GetByIdAsync(eventId)).AvailableSeats);
    }
    private sealed class ConcurrentConfirmationInterceptor : SaveChangesInterceptor
    {
        private readonly ConcurrentDictionary<Guid, byte> _contexts = new();
        private readonly TaskCompletionSource _bothSaving = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ContextCount => _contexts.Count;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context!;
            if (context.ChangeTracker.Entries<Booking>().Any(e =>
                    e.State == EntityState.Modified && e.Entity.BookingStatus == BookingStatus.Confirmed))
            {
                _contexts.TryAdd(context.ContextId.InstanceId, 0);
                if (_contexts.Count == 2)
                {
                    _bothSaving.TrySetResult();
                }

                // Первый SaveChangesAsync ждёт входа второго: последовательная обработка провалит тест.
                await _bothSaving.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }

            return result;
        }
    }
}
