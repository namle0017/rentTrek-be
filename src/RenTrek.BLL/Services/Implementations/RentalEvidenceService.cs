using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.RentalOrder.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <inheritdoc cref="IRentalEvidenceService"/>
public sealed class RentalEvidenceService(
    IUnitOfWork unitOfWork,
    IFileStorageService fileStorage,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IRentalEvidenceService
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private const int MaxNoteLength = 500;
    private const string StorageFolder = "rental-evidence";

    public async Task<RentalEvidencePhotoResponse> UploadStaffAsync(
        Guid orderId, string stage, Guid? assignmentId, string? note,
        Stream content, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default)
    {
        if (!RentalEvidenceStages.StaffStages.Contains(stage))
            throw new BadRequestException("Giai đoạn ảnh chỉ nhận Handover, Return hoặc Damage", ErrorCodes.ValidationError);

        var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);
        EnsureAssignment(order, assignmentId, stage);
        return await SaveAsync(order, stage, assignmentId, note, currentUser.RequiredUserId, content, fileName, contentType, length, cancellationToken);
    }

    public async Task<RentalEvidencePhotoResponse> UploadCustomerAsync(
        Guid userId, Guid orderId, Guid? assignmentId, string? note,
        Stream content, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default)
    {
        var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken);
        if (order is null || order.CustomerUserId != userId)
            throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);
        if (order.Status != OrderStatuses.InFulfillment)
            throw new ConflictException("Chỉ gửi ảnh báo lỗi khi đơn đang thuê");
        EnsureAssignment(order, assignmentId, RentalEvidenceStages.CustomerIssue);
        return await SaveAsync(order, RentalEvidenceStages.CustomerIssue, assignmentId, note, userId, content, fileName, contentType, length, cancellationToken);
    }

    public async Task DeleteAsync(Guid orderId, Guid photoId, CancellationToken cancellationToken = default)
    {
        var photo = await unitOfWork.RentalEvidencePhotos.GetByIdAsync(photoId, cancellationToken);
        if (photo is null || photo.RentalOrderId != orderId || photo.IsDeleted)
            throw new NotFoundException("Không tìm thấy ảnh", ErrorCodes.NotFound);

        var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);
        var assignment = photo.AssetAssignmentId is { } id
            ? RentalOrderOps.Assignments(order).FirstOrDefault(a => a.AssetAssignmentId == id)
            : null;

        // Ảnh đã làm bằng chứng cho bước đã hoàn tất thì không được xoá
        var stepDone = photo.Stage switch
        {
            RentalEvidenceStages.Handover => assignment is not null && assignment.Status != AssignmentStatuses.Reserved,
            RentalEvidenceStages.Return => assignment is not null && assignment.Status != AssignmentStatuses.Issued,
            _ => order.Status is not OrderStatuses.InFulfillment
        };
        if (stepDone)
            throw new ConflictException("Ảnh đã dùng làm bằng chứng cho bước đã hoàn tất, không thể xoá");

        photo.IsDeleted = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RentalEvidencePhotoResponse>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => (await unitOfWork.RentalEvidencePhotos.GetByOrderAsync(orderId, cancellationToken)).Select(Map).ToList();

    private static void EnsureAssignment(Order order, Guid? assignmentId, string stage)
    {
        if (assignmentId is null)
        {
            if (stage is RentalEvidenceStages.Handover or RentalEvidenceStages.Return)
                throw new BadRequestException("Ảnh giao / nhận phải gắn với từng thiết bị", ErrorCodes.ValidationError);
            return;
        }

        var assignment = RentalOrderOps.Assignments(order).FirstOrDefault(a => a.AssetAssignmentId == assignmentId)
            ?? throw new NotFoundException("Thiết bị không thuộc đơn thuê này", ErrorCodes.ItemNotFound);
        var ok = stage switch
        {
            RentalEvidenceStages.Handover => assignment.Status == AssignmentStatuses.Reserved,
            RentalEvidenceStages.Return => assignment.Status == AssignmentStatuses.Issued,
            _ => assignment.Status is AssignmentStatuses.Issued or AssignmentStatuses.Returned or AssignmentStatuses.LostOrException
        };
        if (!ok)
            throw new ConflictException($"Thiết bị {assignment.EquipmentAsset.AssetCode} không ở bước phù hợp để chụp ảnh {stage}");
    }

    private async Task<RentalEvidencePhotoResponse> SaveAsync(
        Order order, string stage, Guid? assignmentId, string? note, Guid uploaderId,
        Stream content, string fileName, string contentType, long length,
        CancellationToken cancellationToken)
    {
        if (note?.Trim().Length > MaxNoteLength)
            throw new BadRequestException($"Ghi chú tối đa {MaxNoteLength} ký tự", ErrorCodes.ValidationError);

        var url = await fileStorage.SaveImageAsync(content, fileName, contentType, length, StorageFolder, MaxPhotoBytes, cancellationToken);
        var photo = new RentalEvidencePhoto
        {
            RentalEvidencePhotoId = Guid.NewGuid(),
            RentalOrderId = order.OrderId,
            AssetAssignmentId = assignmentId,
            Stage = stage,
            ImageUrl = url,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            UploadedByUserId = uploaderId,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        await unitOfWork.RentalEvidencePhotos.AddAsync(photo, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(photo);
    }

    private static RentalEvidencePhotoResponse Map(RentalEvidencePhoto photo) => new()
    {
        Id = photo.RentalEvidencePhotoId,
        AssetAssignmentId = photo.AssetAssignmentId,
        Stage = photo.Stage,
        ImageUrl = photo.ImageUrl,
        Note = photo.Note,
        CreatedAt = RentalPeriodCalculator.ToUtc(photo.CreatedAt)
    };
}
