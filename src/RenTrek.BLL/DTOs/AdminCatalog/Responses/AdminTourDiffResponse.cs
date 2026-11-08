namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Sai khác giữa hai phiên bản cùng họ tour (§6.26): đi từ bản gốc (Base) sang bản so sánh (Target).</summary>
public sealed class AdminTourDiffResponse
{
    public Guid BaseTourId { get; set; }
    public int BaseVersion { get; set; }
    public Guid TargetTourId { get; set; }
    public int TargetVersion { get; set; }
    public IReadOnlyList<AdminTourDiffItemResponse> Changes { get; set; } = [];
}
