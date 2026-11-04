namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminKpisResponse(int WeeklyBookingsCount, decimal AverageOccupancyRate, decimal NetRevenueVnd, int ActiveRentedAssetsCount, int TotalAvailableAssetsCount);
