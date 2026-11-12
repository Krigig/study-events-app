using WebApi.Interfaces;

namespace WebApi.Services;

/// <summary>
/// Периодически обрабатывает созданные брони, ожидающие подтверждения.
/// </summary>
public sealed class BookingProcessingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    private readonly IBookingService _bookingService;

    public BookingProcessingBackgroundService(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var pendingBookings = await _bookingService.GetPendingBookingsAsync();

                foreach (var booking in pendingBookings)
                {
                    await Task.Delay(ProcessingDelay, stoppingToken);
                    await _bookingService.ConfirmBookingAsync(booking.Id);
                }

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Штатная остановка приложения.
        }
    }
}
