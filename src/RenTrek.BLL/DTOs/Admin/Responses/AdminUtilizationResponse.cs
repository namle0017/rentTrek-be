namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminUtilizationResponse(Guid EquipmentAssetId, string AssetCode, decimal IssuedDays, decimal PeriodDays, decimal UtilizationRate);
