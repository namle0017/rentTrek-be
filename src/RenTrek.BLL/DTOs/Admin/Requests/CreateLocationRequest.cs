namespace RenTrek.BLL.DTOs.Admin.Requests;

public record CreateLocationRequest(string Name, string Address, decimal Latitude, decimal Longitude);
