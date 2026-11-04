namespace RenTrek.BLL.DTOs.Admin.Requests;

public sealed record ParticipantOverrideRequest(string FullName, string? Phone, DateOnly? DateOfBirth, string? EmergencyContactName, string? EmergencyContactPhone, string Reason);
