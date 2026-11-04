namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Phản hồi kiểm tra điều kiện sẵn sàng khởi hành trước giờ G (8.5).
/// </summary>
public sealed class TripReadinessChecklistResponse
{
    public bool IsReadyToDepart { get; set; }
    public TripReadinessChecklistDetailResponse Checklist { get; set; } = new();
}
