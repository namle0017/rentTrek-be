namespace RenTrek.BLL.DTOs.Admin.Requests;

public sealed record CreateStaffRequest(string Email, string FullName, string? Phone, string Role, string Password, DateOnly? CareerStartDate);
