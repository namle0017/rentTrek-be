using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.RentalOps.Requests;

public sealed class ReturnRentalAssetItemRequest
{
    public Guid AssignmentId { get; set; }

    /// <summary>Normal | Damaged | MissingAccessory | Lost</summary>
    public string Condition { get; set; } = string.Empty;

    public string? Note { get; set; }
}
