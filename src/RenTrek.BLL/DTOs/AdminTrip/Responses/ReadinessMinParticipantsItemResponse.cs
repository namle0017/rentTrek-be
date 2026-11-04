namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Tiêu chí 1: Số khách tham gia đạt mức tối thiểu (8.5).
/// </summary>
public sealed class ReadinessMinParticipantsItemResponse
{
    public bool Passed { get; set; }
    public int Current { get; set; }
    public int Required { get; set; }
}
