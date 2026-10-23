using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Seeds;

public static class RoleSeed
{
    public static readonly Role Admin = new()
    {
        RoleId = 1,
        RoleName = "Admin",
        Description = "Quản trị viên toàn quyền hệ thống",
        Status = "Active"
    };

    public static readonly Role Staff = new()
    {
        RoleId = 2,
        RoleName = "Staff",
        Description = "Nhân viên vận hành, quản lý đơn thuê và tour",
        Status = "Active"
    };

    public static readonly Role Customer = new()
    {
        RoleId = 3,
        RoleName = "Customer",
        Description = "Khách hàng thuê thiết bị và đăng ký tour",
        Status = "Active"
    };

    public static readonly Role TourGuide = new()
    {
        RoleId = 4,
        RoleName = "TourGuide",
        Description = "Hướng dẫn viên dẫn đoàn tour trekking",
        Status = "Active"
    };

    public static IEnumerable<Role> GetInitialRoles()
    {
        yield return Admin;
        yield return Staff;
        yield return Customer;
        yield return TourGuide;
    }
}
