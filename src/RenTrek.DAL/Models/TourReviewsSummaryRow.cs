namespace RenTrek.DAL.Models;

/// <summary>
/// Kết quả tổng hợp số lượng sao theo mức 1–5 và danh sách đánh giá theo trang của một họ tour.
/// </summary>
public sealed class TourReviewsSummaryRow
{
    public IReadOnlyDictionary<int, int> RatingCounts { get; set; } = new Dictionary<int, int>();
    public IReadOnlyList<TourReviewRow> Items { get; set; } = [];
    public int TotalCount { get; set; }
}
