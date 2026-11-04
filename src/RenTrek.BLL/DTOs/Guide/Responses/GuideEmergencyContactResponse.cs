namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Thông tin người liên hệ khẩn cấp (Emergency Contact) tại màn G03 (24.3).
/// </summary>
public sealed class GuideEmergencyContactResponse
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
}
