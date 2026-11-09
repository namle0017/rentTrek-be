using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace RenTrek.DAL.Extensions;

/// <summary>
/// Sắp xếp động theo whitelist và phân trang dùng chung cho các repository.
/// </summary>
internal static class QueryPaging
{
    /// <summary>
    /// Sắp xếp theo property <paramref name="sortBy"/> (không phân biệt hoa thường) nếu nằm trong whitelist,
    /// ngược lại theo <paramref name="defaultSort"/>; luôn ThenBy theo khoá để phân trang ổn định.
    /// </summary>
    public static IQueryable<T> SortBy<T>(this IQueryable<T> query, string? sortBy, bool isDescending,
        string defaultSort, params string[] allowedSorts)
    {
        var type = typeof(T);
        var property = type.GetProperties().FirstOrDefault(x =>
                x.Name.Equals(sortBy, StringComparison.OrdinalIgnoreCase)
                && (x.Name == defaultSort || allowedSorts.Contains(x.Name)))
            ?? type.GetProperty(defaultSort)!;
        var key = type.GetProperty("Id") ?? type.GetProperty(type.Name + "Id")
            ?? type.GetProperties().First(x => x.Name.EndsWith("Id", StringComparison.Ordinal));
        var sorted = Order(query, property, isDescending ? "OrderByDescending" : "OrderBy");
        return Order(sorted, key, "ThenBy");
    }

    /// <summary>Đếm trên entity (chưa chiếu) rồi lấy một trang đã chiếu. Gọi sau khi đã lọc và sắp xếp.</summary>
    public static async Task<(IReadOnlyList<TResult> Items, int TotalCount)> ToPageAsync<T, TResult>(
        this IQueryable<T> query, int pageNumber, int pageSize, Expression<Func<T, TResult>> projection,
        CancellationToken cancellationToken)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Max(1, pageSize);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(projection).ToListAsync(cancellationToken);
        return (items, total);
    }

    private static IQueryable<T> Order<T>(IQueryable<T> query, PropertyInfo property, string method)
    {
        var parameter = Expression.Parameter(typeof(T), "row");
        var selector = Expression.Lambda(Expression.Property(parameter, property), parameter);
        return query.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), method,
            [typeof(T), property.PropertyType], query.Expression, Expression.Quote(selector)));
    }
}
