using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Common;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Cung cấp các phép mở rộng dùng chung cho truy vấn phân trang.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Đếm tổng và lấy 1 trang dữ liệu. Gọi sau khi đã filter / sort (và projection sang DTO nếu có).
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PagingRequest paging,
        CancellationToken cancellationToken = default)
    {
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((paging.PageNumber - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            PageNumber = paging.PageNumber,
            PageSize = paging.PageSize,
            TotalItems = totalItems
        };
    }
}
