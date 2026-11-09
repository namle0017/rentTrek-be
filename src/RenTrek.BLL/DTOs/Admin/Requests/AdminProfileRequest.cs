namespace RenTrek.BLL.DTOs.Admin.Requests;

public sealed record AdminProfileRequest(string FullName, string? Phone, DateOnly? DateOfBirth, string? Address, DateOnly? CareerStartDate, string? Bio);
