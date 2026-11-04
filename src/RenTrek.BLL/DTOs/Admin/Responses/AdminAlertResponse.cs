namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminAlertResponse(string AlertType, string Severity, string Title, string Message, string RelatedEntity, string EntityId, string ActionUrl);
