using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.BLL.Tests;

/// <summary>
/// Handler mặc định cho các repo của luồng cọc (policy mặc định, chưa có hồ sơ hoàn / quyết toán / ảnh, khách đã xác thực email),
/// nối vào cuối TestProxy của IUnitOfWork ở các test không kiểm tra luồng cọc. Handler khai báo trước trong test được ưu tiên.
/// </summary>
internal static class RentalDepositTestDefaults
{
    public static (string Name, Func<object?[], object?> Handler)[] UnitOfWorkHandlers() =>
    [
        ("get_RentalPolicies", _ => TestProxy.Create<IRentalPolicyRepository>(
            ("GetActiveWithTiersAsync", _ => Task.FromResult<RentalPolicy?>(null)),
            ("GetWithTiersAsync", _ => Task.FromResult<RentalPolicy?>(null)))),
        ("get_RefundCases", _ => TestProxy.Create<IRefundCaseRepository>(
            ("GetLatestByOrderAsync", _ => Task.FromResult<RefundCase?>(null)))),
        ("get_RentalSettlements", _ => TestProxy.Create<IRentalSettlementRepository>(
            ("GetByOrderAsync", _ => Task.FromResult<RentalSettlement?>(null)))),
        ("get_RentalEvidencePhotos", _ => TestProxy.Create<IRentalEvidencePhotoRepository>(
            ("GetByOrderAsync", _ => Task.FromResult<IReadOnlyList<RentalEvidencePhoto>>([])))),
        ("get_ProductVariants", _ => TestProxy.Create<IProductVariantRepository>(
            ("GetReplacementValuesAsync", _ => Task.FromResult(new Dictionary<Guid, decimal?>())))),
        ("get_ProductBundles", _ => TestProxy.Create<IProductBundleRepository>(
            ("GetReplacementValuesAsync", _ => Task.FromResult(new Dictionary<Guid, decimal?>())))),
        ("get_Accounts", args => TestProxy.Create<IAccountRepository>(
            ("GetByIdWithRoleAsync", a => Task.FromResult<Account?>(new Account
            {
                UserId = (Guid)a[0]!,
                Email = string.Empty,
                FullName = "Khách",
                EmailConfirmedAt = DateTime.UtcNow
            }))))
    ];
}
