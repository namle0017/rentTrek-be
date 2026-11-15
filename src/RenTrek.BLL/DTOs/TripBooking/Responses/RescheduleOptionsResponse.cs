namespace RenTrek.BLL.DTOs.TripBooking.Responses;

/// <summary>Điều kiện đổi lịch của booking và các chuyến có thể đổi sang.</summary>
public sealed class RescheduleOptionsResponse
{
    public bool CanReschedule { get; set; }
    public string? BlockedReason { get; set; }
    /// <summary>Chuyến bị công ty huỷ: chuyển miễn phí, không trả thêm, không tính lượt, không giới hạn giờ.</summary>
    public bool CompanyCancelled { get; set; }
    public decimal Fee { get; set; }
    public int RemainingTimes { get; set; }
    public DateTimeOffset? Deadline { get; set; }
    public List<TripOption> Trips { get; set; } = [];
    public RescheduleResultResponse? Pending { get; set; }

    public sealed class TripOption
    {
        public Guid TripId { get; set; }
        public DateTimeOffset StartAt { get; set; }
        public DateTimeOffset EndAt { get; set; }
        public decimal PricePerPerson { get; set; }
        public int AvailableSlots { get; set; }
        /// <summary>(giá mới − giá cũ) × số người + phí; dương = trả thêm, âm = được hoàn.</summary>
        public decimal PriceDiff { get; set; }
    }
}
