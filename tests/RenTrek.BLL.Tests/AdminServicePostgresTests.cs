using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Tests;

public sealed class AdminServicePostgresTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [AdminPostgresFact]
    public async Task ReadQueries_TranslateOnPostgres_AndKeepResponseShapes()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var context = database.Context(); var service = database.Service(context); var request = new AdminQueryRequest();
        Assert.Equal(5, (await service.Users.GetUsersAsync(request, Token)).TotalItems);
        Assert.Equal(2, (await service.Orders.GetOrdersAsync(request, Token)).TotalItems);
        Assert.Single(await service.MasterData.GetCategoriesAsync(request, Token));
        Assert.Single(await service.MasterData.GetLocationsAsync(request, Token));
        Assert.Empty(await service.MasterData.GetTagsAsync(request, Token));
        Assert.Empty(await service.MasterData.GetSoftSkillsAsync(request, Token));
        Assert.Empty((await service.Promotions.GetPromotionsAsync(request, Token)).Items);
        Assert.Single((await service.Governance.GetReviewsAsync(request, Token)).Items);
        Assert.Single(await service.Governance.GetSettingsAsync(Token));
        Assert.Empty((await service.Governance.GetAuditLogsAsync(request, null, Token)).Items);
        Assert.Equal(1, (await service.Reports.GetStatsAsync(Token)).TripsDepartingIn7DaysCount);
        Assert.Single(await service.Reports.GetAlertsAsync(Token));
        Assert.Equal(1, (await service.Reports.GetKpisAsync(Token)).WeeklyBookingsCount);
        Assert.Empty(await service.Reports.GetRevenueAsync(request, Token));
        Assert.Empty(await service.Reports.GetOccupancyAsync(request, Token));
        Assert.Single(await service.Reports.GetUtilizationAsync(request, Token));
        Assert.Equal(0, (await service.Reports.GetRefundChargesAsync(request, Token)).RefundedAmount);
        Assert.Single((await service.Orders.GetOrderAsync("TOUR-TEST", Token)).Participants);
        Assert.Single((await service.Orders.GetOrderAsync("RENT-TEST", Token)).Lines);
    }

    [AdminPostgresFact]
    public async Task Staff_SeesOnlyOrdersTheyHandle()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var context = database.Context(); var service = database.Service(context, database.StaffId, "Staff");
        Assert.Single((await service.Orders.GetOrdersAsync(new(), Token)).Items);
        await Assert.ThrowsAsync<NotFoundException>(() => service.Orders.GetOrderAsync("RENT-TEST", Token));
        await context.RentalOrders.Where(x => x.Order.OrderCode == "RENT-TEST").ExecuteUpdateAsync(x => x.SetProperty(r => r.HandledByUserId, database.StaffId));
        Assert.Equal(2, (await service.Orders.GetOrdersAsync(new(), Token)).TotalItems);
        await using var otherContext = database.Context(); var other = database.Service(otherContext, database.OtherStaffId, "Staff");
        Assert.Empty((await other.Orders.GetOrdersAsync(new(), Token)).Items);
        await Assert.ThrowsAsync<ForbiddenException>(() => other.Users.GetUsersAsync(new(), Token));
    }

    [AdminPostgresFact]
    public async Task ManualPayment_IsIdempotent_AndCancellationReleasesAndQueuesRefund()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var context = database.Context(); var service = database.Service(context);
        var request = new ManualPaymentRequest(1000000, "Cash", "MANUAL-1", "Thu tại quầy");
        await service.Orders.ManualPaymentAsync("TOUR-TEST", request, Token);
        await service.Orders.ManualPaymentAsync("TOUR-TEST", request, Token);
        Assert.Single(await context.Payments.ToListAsync());
        Assert.Equal("Confirmed", (await service.Orders.GetOrderAsync("TOUR-TEST", Token)).Order.Status);
        await service.Orders.CancelOrderAsync("TOUR-TEST", new("Hủy theo yêu cầu quản trị"), Token);
        await service.Orders.CancelOrderAsync("TOUR-TEST", new("Gửi lại yêu cầu"), Token);
        Assert.Single(await context.Payments.Where(x => x.PaymentType == "Refund" && x.Status == "PendingApproval" && x.Amount == 1000000).ToListAsync());
        Assert.Equal("Cancelled", (await context.BookingParticipants.SingleAsync()).Status);
        Assert.True(await context.AuditLogs.AnyAsync(x => x.Action == "ManualPayment"));
        Assert.True(await context.AuditLogs.AnyAsync(x => x.Action == "Cancel"));
    }

    [AdminPostgresFact]
    public async Task LockAndRoleChange_RevokeAccessAndRefreshTokens_AndProtectAdmins()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var context = database.Context(); var service = database.Service(context);
        var refresh = new RefreshToken { UserId = database.StaffId, FamilyId = Guid.NewGuid(), TokenHash = "hash", ExpiresAt = database.Now.UtcDateTime.AddDays(7) };
        refresh.AccessTokens.Add(new AccessToken { UserId = database.StaffId, Jti = "test-jti", ExpiresAt = database.Now.UtcDateTime.AddHours(1) });
        context.RefreshTokens.Add(refresh); await context.SaveChangesAsync();
        await service.Users.SetUserLockAsync(database.StaffId, true, "Kiểm tra bảo mật", Token);
        Assert.NotNull(refresh.RevokedAt); Assert.NotNull(refresh.AccessTokens.Single().RevokedAt);
        await service.Users.SetUserLockAsync(database.StaffId, false, null, Token);
        await service.Users.ChangeRoleAsync(database.StaffId, new("Guide", "Điều chuyển nhân sự"), Token);
        await Assert.ThrowsAsync<ValidationException>(() => service.Users.SetUserLockAsync(database.AdminId, true, "Tự khóa", Token));
        await Assert.ThrowsAsync<ValidationException>(() => service.Users.ChangeRoleAsync(database.AdminId, new("Staff", "Đổi vai trò"), Token));
        Assert.Equal("Guide", (await service.Users.GetUserAsync(database.StaffId, Token)).User.Role);
    }

    [AdminPostgresFact]
    public async Task MasterData_RejectsDuplicateName_AndLocationInUse_AndPreservesSoftDeletes()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var context = database.Context(); var service = database.Service(context);
        var tag = await service.MasterData.CreateTagAsync(new("An toàn", "an-toan"), Token);
        await Assert.ThrowsAsync<ConflictException>(() => service.MasterData.CreateTagAsync(new("An toàn", "an-toan-2"), Token));
        var location = (await service.MasterData.GetLocationsAsync(new(), Token)).Single();
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => service.MasterData.DeleteLocationAsync(location.LocationId, Token));
        Assert.Equal("LOCATION_IN_USE", exception.ErrorCode);
        await service.MasterData.DeleteTagAsync(tag, Token);
        Assert.Empty(await service.MasterData.GetTagsAsync(new(), Token));
        Assert.True((await context.Tags.SingleAsync()).IsDeleted);
    }

    [AdminPostgresFact]
    public async Task Promotions_ReplaceTiersWithoutDeletingUsageHistory()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var context = database.Context(); var service = database.Service(context);
        var id = await service.Promotions.CreatePromotionAsync(new("Ưu đãi", "TEST10", null, database.Now.UtcDateTime, database.Now.UtcDateTime.AddDays(10), "Tour", "Order", 10, 1, false, 1), Token);
        await service.Promotions.SetPromotionTiersAsync(id, new([new("OrderAmount", 100000, "Percentage", 10, null, null)]), Token);
        await service.Promotions.SetPromotionStatusAsync(id, new("Active"), Token);
        var tier = (await service.Promotions.GetPromotionAsync(id, Token)).Tiers.Single();
        context.AppliedPromotions.Add(new AppliedPromotion { AppliedPromotionId = Guid.NewGuid(), PromotionTierId = tier.PromotionTierId, OrderId = (await context.Orders.SingleAsync(x => x.OrderCode == "TOUR-TEST")).OrderId, DiscountAmount = 10000, AppliedAt = database.Now.UtcDateTime, CreatedAt = database.Now.UtcDateTime, Status = "Applied" });
        await context.SaveChangesAsync();
        await service.Promotions.SetPromotionTiersAsync(id, new([new("OrderAmount", 200000, "Percentage", 15, null, null)]), Token);
        Assert.Equal(15, (await service.Promotions.GetPromotionAsync(id, Token)).Tiers.Single().DiscountValue);
        Assert.Single((await service.Promotions.GetPromotionUsageAsync(id, new(), Token)).Items);
        Assert.Equal(1, (await service.Promotions.GetPromotionAsync(id, Token)).Promotion.UsedCount);
    }

    [AdminPostgresFact]
    public async Task ReviewModeration_RequiresReason_AndNeverChangesComment()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var context = database.Context(); var service = database.Service(context);
        await Assert.ThrowsAsync<ValidationException>(() => service.Governance.SetReviewVisibilityAsync(database.ReviewId, true, " ", Token));
        await service.Governance.SetReviewVisibilityAsync(database.ReviewId, true, "Vi phạm tiêu chuẩn", Token);
        Assert.Single((await service.Governance.GetReviewsAsync(new() { Status = "Hidden" }, Token)).Items);
        await service.Governance.SetReviewVisibilityAsync(database.ReviewId, false, null, Token);
        var review = await context.Reviews.SingleAsync();
        Assert.Equal("Nội dung gốc", review.Comment); Assert.False(review.IsHidden); Assert.Null(review.HiddenReason);
    }

    [AdminPostgresFact]
    public async Task SettingsAndParticipantOverrides_RecordBeforeAndAfter_AndRejectInvalidValues()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var context = database.Context(); var service = database.Service(context);
        await Assert.ThrowsAsync<ValidationException>(() => service.Governance.UpdateSettingAsync("orders.holdExpiresMinutes", new("-1", "Sai giá trị"), Token));
        await service.Governance.UpdateSettingAsync("orders.holdExpiresMinutes", new("30", "Tăng thời gian giữ chỗ"), Token);
        await service.Orders.OverrideParticipantAsync("TOUR-TEST", database.ParticipantId, new("Tên đã sửa", "0912345678", null, "Người thân", "0987654321", "Khách yêu cầu"), Token);
        Assert.Equal("30", (await service.Governance.GetSettingsAsync(Token)).Single().ConfigValue);
        var logs = await service.Governance.GetAuditLogsAsync(new(), null, Token);
        Assert.Equal(2, logs.TotalItems);
        Assert.All(logs.Items, log => Assert.True(log.Details.TryGetProperty("before", out _)));
        Assert.Contains((await service.Orders.GetOrderAsync("TOUR-TEST", Token)).AuditLogs, x => x.EntityType == "BookingParticipant");
    }
}
