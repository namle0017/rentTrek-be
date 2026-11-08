namespace RenTrek.API.Common;

/// <summary>
/// Tên policy rate limit đăng ký ở Program.cs.
/// </summary>
public static class RateLimitPolicies
{
    public const string RefundSensitive = "refund-sensitive";
    public const string AuthPublic = "auth-public";
}
