namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminLocationResponse(Guid LocationId, string Name, string Address, decimal Latitude, decimal Longitude, int ToursCount, DateTime CreatedAt);
