using System.ComponentModel.DataAnnotations;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Tests;

public class EventSeatTests
{
    private static Event CreateEvent(int totalSeats) => Event.Create(
        "Test Event", null, new DateTime(2026, 12, 1, 10, 0, 0),
        new DateTime(2026, 12, 1, 11, 0, 0), totalSeats);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidTotalSeats_ShouldThrowValidationException(int totalSeats)
    {
        Assert.Throws<ValidationException>(() => CreateEvent(totalSeats));
    }

    [Fact]
    public void ReserveAndRelease_ShouldKeepSeatCountsWithinCapacity()
    {
        var @event = CreateEvent(2);

        Assert.True(@event.TryReserveSeats());
        Assert.True(@event.TryReserveSeats());
        Assert.False(@event.TryReserveSeats());
        Assert.Equal(0, @event.AvailableSeats);

        @event.ReleaseSeats();
        Assert.Equal(1, @event.AvailableSeats);
        Assert.Throws<InvalidOperationException>(() => @event.ReleaseSeats(2));
    }

    [Fact]
    public void Update_ShouldPreserveSeatCounts()
    {
        var service = new EventService();
        var request = new CreateEventRequest
        {
            Title = "Test Event",
            StartAt = new DateTime(2026, 12, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 12, 1, 11, 0, 0),
            TotalSeats = 3
        };
        var eventId = service.Create(request).Id;
        Assert.True(service.TryReserveSeat(eventId));
        Assert.True(service.TryReserveSeat(eventId));

        var updated = service.Update(eventId, new EventRequest
        {
            Title = "Updated Event",
            StartAt = request.StartAt,
            EndAt = request.EndAt
        });

        Assert.Equal("Updated Event", updated.Title);
        Assert.Equal(3, updated.TotalSeats);
        Assert.Equal(1, updated.AvailableSeats);
        Assert.Equal(3, service.GetById(eventId).TotalSeats);
    }

    [Fact]
    public async Task ConcurrentReservations_ShouldNotExceedCapacity()
    {
        var @event = CreateEvent(10);

        var results = await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => @event.TryReserveSeats())));

        Assert.Equal(10, results.Count(reserved => reserved));
        Assert.Equal(0, @event.AvailableSeats);
    }
}
