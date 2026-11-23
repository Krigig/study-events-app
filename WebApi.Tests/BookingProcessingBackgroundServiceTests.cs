using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Interfaces;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Tests;

public class BookingProcessingBackgroundServiceTests
{
    [Fact]
    public async Task ProcessingBatch_ShouldConfirmInParallelAndRejectFailedBookings()
    {
        var eventStore = new InMemoryEventStore();
        IEventService eventService = new EventService(eventStore);
        var bookingStore = new FailingOnceBookingStore();
        IBookingService bookingService = new BookingService(eventStore, bookingStore);

        Guid CreateEvent(int seats) => eventService.Create(new CreateEventRequest
        {
            Title = "Test Event",
            StartAt = new DateTime(2026, 12, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 12, 1, 11, 0, 0),
            TotalSeats = seats
        }).Id;

        var activeEventId = CreateEvent(2);
        var deletedEventId = CreateEvent(1);
        var failedEventId = CreateEvent(1);
        var first = await bookingService.CreateBookingAsync(activeEventId);
        var second = await bookingService.CreateBookingAsync(activeEventId);
        var deleted = await bookingService.CreateBookingAsync(deletedEventId);
        var failed = await bookingService.CreateBookingAsync(failedEventId);
        bookingStore.FailConfirmationFor(failed.Id);
        eventService.Delete(deletedEventId);

        var background = new BookingProcessingBackgroundService(
            bookingStore, eventStore, NullLogger<BookingProcessingBackgroundService>.Instance);
        await background.StartAsync(CancellationToken.None);
        try
        {
            var bookingIds = new[] { first.Id, second.Id, deleted.Id, failed.Id };
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline && bookingIds.Any(id =>
                       bookingStore.GetById(id)!.BookingStatus == BookingStatus.Pending))
            {
                await Task.Delay(50);
            }

            var firstResult = bookingStore.GetById(first.Id)!;
            var secondResult = bookingStore.GetById(second.Id)!;
            Assert.Equal(BookingStatus.Confirmed, firstResult.BookingStatus);
            Assert.Equal(BookingStatus.Confirmed, secondResult.BookingStatus);
            Assert.True((firstResult.ProcessedAt - secondResult.ProcessedAt).Duration() < TimeSpan.FromMilliseconds(1500));
            Assert.Equal(BookingStatus.Rejected, bookingStore.GetById(deleted.Id)!.BookingStatus);
            Assert.Equal(BookingStatus.Rejected, bookingStore.GetById(failed.Id)!.BookingStatus);
            Assert.Equal(1, eventService.GetById(failedEventId).AvailableSeats);
        }
        finally
        {
            await background.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task CancellationDuringDelay_ShouldLeaveBookingPending()
    {
        var eventStore = new InMemoryEventStore();
        IEventService eventService = new EventService(eventStore);
        var bookingStore = new InMemoryBookingStore();
        IBookingService bookingService = new BookingService(eventStore, bookingStore);
        var eventId = eventService.Create(new CreateEventRequest
        {
            Title = "Test Event",
            StartAt = new DateTime(2026, 12, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 12, 1, 11, 0, 0),
            TotalSeats = 1
        }).Id;
        var booking = await bookingService.CreateBookingAsync(eventId);

        var background = new BookingProcessingBackgroundService(
            bookingStore, eventStore, NullLogger<BookingProcessingBackgroundService>.Instance);
        await background.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await background.StopAsync(CancellationToken.None);

        Assert.Equal(BookingStatus.Pending, bookingStore.GetById(booking.Id)!.BookingStatus);
        Assert.Equal(0, eventService.GetById(eventId).AvailableSeats);
    }

    private sealed class FailingOnceBookingStore : IBookingStore
    {
        private readonly InMemoryBookingStore _inner = new();
        private Guid _failedBookingId;
        private int _failOnce;

        public void FailConfirmationFor(Guid id)
        {
            _failedBookingId = id;
            _failOnce = 1;
        }

        public bool TryAdd(Booking booking) => _inner.TryAdd(booking);
        public Booking? GetById(Guid id) => _inner.GetById(id);
        public IReadOnlyCollection<Booking> GetPending() => _inner.GetPending();

        public bool TryUpdate(Booking current, Booking updated)
        {
            if (current.Id == _failedBookingId && updated.BookingStatus == BookingStatus.Confirmed &&
                Interlocked.Exchange(ref _failOnce, 0) == 1)
            {
                throw new InvalidOperationException("Simulated storage failure");
            }

            return _inner.TryUpdate(current, updated);
        }
    }
}
