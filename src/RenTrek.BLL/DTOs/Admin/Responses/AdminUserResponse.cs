namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminUserResponse(Guid UserId, string? UserCode, string Email, string FullName, string? Phone, string Role, string Status, string? AvatarUrl, DateTime? LastLoginAt, DateTime CreatedAt);
