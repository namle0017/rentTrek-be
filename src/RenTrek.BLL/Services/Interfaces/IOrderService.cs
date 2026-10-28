using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.DTOs.Order;

namespace RenTrek.BLL.Services.Interfaces;

public interface IOrderService
{
    Task<PagedResult<MyOrderResponse>> GetMyOrdersAsync(Guid userId, MyOrderQueryRequest request, CancellationToken cancellationToken = default);
}
