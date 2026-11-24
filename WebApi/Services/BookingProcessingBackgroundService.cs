using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

/// <summary>Параллельно обрабатывает брони, ожидающие подтверждения.</summary>
public sealed class BookingProcessingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    private readonly IBookingStore _bookingStore;
    private readonly InMemoryEventStore _eventStore;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

    public BookingProcessingBackgroundService(
        IBookingStore bookingStore,
        InMemoryEventStore eventStore,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _bookingStore = bookingStore;
        _eventStore = eventStore;
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
                    var pendingBookings = _bookingStore.GetPending().ToList();
                    var tasks = pendingBookings.Select(booking => ProcessBookingAsync(booking, stoppingToken));
                    await Task.WhenAll(tasks);
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

    private async Task ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
    {
        var semaphoreAcquired = false;
        try
        {
            // Задержки всех броней выполняются одновременно, до захвата семафора.
            await Task.Delay(ProcessingDelay, stoppingToken);
            await _processingSemaphore.WaitAsync(stoppingToken);
            semaphoreAcquired = true;
            stoppingToken.ThrowIfCancellationRequested();

            var current = _bookingStore.GetById(booking.Id);
            if (current is null || current.BookingStatus != BookingStatus.Pending)
            {
                return;
            }

            if (_eventStore.GetById(current.EventId) is null)
            {
                if (!_bookingStore.TryUpdate(current, current.Reject()))
                {
                    throw new InvalidOperationException($"Не удалось отклонить бронь {current.Id}.");
                }

                _logger.LogWarning("Бронь {BookingId} отклонена: событие {EventId} удалено.",
                    current.Id, current.EventId);
                return;
            }

            if (!_bookingStore.TryUpdate(current, current.Confirm()))
            {
                throw new InvalidOperationException($"Не удалось подтвердить бронь {current.Id}.");
            }

            _logger.LogInformation("Бронь {BookingId} подтверждена.", current.Id);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка обработки брони {BookingId}.", booking.Id);

            try
            {
                if (!semaphoreAcquired)
                {
                    await _processingSemaphore.WaitAsync(stoppingToken);
                    semaphoreAcquired = true;
                }

                var current = _bookingStore.GetById(booking.Id);
                if (current is not null && current.BookingStatus == BookingStatus.Pending &&
                    _bookingStore.TryUpdate(current, current.Reject()))
                {
                    // Если событие удалено, возвращать место уже некуда.
                    var @event = _eventStore.GetById(current.EventId);
                    if (@event is not null)
                    {
                        @event.ReleaseSeats();
                        _eventStore.Save(@event);
                    }

                    _logger.LogWarning("Бронь {BookingId} отклонена после ошибки.", current.Id);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception rejectionException)
            {
                _logger.LogError(rejectionException, "Не удалось отклонить бронь {BookingId}.", booking.Id);
            }
        }
        finally
        {
            if (semaphoreAcquired)
            {
                _processingSemaphore.Release();
            }
        }
    }
}
