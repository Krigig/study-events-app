using Microsoft.EntityFrameworkCore;
using WebApi.DataAccess;
using WebApi.Models;

namespace WebApi.Services;

/// <summary>Параллельно обрабатывает брони, ожидающие подтверждения.</summary>
public sealed class BookingProcessingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    public BookingProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    List<Guid> pendingIds;
                    await using (var scope = _scopeFactory.CreateAsyncScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        pendingIds = await context.Bookings.AsNoTracking()
                            .Where(b => b.BookingStatus == BookingStatus.Pending)
                            .Select(b => b.Id).ToListAsync(stoppingToken);
                    }

                    await Task.WhenAll(pendingIds.Select(id => ProcessBookingAsync(id, stoppingToken)));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Ошибка получения или обработки ожидающих броней.");
                }

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Штатная остановка приложения.
        }
    }

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        await Task.Delay(ProcessingDelay, stoppingToken);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await context.Bookings.FindAsync([bookingId], stoppingToken);
            if (booking is null || booking.BookingStatus != BookingStatus.Pending)
            {
                return;
            }

            var eventExists = await context.Events.AnyAsync(e => e.Id == booking.EventId, stoppingToken);
            context.Entry(booking).CurrentValues.SetValues(eventExists ? booking.Confirm() : booking.Reject());
            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Бронь {BookingId} обработана: {Status}.", bookingId, booking.BookingStatus);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка обработки брони {BookingId}.", bookingId);
            try
            {
                // Возврат места синхронизируем с созданием броней для того же события.
                await DataAccessSynchronization.WriteSemaphore.WaitAsync(stoppingToken);
                try
                {
                    // После ошибки используем новый контекст без несохранённых изменений.
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var booking = await context.Bookings.FindAsync([bookingId], stoppingToken);
                    if (booking is not null && booking.BookingStatus == BookingStatus.Pending)
                    {
                        context.Entry(booking).CurrentValues.SetValues(booking.Reject());
                        var @event = await context.Events.FindAsync([booking.EventId], stoppingToken);
                        @event?.ReleaseSeats();
                        await context.SaveChangesAsync(stoppingToken);
                        _logger.LogWarning("Бронь {BookingId} отклонена после ошибки.", bookingId);
                    }
                }
                finally
                {
                    DataAccessSynchronization.WriteSemaphore.Release();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception rejectionException)
            {
                _logger.LogError(rejectionException, "Не удалось отклонить бронь {BookingId}.", bookingId);
            }
        }
    }
}
