namespace RenTrek.BLL.DTOs.RentalOps.Responses;

public sealed class AdminRentalCustomerResponse
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
