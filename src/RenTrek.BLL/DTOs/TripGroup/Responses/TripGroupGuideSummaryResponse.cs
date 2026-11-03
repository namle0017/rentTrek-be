namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Thông tin tóm tắt của hướng dẫn viên phụ trách TripGroup.
/// </summary>
public sealed class TripGroupGuideSummaryResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
