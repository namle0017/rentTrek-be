namespace RenTrek.BLL.DTOs.Tour.Responses;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ tour image.
/// </summary>
public class TourImageResponse
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}
