using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.Admin.Requests;

public sealed class AdminQueryRequest : PagingRequest
{
    public string? Role { get; set; }
    public string? Status { get; set; }
    public string? CategoryType { get; set; }
    public string? OrderType { get; set; }
    public string? PaymentStatus { get; set; }
    public string? Tab { get; set; }
    public string? AppliesTo { get; set; }
    public string? PostType { get; set; }
    public int? Rating { get; set; }
    public Guid? TourId { get; set; }
    public Guid? GuideUserId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Action { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string GroupBy { get; set; } = "month";
}
