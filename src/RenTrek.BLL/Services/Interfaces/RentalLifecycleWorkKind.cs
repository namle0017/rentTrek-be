using RenTrek.BLL.DTOs.RentalOrder.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Loại việc job vòng đời đơn thuê cần xử lý.</summary>
public enum RentalLifecycleWorkKind
{
    PickupOverdue,
    ReturnReminder,
    ReturnOverdue,
    AutoConfirmReceipt,
    BankInfoReminder,
    ExpirePendingExtension
}
