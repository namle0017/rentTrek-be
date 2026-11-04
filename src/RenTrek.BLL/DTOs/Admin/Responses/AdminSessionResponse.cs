namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminSessionResponse(Guid SessionId, Guid FamilyId, DateTime CreatedAt, DateTime ExpiresAt);
