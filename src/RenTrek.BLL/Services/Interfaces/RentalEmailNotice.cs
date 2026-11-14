namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Nội dung email đi kèm một thông báo luồng thuê đồ.</summary>
public sealed record RentalEmailNotice(
    string Subject,
    string Title,
    IReadOnlyList<(string Label, string Value)> Rows,
    string Instruction);
