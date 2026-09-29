namespace WebApi.Models;

/// <summary>
/// Результат пагинации: элементы текущей страницы и метаданные о ней.
/// </summary>
/// <typeparam name="T">Тип элементов (например, EventResponse).</typeparam>
public class PaginatedResult<T>
{
    /// <summary>
    /// Элементы текущей страницы.
    /// </summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>
    /// Номер текущей страницы (начиная с 1).
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Количество элементов на текущей странице.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Общее количество элементов (по всем страницам, с учётом фильтров).
    /// </summary>
    public int TotalItems { get; init; }

    /// <summary>
    /// Общее количество страниц.
    /// </summary>
    public int TotalPages { get; init; }
}
