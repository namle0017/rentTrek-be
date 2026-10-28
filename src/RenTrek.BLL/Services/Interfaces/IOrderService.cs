using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Order.Requests;
using RenTrek.BLL.DTOs.Order.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface IOrderService
{
    Task<PagedResponse<MyOrderResponse>> GetMyOrdersAsync(Guid userId, MyOrderQueryRequest request, CancellationToken cancellationToken = default);
}
