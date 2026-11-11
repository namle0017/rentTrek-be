namespace RenTrek.BLL.Constants;

/// <summary>asset_assignments.return_condition.</summary>
public static class ReturnConditions
{
    public const string Normal = "Normal";
    public const string Damaged = "Damaged";
    public const string MissingAccessory = "MissingAccessory";
    public const string Lost = "Lost";

    public static readonly string[] All = [Normal, Damaged, MissingAccessory, Lost];
}
