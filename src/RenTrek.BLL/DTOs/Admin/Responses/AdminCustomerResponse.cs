namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminCustomerResponse(Guid UserId, string? UserCode, string FullName, string Email, string? Phone);
