namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>
/// Đồng hồ chỉ dùng ở môi trường Development: giờ thật + một độ lệch chỉnh được,
/// để giả lập "gần chuyến đi / đang đi / đã kết thúc" hay "trả đồ trễ" mà không phải sửa ngày trong DB.
/// Độ lệch nằm trong bộ nhớ: khởi động lại API là về giờ thật.
/// </summary>
public sealed class SimulatedTimeProvider(TimeProvider? realClock = null) : TimeProvider
{
    private readonly TimeProvider _realClock = realClock ?? System;
    private long _offsetTicks;

    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public DateTimeOffset RealUtcNow => _realClock.GetUtcNow();

    public override DateTimeOffset GetUtcNow() => _realClock.GetUtcNow() + Offset;

    /// <summary>Đặt "bây giờ" của hệ thống thành <paramref name="target"/>; đồng hồ vẫn chạy tiếp từ mốc đó.</summary>
    public void SetUtcNow(DateTimeOffset target)
        => Interlocked.Exchange(ref _offsetTicks, (target - _realClock.GetUtcNow()).Ticks);

    public void Reset() => Interlocked.Exchange(ref _offsetTicks, 0);
}
