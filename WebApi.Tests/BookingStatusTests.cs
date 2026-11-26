using WebApi.Models;

namespace WebApi.Tests;

public class BookingStatusTests
{
    private static Booking CreatePending() => Booking.Create(Event.Create(
        "Test Event", null, new DateTime(2026, 12, 1, 10, 0, 0),
        new DateTime(2026, 12, 1, 11, 0, 0), 1));

    [Fact]
    public void Confirm_ShouldSetStatusAndProcessedAt()
    {
        var pending = CreatePending();

        var confirmed = pending.Confirm();

        Assert.Equal(BookingStatus.Confirmed, confirmed.BookingStatus);
        Assert.InRange(confirmed.ProcessedAt, pending.CreatedAt, DateTime.UtcNow);
        Assert.Equal(pending.Id, confirmed.Id);
        Assert.Same(pending.Event, confirmed.Event);
        Assert.Equal(BookingStatus.Pending, pending.BookingStatus);
    }

    [Fact]
    public void Reject_ShouldSetStatusAndProcessedAt()
    {
        var pending = CreatePending();

        var rejected = pending.Reject();

        Assert.Equal(BookingStatus.Rejected, rejected.BookingStatus);
        Assert.InRange(rejected.ProcessedAt, pending.CreatedAt, DateTime.UtcNow);
        Assert.Equal(pending.Id, rejected.Id);
        Assert.Same(pending.Event, rejected.Event);
        Assert.Equal(BookingStatus.Pending, pending.BookingStatus);
    }
}
