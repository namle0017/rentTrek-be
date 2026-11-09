namespace RenTrek.BLL.DTOs.RentalOps.Responses;

public sealed class AdminRentalAssignmentResponse
{
    public Guid AssignmentId { get; set; }
    public Guid RentalOrderItemId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AssetStatus { get; set; } = string.Empty;

    /// <summary>Thời điểm giao thực tế khi Status = Issued / Returned / LostOrException.</summary>
    public DateTime? IssuedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }
    public string? ReturnCondition { get; set; }
}
