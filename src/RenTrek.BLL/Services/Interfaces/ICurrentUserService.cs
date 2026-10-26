namespace RenTrek.BLL.Services.Interfaces;

// Interface ở BLL, implement ở API (đọc từ HttpContext)
public interface ICurrentUserService
{
    // null nếu request chưa đăng nhập
    Guid? UserId { get; }

    // Ném UnauthorizedException nếu token không có định danh hợp lệ
    Guid RequiredUserId { get; }

    string? Jti { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}
