using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.CustomerReport.Requests;
using RenTrek.BLL.DTOs.CustomerReport.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <inheritdoc cref="ICustomerReportService"/>
public sealed class CustomerReportService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IFileStorageService fileStorage,
    INotificationService notificationService,
    TimeProvider timeProvider) : ICustomerReportService
{
    public const long MaxPhotoBytes = 5 * 1024 * 1024;
    private const int MaxPhotos = 5;
    private const int MaxPageSize = 100;
    private const int MinDescriptionLength = 10;
    private const int MaxDescriptionLength = 2000;
    private const int MaxAdminNoteLength = 1000;
    private const string StorageFolder = "customer-reports";

    // Đơn chưa thanh toán / đã huỷ / hết hạn thì chưa có gì để báo cáo
    private static readonly HashSet<string> ReportableOrderStatuses =
    [
        OrderStatuses.Confirmed,
        OrderStatuses.InFulfillment,
        OrderStatuses.AwaitingSettlement,
        OrderStatuses.Completed
    ];

    public async Task<CustomerReportImageResponse> UploadImageAsync(
        Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        var url = await fileStorage.SaveImageAsync(content, fileName, contentType, length, StorageFolder, MaxPhotoBytes, cancellationToken);
        return new CustomerReportImageResponse(url);
    }

    public async Task<CustomerReportResponse> CreateAsync(CreateCustomerReportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = currentUser.RequiredUserId;
        var errors = Validate(request);
        if (errors.Count > 0)
            throw new ValidationException(errors);

        var order = await unitOfWork.CustomerReports.GetOrderForReportAsync(request.OrderId, cancellationToken);
        var expectedOrderType = request.SourceType == CustomerReportSourceTypes.Rental ? OrderTypes.Rental : OrderTypes.TripBooking;
        if (order is null || order.OrderType != expectedOrderType || !CanReport(order, userId))
            throw new NotFoundException("Không tìm thấy đơn", ErrorCodes.OrderNotFound);
        if (!ReportableOrderStatuses.Contains(order.Status))
            throw new ConflictException("Chỉ gửi báo cáo cho đơn đã thanh toán, đang diễn ra hoặc đã hoàn tất");

        var now = timeProvider.GetUtcNow();
        var report = new CustomerReport
        {
            CustomerReportId = Guid.NewGuid(),
            ReporterUserId = userId,
            OrderId = order.OrderId,
            SourceType = request.SourceType,
            Category = request.Category,
            SubjectLabel = Clean(request.SubjectLabel),
            Description = request.Description.Trim(),
            PreferredAction = Clean(request.PreferredAction),
            ContactPhone = Clean(request.ContactPhone),
            PhotoUrls = request.PhotoUrls.Select(url => url.Trim()).ToList(),
            Status = CustomerReportStatuses.Open,
            CreatedAt = now,
            UpdatedAt = now
        };
        await unitOfWork.CustomerReports.AddAsync(report, cancellationToken);

        var source = report.SourceType == CustomerReportSourceTypes.Rental ? "đơn thuê" : "chuyến đi";
        var staffIds = await unitOfWork.RefundCases.GetActiveUserIdsByRolesAsync([RoleNames.Admin, RoleNames.Staff], cancellationToken);
        foreach (var staffId in staffIds)
        {
            await notificationService.QueueAsync(
                staffId,
                NotificationTypes.CustomerReportSubmitted,
                $"Báo cáo mới từ khách về {source}",
                $"Đơn {order.OrderCode}: {Shorten(report.Description, 120)}",
                "CustomerReport",
                report.CustomerReportId,
                $"CustomerReportSubmitted:{report.CustomerReportId}",
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(report.CustomerReportId, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerReportResponse>> GetMineAsync(Guid? orderId, CancellationToken cancellationToken = default)
    {
        var reports = await unitOfWork.CustomerReports.GetByReporterAsync(currentUser.RequiredUserId, orderId, cancellationToken);
        return reports.Select(Map).ToList();
    }

    public async Task<PagedResponse<CustomerReportResponse>> SearchAsync(CustomerReportQueryRequest query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var sourceType = CustomerReportSourceTypes.All.Contains(query.SourceType ?? string.Empty) ? query.SourceType : null;
        var status = CustomerReportStatuses.All.Contains(query.Status ?? string.Empty) ? query.Status : null;
        var keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();

        var (items, total) = await unitOfWork.CustomerReports.SearchAsync(sourceType, status, keyword, pageNumber, pageSize, cancellationToken);
        return new PagedResponse<CustomerReportResponse>
        {
            Items = items.Select(Map).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    public async Task<CustomerReportSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var counts = await unitOfWork.CustomerReports.CountByStatusAsync(cancellationToken);
        return new CustomerReportSummaryResponse
        {
            Open = counts.GetValueOrDefault(CustomerReportStatuses.Open),
            InProgress = counts.GetValueOrDefault(CustomerReportStatuses.InProgress),
            Resolved = counts.GetValueOrDefault(CustomerReportStatuses.Resolved),
            Rejected = counts.GetValueOrDefault(CustomerReportStatuses.Rejected)
        };
    }

    public async Task<CustomerReportResponse> GetAsync(Guid customerReportId, CancellationToken cancellationToken = default)
    {
        var report = await unitOfWork.CustomerReports.GetDetailAsync(customerReportId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy báo cáo", ErrorCodes.CustomerReportNotFound);
        return Map(report);
    }

    public async Task<CustomerReportResponse> UpdateStatusAsync(
        Guid customerReportId, UpdateCustomerReportStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var status = request.Status?.Trim() ?? string.Empty;
        var note = Clean(request.AdminNote);
        if (status is not (CustomerReportStatuses.InProgress or CustomerReportStatuses.Resolved or CustomerReportStatuses.Rejected))
            throw new ValidationException("status", "Trạng thái chỉ nhận InProgress, Resolved hoặc Rejected");
        if (CustomerReportStatuses.IsClosed(status) && note is null)
            throw new ValidationException("adminNote", "Vui lòng nhập phản hồi cho khách khi đóng báo cáo");
        if (note?.Length > MaxAdminNoteLength)
            throw new ValidationException("adminNote", $"Phản hồi tối đa {MaxAdminNoteLength} ký tự");

        var report = await unitOfWork.CustomerReports.GetDetailAsync(customerReportId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy báo cáo", ErrorCodes.CustomerReportNotFound);
        if (CustomerReportStatuses.IsClosed(report.Status))
            throw new ConflictException("Báo cáo đã đóng, không thể cập nhật");
        if (report.Status == status && (note is null || note == report.AdminNote))
            return Map(report);

        var now = timeProvider.GetUtcNow();
        report.Status = status;
        report.AdminNote = note ?? report.AdminNote;
        report.HandledByUserId = currentUser.RequiredUserId;
        report.HandledAt = now;
        report.UpdatedAt = now;

        var (title, verb) = status switch
        {
            CustomerReportStatuses.InProgress => ("Báo cáo của bạn đang được xử lý", "đang được xử lý"),
            CustomerReportStatuses.Resolved => ("Báo cáo của bạn đã được xử lý", "đã được xử lý"),
            _ => ("Báo cáo của bạn đã bị từ chối", "đã bị từ chối")
        };
        var content = $"Báo cáo về đơn {report.Order.OrderCode} {verb}."
                      + (report.AdminNote is null ? string.Empty : $" Phản hồi: {Shorten(report.AdminNote, 200)}");
        await notificationService.QueueAsync(
            report.ReporterUserId,
            NotificationTypes.CustomerReportUpdated,
            title,
            content,
            "CustomerReport",
            report.CustomerReportId,
            $"CustomerReportUpdated:{report.CustomerReportId}:{status}",
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(report);
    }

    /// <summary>Đơn thuê: chỉ chủ đơn. Booking chuyến: chủ booking hoặc người tham gia có tài khoản liên kết.</summary>
    private static bool CanReport(Order order, Guid userId)
        => order.CustomerUserId == userId
           || (order.OrderType == OrderTypes.TripBooking
               && order.TripBookingOrder?.Participants.Any(p => p.LinkedUserId == userId && !p.IsDeleted) == true);

    private static Dictionary<string, string[]> Validate(CreateCustomerReportRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (!CustomerReportSourceTypes.All.Contains(request.SourceType ?? string.Empty))
        {
            errors["sourceType"] = ["Loại báo cáo chỉ nhận Rental hoặc Trip"];
            return errors;
        }

        if (request.OrderId == Guid.Empty)
            errors["orderId"] = ["Vui lòng chọn đơn cần báo cáo"];
        if (!CustomerReportCategories.For(request.SourceType).Contains(request.Category ?? string.Empty))
            errors["category"] = ["Vui lòng chọn loại vấn đề hợp lệ"];

        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length < MinDescriptionLength)
            errors["description"] = [$"Mô tả tối thiểu {MinDescriptionLength} ký tự"];
        else if (description.Length > MaxDescriptionLength)
            errors["description"] = [$"Mô tả tối đa {MaxDescriptionLength} ký tự"];

        if (request.SubjectLabel?.Trim().Length > 255)
            errors["subjectLabel"] = ["Tên thiết bị / hoạt động tối đa 255 ký tự"];
        if (Clean(request.PreferredAction) is { } action && !CustomerReportPreferredActions.All.Contains(action))
            errors["preferredAction"] = ["Mong muốn xử lý không hợp lệ"];
        if (Clean(request.ContactPhone) is { } phone && !IsPhone(phone))
            errors["contactPhone"] = ["Số điện thoại không hợp lệ"];

        var photos = request.PhotoUrls ?? [];
        if (photos.Count > MaxPhotos)
            errors["photoUrls"] = [$"Tối đa {MaxPhotos} ảnh"];
        else if (photos.Any(url => !IsImageUrl(url)))
            errors["photoUrls"] = ["Link ảnh không hợp lệ"];

        return errors;
    }

    private static bool IsPhone(string phone)
        => phone.Length is >= 9 and <= 15 && phone.TrimStart('+').All(char.IsDigit);

    // Ảnh do FileStorageService trả về: link tương đối /uploads/... (lưu local) hoặc https (Supabase)
    private static bool IsImageUrl(string? url)
    {
        var value = url?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > 1000)
            return false;
        if (value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal))
            return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Shorten(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";

    private static CustomerReportResponse Map(CustomerReport report)
    {
        var trip = report.Order?.TripBookingOrder?.Trip;
        return new CustomerReportResponse
        {
            CustomerReportId = report.CustomerReportId,
            SourceType = report.SourceType,
            OrderId = report.OrderId,
            OrderCode = report.Order?.OrderCode ?? string.Empty,
            OrderStatus = report.Order?.Status ?? string.Empty,
            TripId = trip?.TripId,
            TourName = trip?.Tour?.Name,
            TripStartAt = trip?.StartAt,
            Category = report.Category,
            SubjectLabel = report.SubjectLabel,
            Description = report.Description,
            PreferredAction = report.PreferredAction,
            ContactPhone = report.ContactPhone,
            PhotoUrls = report.PhotoUrls,
            Status = report.Status,
            AdminNote = report.AdminNote,
            HandledByName = report.HandledByUser?.FullName,
            HandledAt = report.HandledAt,
            ReporterUserId = report.ReporterUserId,
            ReporterName = report.ReporterUser?.FullName ?? string.Empty,
            ReporterEmail = report.ReporterUser?.Email,
            ReporterPhone = report.ReporterUser?.Phone,
            CreatedAt = report.CreatedAt,
            UpdatedAt = report.UpdatedAt
        };
    }
}
