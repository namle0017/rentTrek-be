namespace RenTrek.BLL.DTOs.Participation.Responses;

/// <summary>
/// Kết quả ghi nhận trạng thái đã đọc bài viết hướng dẫn cho hành khách (idempotent).
/// </summary>
public sealed class ParticipantPostReadResponse
{
    public Guid ParticipantPostReadId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid PostId { get; set; }
    public bool IsRead { get; set; }
    public DateTime ReadAt { get; set; }
}
