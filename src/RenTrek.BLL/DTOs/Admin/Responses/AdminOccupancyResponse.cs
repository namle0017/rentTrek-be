namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminOccupancyResponse(Guid TripId, string TourName, DateTimeOffset StartAt, int TotalSlots, int BookedSlots, decimal OccupancyRate);
