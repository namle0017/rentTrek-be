namespace RenTrek.BLL.DTOs.DevClock.Responses;

public sealed record DevClockResponse(bool IsSimulated, DateTimeOffset Now, DateTimeOffset RealNow, long OffsetSeconds);
