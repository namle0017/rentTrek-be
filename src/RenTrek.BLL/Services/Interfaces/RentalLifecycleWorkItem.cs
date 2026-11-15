using RenTrek.BLL.DTOs.RentalOrder.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Một việc của job: <see cref="Id"/> là orderId, riêng BankInfoReminder là refundCaseId.</summary>
public sealed record RentalLifecycleWorkItem(RentalLifecycleWorkKind Kind, Guid Id);
