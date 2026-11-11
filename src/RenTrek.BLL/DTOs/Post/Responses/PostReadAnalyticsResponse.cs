namespace RenTrek.BLL.DTOs.Post.Responses;

/// <summary>Tỉ lệ khách của các tour gắn bài đã đọc bài (bảng PARTICIPANT_POST_READS).</summary>
public sealed record PostReadAnalyticsResponse(Guid PostId, int ParticipantsCount, int ReadCount, decimal ReadRate);
