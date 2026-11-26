using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WebApi.DataAccess;
using WebApi.DataAccess.Repositories;
using WebApi.Models;

namespace WebApi.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class EventRepositoryTests(PostgreSqlFixture fixture) : RepositoryTestBase(fixture)
{
    private static readonly DateTime Start = new(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Event CreateEvent(string title = "Test Event", int day = 0, string? description = "Description") =>
        Event.Create(title, description, Start.AddDays(day), Start.AddDays(day).AddHours(1), 5);

    private async Task SeedAsync(params Event[] events)
    {
        await using var scope = Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        foreach (var @event in events)
        {
            repository.Add(@event);
        }

        await repository.SaveChangesAsync();
    }

    [Fact]
    public async Task AddAndSave_ShouldPersistAllFieldsAcrossScopes()
    {
        var expected = CreateEvent();
        await SeedAsync(expected);
        await using var scope = Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var actual = await repository.GetByIdAsync(expected.Id);

        Assert.NotNull(actual);
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.StartAt, actual.StartAt);
        Assert.Equal(DateTimeKind.Utc, actual.StartAt.Kind);
        Assert.Equal(expected.EndAt, actual.EndAt);
        Assert.Equal(5, actual.TotalSeats);
        Assert.Equal(5, actual.AvailableSeats);
    }

