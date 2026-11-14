namespace RenTrek.BLL.DTOs.Users.Responses;

/// <summary>
/// Một phiên đăng nhập (một family refresh token) của người dùng hiện tại.
/// CreatedAt là lúc đăng nhập, LastActiveAt là lần xoay token gần nhất.
/// </summary>
public sealed record MySessionResponse(Guid FamilyId, DateTime CreatedAt, DateTime LastActiveAt, DateTime ExpiresAt, bool IsCurrent);
