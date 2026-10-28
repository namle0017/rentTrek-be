using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.DTOs.Order;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ order.
/// </summary>
public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<MyOrderResponse>> GetMyOrdersAsync(Guid userId, MyOrderQueryRequest request, CancellationToken cancellationToken = default)
    {
        var filter = MyOrderMapper.ToFilter(request);
        var (items, totalCount) = await _unitOfWork.Orders.GetMyOrdersPagedAsync(userId, filter, cancellationToken);

        return new PagedResult<MyOrderResponse>
        {
            Items = items.Select(MyOrderMapper.ToResponse).ToList(),
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
            TotalItems = totalCount
        };
    }
}
