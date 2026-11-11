namespace RenTrek.BLL.DTOs.Admin.Requests;

/// <summary>Cùng dữ liệu với <see cref="CreateLocationRequest"/>.</summary>
public sealed record UpdateLocationRequest(string Name, string Address, decimal Latitude, decimal Longitude) : CreateLocationRequest(Name, Address, Latitude, Longitude);
