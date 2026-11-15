namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>Ảnh bằng chứng giao / nhận / hư hỏng.</summary>
public class RentalEvidencePhotoResponse
{
    public Guid Id { get; set; }
    public Guid? AssetAssignmentId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
