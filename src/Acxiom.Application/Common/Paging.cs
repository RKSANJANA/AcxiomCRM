using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Application.Common;

public class ListFilter
{
    public string? Q { get; set; }
    public string? Owner { get; set; }

    /// <summary>Column key, prefix with "-" for descending (e.g. "-created").</summary>
    public string? Sort { get; set; }

    public int Page { get; set; } = 1;
    public int Size { get; set; } = 15;

    public string? Text => string.IsNullOrWhiteSpace(Q) ? null : Q.Trim().ToLower();
}

public class Paged<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int Size { get; init; }
    public int Total { get; init; }
    public int Pages => Size <= 0 ? 0 : (Total + Size - 1) / Size;
}

public static class QueryExtensions
{
    public static async Task<Paged<T>> PageAsync<T>(this IQueryable<T> query, ListFilter f)
    {
        var size = f.Size is >= 5 and <= 100 ? f.Size : 15;
        var total = await query.CountAsync();
        var page = Math.Clamp(f.Page, 1, Math.Max(1, (total + size - 1) / size));
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
        return new Paged<T> { Items = items, Page = page, Size = size, Total = total };
    }

    public static IQueryable<T> SortBy<T>(this IQueryable<T> query, string? sort,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> columns, string fallback)
    {
        var key = string.IsNullOrWhiteSpace(sort) ? fallback : sort;
        var desc = key.StartsWith('-');
        if (!columns.TryGetValue(key.TrimStart('-'), out var column))
        {
            desc = fallback.StartsWith('-');
            column = columns[fallback.TrimStart('-')];
        }
        return desc ? query.OrderByDescending(column) : query.OrderBy(column);
    }

    public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool condition, Expression<Func<T, bool>> predicate) =>
        condition ? query.Where(predicate) : query;
}
