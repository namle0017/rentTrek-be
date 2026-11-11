namespace RenTrek.BLL.DTOs.Post.Responses;

public sealed record PostTourResponse(Guid TourId, string Name, int DurationDays, int DurationNights, decimal BasePrice);