    [Fact]
    public async Task GetById_WhenMissing_ShouldReturnNull()
    {
        await using var scope = Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IEventRepository>().GetByIdAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateAndSave_ShouldPersistTrackedOrDetachedEntity(bool detached)
    {
        var @event = CreateEvent();
        await SeedAsync(@event);
        await using (var scope = Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            var updated = detached ? @event : (await repository.GetByIdAsync(@event.Id))!;
            updated.Title = "Updated";
            updated.Description = null;
            updated.StartAt = Start.AddDays(1);
            updated.EndAt = Start.AddDays(1).AddHours(2);
            Assert.True(updated.TryReserveSeats());
            repository.Update(updated);
            await repository.SaveChangesAsync();
        }

        await using var readScope = Services.CreateAsyncScope();
        var actual = await readScope.ServiceProvider.GetRequiredService<IEventRepository>().GetByIdAsync(@event.Id);
        Assert.NotNull(actual);
        Assert.Equal("Updated", actual.Title);
        Assert.Null(actual.Description);
        Assert.Equal(Start.AddDays(1), actual.StartAt);
        Assert.Equal(Start.AddDays(1).AddHours(2), actual.EndAt);
        Assert.Equal(4, actual.AvailableSeats);
    }

    [Fact]
    public async Task GetById_ShouldRefreshTrackedEntityAfterAnotherScopeUpdatesOrDeletesIt()
    {
        var @event = CreateEvent();
        await SeedAsync(@event);
        await using var readerScope = Services.CreateAsyncScope();
        var reader = readerScope.ServiceProvider.GetRequiredService<IEventRepository>();
        Assert.NotNull(await reader.GetByIdAsync(@event.Id));
        await using (var writerScope = Services.CreateAsyncScope())
        {
            var writer = writerScope.ServiceProvider.GetRequiredService<IEventRepository>();
            var updated = (await writer.GetByIdAsync(@event.Id))!;
            updated.Title = "Changed elsewhere";
            writer.Update(updated);
            await writer.SaveChangesAsync();
            Assert.Equal(updated.Title, (await reader.GetByIdAsync(@event.Id))!.Title);
            writer.Remove(updated);
            await writer.SaveChangesAsync();
        }

        Assert.Null(await reader.GetByIdAsync(@event.Id));
    }

    [Theory]
    [InlineData(false, false, false, 4)]
    [InlineData(true, false, false, 3)]
    [InlineData(false, true, false, 3)]
    [InlineData(false, false, true, 3)]
    [InlineData(true, true, false, 2)]
    [InlineData(true, false, true, 2)]
    [InlineData(false, true, true, 2)]
    [InlineData(true, true, true, 1)]
    public async Task GetAll_ShouldApplyEveryCombinationOfFilters(bool title, bool from, bool to, int count)
    {
        var events = new[]
        {
            CreateEvent("dotnet early", 0), CreateEvent("dotnet middle", 1),
            CreateEvent("java middle", 2), CreateEvent("dotnet late", 3)
        };
        await SeedAsync(events);
        await using var scope = Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var result = await repository.GetAllAsync(title ? "DOTNET" : null,
            from ? Start.AddDays(1) : null, to ? Start.AddDays(2).AddHours(1) : null);

        Assert.Equal(count, result.TotalItems);
        Assert.Equal(count, result.Items.Count);
        var expected = events.Where(e => (!title || e.Title.Contains("dotnet")) &&
            (!from || e.StartAt >= Start.AddDays(1)) && (!to || e.EndAt <= Start.AddDays(2).AddHours(1)));
        Assert.Equal(expected.Select(e => e.Id), result.Items.Select(e => e.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAll_ShouldIgnoreEmptyTitle(string? title)
    {
        await SeedAsync(CreateEvent("First"), CreateEvent("Second", 1));
        await using var scope = Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IEventRepository>().GetAllAsync(title, null, null);
        Assert.Equal(2, result.TotalItems);
    }

    [Theory]
    [InlineData(1, 3, 0, 3)]
    [InlineData(2, 3, 3, 3)]
    [InlineData(3, 3, 6, 1)]
    [InlineData(4, 3, 7, 0)]
    public async Task GetAll_ShouldReturnStablePagesAndTotalBeforePagination(int page, int size, int skip, int count)
    {
        var events = Enumerable.Range(0, 7).Select(i => CreateEvent($"Event {i}", i)).ToArray();
        await SeedAsync(events.Reverse().ToArray());
        await using var scope = Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IEventRepository>()
            .GetAllAsync("event", Start, Start.AddDays(6).AddHours(1), page, size);
        Assert.Equal(7, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(page, result.Page);
        Assert.Equal(size, result.PageSize);
        Assert.Equal(events.Skip(skip).Take(count).Select(e => e.Id), result.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task GetAll_WithNoMatches_ShouldReturnEmptyPage()
    {
        await SeedAsync(CreateEvent());
        await using var scope = Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IEventRepository>().GetAllAsync("missing", null, null);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task RemoveAndSave_ShouldCascadeBookingsInDatabase()
    {
        var @event = CreateEvent();
        var booking = Booking.Create(@event);
        await using (var scope = Services.CreateAsyncScope())
        {
            var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            bookings.Add(booking);
            await bookings.SaveChangesAsync();
        }

        // Новый scope не загружает брони: каскад должен выполнить сам PostgreSQL.
        await using (var scope = Services.CreateAsyncScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            events.Remove((await events.GetByIdAsync(@event.Id))!);
            await events.SaveChangesAsync();
        }

        await using var reader = Services.CreateAsyncScope();
        Assert.Null(await reader.ServiceProvider.GetRequiredService<IEventRepository>().GetByIdAsync(@event.Id));
        Assert.Null(await reader.ServiceProvider.GetRequiredService<IBookingRepository>().GetByIdAsync(booking.Id));
    }

    [Theory]
    [InlineData("title", "23502")]
    [InlineData("longTitle", "22001")]
    [InlineData("longDescription", "22001")]
    public async Task Save_ShouldEnforceDatabaseStringConstraints(string field, string sqlState)
    {
        var @event = CreateEvent();
        if (field == "title") @event.Title = null!;
        if (field == "longTitle") @event.Title = new string('x', 501);
        if (field == "longDescription") @event.Description = new string('x', 4001);
        await using var scope = Services.CreateAsyncScope();
        var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        events.Add(@event);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => events.SaveChangesAsync());
        Assert.Equal(sqlState, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task Migrate_ShouldCreateExpectedSchemaAndBeRepeatable()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        await context.Database.MigrateAsync();
        var tables = await context.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();
        Assert.Contains("events", tables);
        Assert.Contains("bookings", tables);
        var foreignKeys = await context.Database.SqlQueryRaw<string>(
            "SELECT pg_get_constraintdef(oid) AS \"Value\" FROM pg_constraint WHERE conrelid = 'public.bookings'::regclass AND contype = 'f'")
            .ToListAsync();
        Assert.Equal("FOREIGN KEY (event_id) REFERENCES events(id) ON DELETE CASCADE", Assert.Single(foreignKeys));
    }
}
