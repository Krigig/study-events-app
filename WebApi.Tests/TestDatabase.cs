using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using WebApi.DataAccess;
using WebApi.Interfaces;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Tests;

internal sealed class TestDatabase : IDisposable
{
    private readonly ConcurrentBag<IServiceScope> _scopes = new();
    private readonly ServiceProvider _provider;

    public TestDatabase(SaveChangesInterceptor? interceptor = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName);
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }
        });
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    public IServiceScopeFactory ScopeFactory => _provider.GetRequiredService<IServiceScopeFactory>();

    public IServiceScope CreateScope() => _provider.CreateScope();

    public AppDbContext CreateContext() => CreateTrackedScope().GetRequiredService<AppDbContext>();
    public IEventService CreateEventService() => CreateTrackedScope().GetRequiredService<IEventService>();
    public IBookingService CreateBookingService() => CreateTrackedScope().GetRequiredService<IBookingService>();

    private IServiceProvider CreateTrackedScope()
    {
        var scope = CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider;
    }

    public void Dispose()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        _provider.Dispose();
    }
}

internal sealed class BookingSaveFailureInterceptor : SaveChangesInterceptor
{
    public bool FailCreation { get; set; }
    public Guid? FailConfirmationFor { get; set; }
    private int _confirmationFailed;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var entries = eventData.Context!.ChangeTracker.Entries<Booking>().ToList();
        if (FailCreation && entries.Any(e => e.State == EntityState.Added) ||
            entries.Any(e => e.State == EntityState.Modified && e.Entity.Id == FailConfirmationFor &&
                e.Entity.BookingStatus == BookingStatus.Confirmed) &&
            Interlocked.Exchange(ref _confirmationFailed, 1) == 0)
        {
            throw new InvalidOperationException("Simulated database failure");
        }

        return ValueTask.FromResult(result);
    }
}
