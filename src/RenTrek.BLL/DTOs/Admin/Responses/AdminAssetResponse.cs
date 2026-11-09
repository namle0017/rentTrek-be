namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminAssetResponse(Guid AssetAssignmentId, string AssetCode, string Status, DateTime ReservedFrom, DateTime ReservedUntil, DateTime? ReturnedAt);
