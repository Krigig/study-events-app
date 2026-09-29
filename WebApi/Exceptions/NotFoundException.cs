namespace WebApi.Exceptions;

/// <summary>
/// Исключение "ресурс не найден" — маппится на 404 Not Found.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
