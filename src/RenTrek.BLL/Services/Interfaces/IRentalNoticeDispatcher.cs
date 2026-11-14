namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Gom thông báo trong transaction (khử trùng theo eventKey) và chỉ gửi email sau khi commit,
/// để job chạy lại hoặc rollback không gửi mail trùng / mail sai.
/// </summary>
public interface IRentalNoticeDispatcher
{
    /// <summary>Thêm thông báo in-app (chưa SaveChanges). Trả false nếu eventKey đã gửi trước đó.</summary>
    Task<bool> QueueAsync(
        Guid recipientUserId,
        string orderCode,
        Guid orderId,
        string type,
        string title,
        string content,
        string eventKey,
        RentalEmailNotice? email,
        CancellationToken cancellationToken = default);

    /// <summary>Gửi các email đã gom (gọi sau commit). Lỗi SMTP chỉ ghi log.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>Bỏ các email đã gom (gọi khi rollback).</summary>
    void Discard();
}
