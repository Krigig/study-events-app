using WebApi.Models;

namespace WebApi.Tests;

public class BookingStatusTests
{
    private static Booking CreatePending() => new()
    {
        Id = Guid.NewGuid(),
        EventId = Guid.NewGuid(),
        BookingStatus = BookingStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        ProcessedAt = DateTime.MinValue
    };

    [Fact]
    public void Confirm_ShouldSetStatusAndProcessedAt()
    {
        var pending = CreatePending();

        var confirmed = pending.Confirm();

        Assert.Equal(BookingStatus.Confirmed, confirmed.BookingStatus);
        Assert.InRange(confirmed.ProcessedAt, pending.CreatedAt, DateTime.UtcNow);
        Assert.Equal(pending.Id, confirmed.Id);
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
        Assert.Equal(BookingStatus.Pending, pending.BookingStatus);
    }
}
