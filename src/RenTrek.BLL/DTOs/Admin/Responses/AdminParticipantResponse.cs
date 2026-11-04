namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminParticipantResponse(Guid ParticipantId, string FullName, string Email, string? Phone, DateOnly? DateOfBirth, string? EmergencyContactName, string? EmergencyContactPhone, string Status);
