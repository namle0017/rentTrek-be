namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminReadAnalyticsResponse(Guid PostId, int ParticipantsCount, int ReadCount, decimal ReadRate);
