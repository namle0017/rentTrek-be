namespace RenTrek.BLL.DTOs.Admin.Requests;

public sealed record LocationWriteRequest(string Name, string Address, decimal Latitude, decimal Longitude);
