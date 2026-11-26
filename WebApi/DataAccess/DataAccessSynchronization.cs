namespace WebApi.DataAccess;

internal static class DataAccessSynchronization
{
    internal static readonly SemaphoreSlim WriteSemaphore = new(1, 1);
}
