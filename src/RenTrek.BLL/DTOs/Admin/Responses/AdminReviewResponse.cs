namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminReviewResponse(Guid ReviewId, int Rating, string? Comment, string CustomerFullName, string TourName, DateTimeOffset TripStartAt, string? GuideFullName, bool IsHidden, string? HiddenReason, DateTime CreatedAt);
