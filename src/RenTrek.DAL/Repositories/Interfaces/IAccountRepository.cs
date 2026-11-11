using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IAccountRepository : IGenericRepository<Account>
{
    Task<Account?> GetByEmailWithRoleAsync(string email, bool tracking = false, CancellationToken cancellationToken = default);
    Task<Account?> GetByIdWithRoleAsync(Guid userId, bool tracking = false, CancellationToken cancellationToken = default);
    /// <summary>Email đã có tài khoản (kể cả đã xoá mềm).</summary>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Danh sách tài khoản chưa xoá trang quản trị.</summary>
    Task<(IReadOnlyList<AdminUserRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminUserFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Một tài khoản chưa xoá (chỉ đọc); null nếu không có.</summary>
    Task<AdminUserRow?> GetAdminRowAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Tài khoản chưa xoá theo danh sách vai trò và trạng thái, sắp theo tên (chỉ đọc).</summary>
    Task<IReadOnlyList<AdminUserRow>> GetByRolesAsync(
        IReadOnlyCollection<string> roleNames, string status, CancellationToken cancellationToken = default);

    /// <summary>Số đơn đã đặt, số chuyến quản lý và số chuyến dẫn của tài khoản.</summary>
    Task<AccountActivityRow> GetActivityAsync(Guid userId, CancellationToken cancellationToken = default);
}
