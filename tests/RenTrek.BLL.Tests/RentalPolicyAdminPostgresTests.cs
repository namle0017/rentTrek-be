using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.RentalPolicy.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;
using RenTrek.DAL.Data;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

/// <summary>Theory chỉ chạy khi có RENTREK_TEST_POSTGRES (bản Theory của <see cref="AdminPostgresFactAttribute"/>).</summary>
public sealed class AdminPostgresTheoryAttribute : TheoryAttribute
{
    public AdminPostgresTheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RENTREK_TEST_POSTGRES")))
            Skip = "Set RENTREK_TEST_POSTGRES to a disposable PostgreSQL server connection (database creation required).";
    }
}

/// <summary>Test tích hợp Postgres cho phần admin của <see cref="RentalPolicyService"/> (TC-RP-R01..R11).</summary>
public sealed class RentalPolicyAdminPostgresTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private static RentalPolicyService Service(AdminTestDatabase db, AppDbContext context)
        => new(new UnitOfWork(context), new FixedClock(db.Now));

    private static CreateRentalPolicyVersionRequest Request(string name = "Policy thuê", decimal deposit = 20,
        List<RentalPolicyTierRequest>? tiers = null) => new()
    {
        Name = name,
        DepositPercent = deposit,
        FreeCancelLimit = 2,
        FreeCancelWindowDays = 30,
        OverLimitCancelFeePercent = 5,
        ReturnGraceHours = 2,
        ExtensionMaxDaysLate = 2,
        SuspectedLossAfterDays = 7,
        ReceiptAutoConfirmHours = 24,
        MinPhotosPerAsset = 1,
        Tiers = tiers ?? ValidTiers()
    };

    private static List<RentalPolicyTierRequest> ValidTiers() =>
    [
        new() { Scope = "PickupLate", MinDaysLate = 1, PercentOfDeposit = 5, IsTerminal = false },
        new() { Scope = "PickupLate", MinDaysLate = 3, PercentOfDeposit = 100, IsTerminal = true },
        new() { Scope = "ReturnLate", MinDaysLate = 2, PercentOfDeposit = 10, IsTerminal = false },
        new() { Scope = "ReturnLate", MinDaysLate = 5, PercentOfDeposit = 100, IsTerminal = true }
    ];

    private static RentalPolicyTierRequest Tier(string scope, int days, decimal percent, bool terminal) =>
        new() { Scope = scope, MinDaysLate = days, PercentOfDeposit = percent, IsTerminal = terminal };

    private static async Task<(AdminTestDatabase Db, AppDbContext Context, RentalPolicyService Service)> StartAsync()
    {
        var db = new AdminTestDatabase();
        await db.InitializeAsync();
        var context = db.Context();
        return (db, context, Service(db, context));
    }

    [AdminPostgresFact]
    public async Task R01_R03_GetAll_SortedByVersionDesc_WithAdminFields_AndGetById()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v1 = await service.CreateVersionAsync(db.AdminId, Request("v1"), Token);
        var v2 = await service.CreateVersionAsync(db.AdminId, Request("v2", 30), Token);

        var all = await service.GetAllAsync(Token);
        Assert.Equal([2, 1], all.Select(x => x.Version).ToArray());
        Assert.All(all, x =>
        {
            Assert.Equal("Admin Test", x.CreatedByName);
            Assert.NotEqual(default, x.CreatedAt);
        });

        var detail = await service.GetByIdAsync(v1.RentalPolicyId!.Value, Token);
        Assert.Equal(4, detail.Tiers.Count);
        Assert.Equal(2, detail.Tiers.Count(t => t.Scope == "PickupLate"));
        Assert.Equal("Admin Test", detail.CreatedByName);
        Assert.Equal(v2.RentalPolicyId, all[0].RentalPolicyId);
    }

    [AdminPostgresFact]
    public async Task R02_OrdersCount_CountsOrdersSnapshottingEachVersion()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v1 = await service.CreateVersionAsync(db.AdminId, Request("v1"), Token);
        await context.RentalOrders.Where(x => x.Order.OrderCode == "RENT-TEST")
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.RentalPolicyId, v1.RentalPolicyId), Token);
        var v2 = await service.CreateVersionAsync(db.AdminId, Request("v2"), Token);
        var all = await service.GetAllAsync(Token);
        Assert.Equal(1, all.Single(x => x.RentalPolicyId == v1.RentalPolicyId).OrdersCount);
        Assert.Equal(0, all.Single(x => x.RentalPolicyId == v2.RentalPolicyId).OrdersCount);
    }

    [AdminPostgresFact]
    public async Task R04_GetById_Missing_ThrowsNotFound()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(Guid.NewGuid(), Token));
    }

    [AdminPostgresFact]
    public async Task R05_CreateVersion_ArchivesOldAndActivatesNew_InSameFamily()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v1 = await service.CreateVersionAsync(db.AdminId, Request("v1"), Token);
        var v2 = await service.CreateVersionAsync(db.AdminId, Request("v2"), Token);
        Assert.Equal(2, v2.Version);
        Assert.Equal("Active", v2.Status);
        Assert.Equal(v1.PolicyFamilyId, v2.PolicyFamilyId);
        var old = await service.GetByIdAsync(v1.RentalPolicyId!.Value, Token);
        Assert.Equal("Archived", old.Status);
        Assert.NotNull(old.EffectiveUntil);
        Assert.Equal(v2.RentalPolicyId, (await service.GetActiveAsync(Token)).RentalPolicyId);
    }

    [AdminPostgresFact]
    public async Task R06_CreateVersion_OnEmptyDatabase_StartsAtVersion1()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var created = await service.CreateVersionAsync(db.AdminId, Request(), Token);
        Assert.Equal(1, created.Version);
        Assert.Equal("Active", created.Status);
    }

    public static IEnumerable<object[]> InvalidTierSets()
    {
        yield return [new List<RentalPolicyTierRequest> { Tier("ReturnLate", 2, 100, true) }, "tiers.PickupLate"];                        // thiếu scope
        yield return [Replace("PickupLate", Tier("PickupLate", 1, 5, false), Tier("PickupLate", 3, 100, false)), "tiers.PickupLate"];   // không có terminal
        yield return [Replace("PickupLate", Tier("PickupLate", 1, 100, true), Tier("PickupLate", 3, 100, true)), "tiers.PickupLate"];   // 2 terminal
        yield return [Replace("PickupLate", Tier("PickupLate", 1, 100, true), Tier("PickupLate", 3, 50, false)), "tiers.PickupLate"];   // terminal không ở cuối
        yield return [Replace("PickupLate", Tier("PickupLate", 1, 50, false), Tier("PickupLate", 3, 20, true)), "tiers.PickupLate"];    // % giảm
        yield return [Replace("PickupLate", Tier("PickupLate", 2, 5, false), Tier("PickupLate", 2, 100, true)), "tiers.PickupLate"];    // ngày trùng
        yield return [Replace("ReturnLate", Tier("ReturnLate", 0, 5, false), Tier("ReturnLate", 3, 100, true)), "tiers.ReturnLate"];    // ngày <= 0
        yield return [Replace("ReturnLate", Tier("ReturnLate", 1, 5, false), Tier("ReturnLate", 3, 101, true)), "tiers.ReturnLate"];    // % > 100
    }

    private static List<RentalPolicyTierRequest> Replace(string scope, params RentalPolicyTierRequest[] replacement)
        => ValidTiers().Where(t => t.Scope != scope).Concat(replacement).ToList();

    [AdminPostgresTheory]
    [MemberData(nameof(InvalidTierSets))]
    public async Task R07_CreateVersion_InvalidTiers_ThrowsValidation(List<RentalPolicyTierRequest> tiers, string expectedKey)
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateVersionAsync(db.AdminId, Request(tiers: tiers), Token));
        Assert.Contains(expectedKey, ex.Errors.Keys);
        Assert.Empty(await context.RentalPolicies.ToListAsync(Token));
    }

    [AdminPostgresFact]
    public async Task R08_TerminalTier_PercentIsForcedTo100()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var tiers = Replace("PickupLate", Tier("PickupLate", 1, 5, false), Tier("PickupLate", 3, 100, true));
        var created = await service.CreateVersionAsync(db.AdminId, Request(tiers: tiers), Token);
        Assert.All(created.Tiers.Where(t => t.IsTerminal), t => Assert.Equal(100, t.PercentOfDeposit));
    }

    [AdminPostgresFact]
    public async Task R10_OldOrdersKeepSnapshotOfOldVersion()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v1 = await service.CreateVersionAsync(db.AdminId, Request("v1", 20), Token);
        await service.CreateVersionAsync(db.AdminId, Request("v2", 40), Token);
        var snapshot = await service.GetSnapshotAsync(v1.RentalPolicyId, Token);
        Assert.Equal(20, snapshot.DepositPercent);
        Assert.Equal(40, (await service.GetActiveSnapshotAsync(Token)).DepositPercent);
    }

    [AdminPostgresTheory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task R11_Name_Required(string name)
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateVersionAsync(db.AdminId, Request(name), Token));
        Assert.Contains("name", ex.Errors.Keys);
    }

    [AdminPostgresFact]
    public async Task R11_Name_Over255_Rejected()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateVersionAsync(db.AdminId, Request(new string('n', 256)), Token));
    }
}
