namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminUserDetailResponse(AdminUserResponse User, DateOnly? DateOfBirth, string? Address, DateOnly? CareerStartDate, string? Bio, int OrdersCount, int ManagedTripsCount, int GuidedTripsCount);
