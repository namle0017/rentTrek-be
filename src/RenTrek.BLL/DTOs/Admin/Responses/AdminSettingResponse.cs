namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminSettingResponse(string ConfigKey, string ConfigValue, string ValueType, string? Description, DateTime UpdatedAt);
