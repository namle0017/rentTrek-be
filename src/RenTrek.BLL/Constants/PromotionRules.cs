namespace RenTrek.BLL.Constants;

/// <summary>
/// Từ vựng của promotions.applies_to / application_scope và promotion_tiers.qualification_metric / discount_type.
/// Schema (varchar) không ràng buộc miền giá trị nên so khớp KHÔNG phân biệt hoa thường và bỏ ký tự không phải chữ/số
/// (ví dụ "Percentage", "percent_off" đều khớp). Giá trị không nhận diện được thì bậc đó không đủ điều kiện (không throw).
/// </summary>
public static class PromotionRules
{
    /// <summary>applies_to áp dụng cho đơn thuê: Rental, Equipment..., hoặc áp dụng mọi loại đơn.</summary>
    public static readonly IReadOnlyList<string> AppliesToRental =
        ["rental", "rentalorder", "equipmentrental", "equipment", "all", "any", "both"];

    public static readonly IReadOnlyList<string> ScopeOrder = ["order", "wholeorder", "orderlevel", "cart"];

    public static readonly IReadOnlyList<string> ScopeLine = ["line", "item", "orderitem", "lineitem", "rentalorderitem"];

    /// <summary>Ngưỡng tính theo tổng tiền thuê trước giảm (VND).</summary>
    public static readonly IReadOnlyList<string> MetricAmount =
    [
        "amount", "subtotal", "subtotalamount", "ordersubtotal", "ordervalue", "orderamount",
        "totalamount", "rentalamount", "rentalvalue", "spend", "revenue"
    ];

    /// <summary>Ngưỡng tính theo tổng số lượng thiết bị.</summary>
    public static readonly IReadOnlyList<string> MetricQuantity =
        ["quantity", "itemquantity", "itemcount", "totalquantity", "items", "units"];

    /// <summary>Ngưỡng tính theo số ngày thuê tính tiền.</summary>
    public static readonly IReadOnlyList<string> MetricDays =
        ["days", "billabledays", "rentaldays", "duration", "rentalduration"];

    public static readonly IReadOnlyList<string> DiscountPercent =
        ["percent", "percentage", "percentoff", "pct", "percentdiscount"];

    public static readonly IReadOnlyList<string> DiscountFixed =
        ["fixed", "fixedamount", "amount", "amountoff", "fixeddiscount", "flat", "money"];

    /// <summary>Chuẩn hoá để so khớp: chữ thường, chỉ giữ chữ và số.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    /// <summary>Kiểm tra giá trị (sau khi chuẩn hoá) có thuộc từ vựng hay không.</summary>
    public static bool Matches(string? value, IReadOnlyList<string> vocabulary)
        => vocabulary.Contains(Normalize(value));
}
