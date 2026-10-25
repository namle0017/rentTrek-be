namespace RenTrek.BLL.Helpers;

public static class DeviceDetector
{
    public static string Detect(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return "Trình duyệt web";

        var ua = userAgent;

        // Detect OS
        string os = "Thiết bị khác";
        if (ua.Contains("Windows NT 10.0", StringComparison.OrdinalIgnoreCase) || ua.Contains("Windows NT", StringComparison.OrdinalIgnoreCase))
            os = "Windows";
        else if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
            os = "iPhone";
        else if (ua.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            os = "iPad";
        else if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase))
            os = "Android";
        else if (ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) || ua.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase))
            os = "macOS";
        else if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            os = "Linux";

        // Detect Browser
        string browser = "Trình duyệt web";
        if (ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) || ua.Contains("Edge/", StringComparison.OrdinalIgnoreCase))
            browser = "Edge";
        else if (ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase) || ua.Contains("Opera", StringComparison.OrdinalIgnoreCase))
            browser = "Opera";
        else if (ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Chromium", StringComparison.OrdinalIgnoreCase))
            browser = "Chrome";
        else if (ua.Contains("Safari", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            browser = "Safari";
        else if (ua.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            browser = "Firefox";

        return $"{os} • {browser}";
    }
}
