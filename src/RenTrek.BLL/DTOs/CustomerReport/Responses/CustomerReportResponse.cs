namespace RenTrek.BLL.DTOs.CustomerReport.Responses;

/// <summary>Báo cáo khách gửi về đơn thuê / chuyến đi.</summary>
public sealed class CustomerReportResponse
{
    public Guid CustomerReportId { get; set; }

    /// <summary>Rental | Trip.</summary>
    public string SourceType { get; set; } = string.Empty;

    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;

    /// <summary>Chỉ có với báo cáo chuyến đi.</summary>
    public Guid? TripId { get; set; }
    public string? TourName { get; set; }
    public DateTimeOffset? TripStartAt { get; set; }

    public string Category { get; set; } = string.Empty;
    public string? SubjectLabel { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? PreferredAction { get; set; }
    public string? ContactPhone { get; set; }
    public IReadOnlyList<string> PhotoUrls { get; set; } = [];

    /// <summary>Open | InProgress | Resolved | Rejected.</summary>
    public string Status { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public string? HandledByName { get; set; }
    public DateTimeOffset? HandledAt { get; set; }

    public Guid ReporterUserId { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public string? ReporterEmail { get; set; }
    public string? ReporterPhone { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
