using WebApi.Interfaces;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Tests;

public class BookingServiceTests
{
    [Fact]
    public async Task CreateBookingAsync_ShouldSetPendingStatusAndCreatedAt()
    {
        var eventId = Guid.NewGuid();
        IBookingService service = new BookingService();

        var booking = await service.CreateBookingAsync(eventId);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.BookingStatus);
        Assert.InRange(booking.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnExistingBooking()
    {
        var eventId = Guid.NewGuid();
        IBookingService service = new BookingService();
        var created = await service.CreateBookingAsync(eventId);

        var result = await service.GetBookingByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
        Assert.Equal(eventId, result.EventId);
    }
}
