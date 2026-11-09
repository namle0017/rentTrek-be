namespace RenTrek.API.Common;

/// <summary>
/// Tên policy rate limit đăng ký ở Program.cs.
/// </summary>
public static class RateLimitPolicies
{
    public const string RefundSensitive = "refund-sensitive";

    /// <summary>Endpoint auth ẩn danh (login, register, OTP...): giới hạn theo IP (PRD NFR-02).</summary>
    public const string AuthSensitive = "auth-sensitive";
}
