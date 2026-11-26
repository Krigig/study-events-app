using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WebApi.DataAccess;
using WebApi.DataAccess.Repositories;
using WebApi.Exceptions;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class BookingRepositoryTests(PostgreSqlFixture fixture) : RepositoryTestBase(fixture)
{
    private static readonly DateTime Start = new(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);

    private async Task<Guid> CreateEventAsync(int seats = 5)
    {
        await using var scope = Services.CreateAsyncScope();
        var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var @event = Event.Create("Test Event", null, Start, Start.AddHours(1), seats);
        events.Add(@event);
        await events.SaveChangesAsync();
        return @event.Id;
    }

    private async Task<Booking> CreateBookingAsync(Guid eventId, BookingStatus status = BookingStatus.Pending)
    {
        await using var scope = Services.CreateAsyncScope();
        var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var @event = (await events.GetByIdAsync(eventId))!;
        Assert.True(@event.TryReserveSeats());
        var booking = Booking.Create(@event);
        bookings.Add(booking);
        await bookings.SaveChangesAsync();
        if (status != BookingStatus.Pending)
        {
            bookings.Update(status == BookingStatus.Confirmed ? booking.Confirm() : booking.Reject());
            await bookings.SaveChangesAsync();
        }

        return booking;
    }

    [Fact]
    public async Task AddAndSave_ShouldPersistBookingAndReservedSeatTogether()
    {
        var eventId = await CreateEventAsync();
        var expected = await CreateBookingAsync(eventId);
        await using var scope = Services.CreateAsyncScope();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var actual = await bookings.GetByIdAsync(expected.Id);
        Assert.NotNull(actual);
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(eventId, actual.EventId);
        Assert.Equal(BookingStatus.Pending, actual.BookingStatus);
        Assert.Equal(expected.CreatedAt.Ticks / 10, actual.CreatedAt.Ticks / 10);
        Assert.Equal(DateTime.MinValue, actual.ProcessedAt);
        Assert.Equal(4, (await events.GetByIdAsync(eventId))!.AvailableSeats);
    }

    [Fact]
    public async Task GetById_WhenMissing_ShouldReturnNull()
    {
        await using var scope = Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IBookingRepository>().GetByIdAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false, BookingStatus.Confirmed)]
    [InlineData(true, BookingStatus.Confirmed)]
    [InlineData(false, BookingStatus.Rejected)]
    [InlineData(true, BookingStatus.Rejected)]
    public async Task UpdateAndSave_ShouldPersistTrackedOrDetachedStatus(bool detached, BookingStatus status)
    {
        var eventId = await CreateEventAsync();
        var original = await CreateBookingAsync(eventId);
        await using (var scope = Services.CreateAsyncScope())
        {
            var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var pending = detached ? original : (await bookings.GetByIdAsync(original.Id))!;
            bookings.Update(status == BookingStatus.Confirmed ? pending.Confirm() : pending.Reject());
            await bookings.SaveChangesAsync();
        }

        await using var reader = Services.CreateAsyncScope();
        var actual = await reader.ServiceProvider.GetRequiredService<IBookingRepository>().GetByIdAsync(original.Id);
        Assert.NotNull(actual);
        Assert.Equal(status, actual.BookingStatus);
        Assert.Equal(eventId, actual.EventId);
        Assert.NotEqual(DateTime.MinValue, actual.ProcessedAt);
        var context = reader.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedStatus = await context.Database.SqlQueryRaw<string>(
            "SELECT status AS \"Value\" FROM bookings").SingleAsync();
        Assert.Equal(status.ToString(), storedStatus);
    }

    [Fact]
    public async Task GetById_ShouldRefreshTrackedBookingAfterAnotherScopeUpdatesOrDeletesIt()
    {
        var eventId = await CreateEventAsync();
        var original = await CreateBookingAsync(eventId);
        await using var readerScope = Services.CreateAsyncScope();
        var reader = readerScope.ServiceProvider.GetRequiredService<IBookingRepository>();
        Assert.NotNull(await reader.GetByIdAsync(original.Id));
        await using (var writerScope = Services.CreateAsyncScope())
        {
            var writer = writerScope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var pending = (await writer.GetByIdAsync(original.Id))!;
            writer.Update(pending.Confirm());
            await writer.SaveChangesAsync();
            Assert.Equal(BookingStatus.Confirmed, (await reader.GetByIdAsync(original.Id))!.BookingStatus);
            writer.Remove(pending);
            await writer.SaveChangesAsync();
        }

        Assert.Null(await reader.GetByIdAsync(original.Id));
    }

    [Fact]
    public async Task PendingQueries_ShouldReturnOnlyPendingBookingsAcrossEvents()
    {
        var firstEvent = await CreateEventAsync();
        var secondEvent = await CreateEventAsync();
        var first = await CreateBookingAsync(firstEvent);
        var second = await CreateBookingAsync(secondEvent);
        await CreateBookingAsync(firstEvent, BookingStatus.Confirmed);
        await CreateBookingAsync(secondEvent, BookingStatus.Rejected);
        await using var scope = Services.CreateAsyncScope();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var pending = await bookings.GetPendingAsync();
        var ids = await bookings.GetPendingIdsAsync();
        var expected = new[] { first.Id, second.Id }.Order();
        Assert.Equal(expected, pending.Select(b => b.Id).Order());
        Assert.All(pending, b => Assert.Equal(BookingStatus.Pending, b.BookingStatus));
        Assert.Equal(expected, ids.Order());
    }

    [Fact]
    public async Task PendingQueries_WhenEmpty_ShouldReturnEmptyCollections()
    {
        await using var scope = Services.CreateAsyncScope();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        Assert.Empty(await bookings.GetPendingAsync());
        Assert.Empty(await bookings.GetPendingIdsAsync());
    }

    [Fact]
    public async Task RemoveAndSave_ShouldDeleteOnlyBooking()
    {
        var eventId = await CreateEventAsync();
        var original = await CreateBookingAsync(eventId);
        await using (var scope = Services.CreateAsyncScope())
        {
            var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            bookings.Remove((await bookings.GetByIdAsync(original.Id))!);
            await bookings.SaveChangesAsync();
        }

        await using var reader = Services.CreateAsyncScope();
        Assert.Null(await reader.ServiceProvider.GetRequiredService<IBookingRepository>().GetByIdAsync(original.Id));
        Assert.NotNull(await reader.ServiceProvider.GetRequiredService<IEventRepository>().GetByIdAsync(eventId));
    }

    [Fact]
    public async Task Save_WhenEventDoesNotExist_ShouldEnforceForeignKey()
    {
        var missingEvent = Event.Create("Not saved", null, Start, Start.AddHours(1), 1);
        var booking = Booking.Create(missingEvent);
        booking.Event = null!;
        await using var scope = Services.CreateAsyncScope();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        bookings.Add(booking);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => bookings.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        Assert.Empty(await bookings.GetPendingAsync());
    }

    [Fact]
    public async Task FailedSave_ShouldRollbackReservedSeatAndAllowNextSaveInSameScope()
    {
        var eventId = await CreateEventAsync();
        var original = await CreateBookingAsync(eventId);
        await using var scope = Services.CreateAsyncScope();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var @event = (await events.GetByIdAsync(eventId))!;
        Assert.True(@event.TryReserveSeats());
        original.Event = @event;
        bookings.Add(original); // Повтор того же id нарушит PK.
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => bookings.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        var persistedEvent = (await events.GetByIdAsync(eventId))!;
        Assert.Equal(4, persistedEvent.AvailableSeats);
        Assert.Single(await bookings.GetPendingAsync());
        Assert.True(persistedEvent.TryReserveSeats());
        bookings.Add(Booking.Create(persistedEvent));
        await bookings.SaveChangesAsync();
        Assert.Equal(3, (await events.GetByIdAsync(eventId))!.AvailableSeats);
        Assert.Equal(2, (await bookings.GetPendingIdsAsync()).Count);
    }

    [Fact]
    public async Task ConcurrentServiceRequests_ShouldNotOverbookPostgreSql()
    {
        var eventId = await CreateEventAsync(5);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await using var scope = Services.CreateAsyncScope();
            try
            {
                return (await scope.ServiceProvider.GetRequiredService<IBookingService>().CreateBookingAsync(eventId)).Id;
            }
            catch (NoAvailableSeatsException)
            {
                return (Guid?)null;
            }
        })));

        Assert.Equal(5, results.Count(id => id.HasValue));
        Assert.Equal(5, results.Where(id => id.HasValue).Distinct().Count());
        await using var reader = Services.CreateAsyncScope();
        Assert.Equal(0, (await reader.ServiceProvider.GetRequiredService<IEventRepository>().GetByIdAsync(eventId))!.AvailableSeats);
        Assert.Equal(5, (await reader.ServiceProvider.GetRequiredService<IBookingRepository>().GetPendingIdsAsync()).Count);
    }
}
