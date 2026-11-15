namespace RenTrek.DAL.Entities;

/// <summary>
/// Ảnh bằng chứng khi giao, nhận lại, hư hỏng hoặc khách báo lỗi.
/// </summary>
public class RentalEvidencePhoto
{
    public Guid RentalEvidencePhotoId { get; set; }
    public Guid RentalOrderId { get; set; }
    public Guid? AssetAssignmentId { get; set; }
    public string Stage { get; set; } = null!;
    public string ImageUrl { get; set; } = null!;
    public string? Note { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public RentalOrder RentalOrder { get; set; } = null!;
    public AssetAssignment? AssetAssignment { get; set; }
    public Account UploadedByUser { get; set; } = null!;
}
