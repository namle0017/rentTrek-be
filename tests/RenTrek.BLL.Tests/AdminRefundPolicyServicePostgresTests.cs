using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Data;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

/// <summary>
/// Test tích hợp Postgres cho <see cref="AdminRefundPolicyService"/> theo plan (TC-RP-C/E/D/A/LA/CL/F/S/Q).
/// Seed của <see cref="AdminTestDatabase"/>: một policy Tour Active (Version 1, hiệu lực từ Now-1 năm, không có rule)
/// đang được 1 Trip + 1 Order dùng, nên cũng là policy Tour hiệu lực duy nhất.
/// </summary>
public sealed class AdminRefundPolicyServicePostgresTests
{
    private static readonly CancellationToken Token = CancellationToken.None;
    private static readonly DateOnly Past = new(2026, 1, 1);
    private static readonly DateOnly Future = new(2027, 6, 1);

    private static IAdminRefundPolicyService Service(AdminTestDatabase db, AppDbContext context, string role = "Admin")
        => new AdminRefundPolicyService(new UnitOfWork(context), new TestActor(role == "Admin" ? db.AdminId : db.StaffId, role), new FixedClock(db.Now));

    private static List<RefundPolicyRuleRequest> Rules3() =>
        [new() { MinHoursBefore = 72, RefundPercentage = 100 }, new() { MinHoursBefore = 24, RefundPercentage = 50 }, new() { MinHoursBefore = 0, RefundPercentage = 0 }];

    private static SaveRefundPolicyRequest Req(string name = "Policy", string type = "Tour", bool draft = false,
        DateOnly? from = null, DateOnly? until = null, string? description = null, List<RefundPolicyRuleRequest>? rules = null)
        => new() { Name = name, PolicyType = type, Description = description, EffectiveFrom = from ?? Past, EffectiveUntil = until, Rules = rules ?? Rules3(), SaveAsDraft = draft };

    private static async Task<T> ThrowsAppAsync<T>(Func<Task> action, int status, string? code = null) where T : AppException
    {
        var ex = await Assert.ThrowsAsync<T>(action);
        Assert.Equal(status, ex.StatusCode);
        if (code != null) Assert.Equal(code, ex.ErrorCode);
        return ex;
    }

    private static async Task ThrowsClientErrorAsync(Func<Task> action)
    {
        var ex = await Assert.ThrowsAnyAsync<AppException>(action);
        Assert.InRange(ex.StatusCode, 400, 409);
    }

    private static async Task<(AdminTestDatabase Db, AppDbContext Context, IAdminRefundPolicyService Service)> StartAsync()
    {
        var db = new AdminTestDatabase();
        await db.InitializeAsync();
        var context = db.Context();
        return (db, context, Service(db, context));
    }

    // ---------- Create ----------

    [AdminPostgresFact]
    public async Task C01_Create_SavesAllRules_AsActiveVersion1()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var id = await service.CreatePolicyAsync(Req("Tết", rules: Rules3()), Token);
        var detail = await service.GetPolicyAsync(id, Token);
        Assert.Equal(3, detail.Rules.Count);
        Assert.Equal("Active", detail.Status);
        Assert.Equal(1, detail.Version);
        Assert.Equal(3, await context.RefundPolicyRules.CountAsync(x => x.RefundPolicyId == id));
    }

    [AdminPostgresFact]
    public async Task C02_CreateDraft_HasDraftStatus_AndRules()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var id = await service.CreatePolicyAsync(Req(draft: true), Token);
        var detail = await service.GetPolicyAsync(id, Token);
        Assert.Equal("Draft", detail.Status);
        Assert.Equal("Draft", detail.DisplayStatus);
        Assert.Equal(3, detail.Rules.Count);
    }

    [AdminPostgresFact]
    public async Task C03_Create_PersistsDescriptionAndEffectiveUntil()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var id = await service.CreatePolicyAsync(Req(description: "Mô tả chi tiết", until: Future), Token);
        var detail = await service.GetPolicyAsync(id, Token);
        Assert.Equal("Mô tả chi tiết", detail.Description);
        Assert.Equal(Future, detail.EffectiveUntil);
    }

    [AdminPostgresFact]
    public async Task C04_List_ReportsRealRulesCount()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var id = await service.CreatePolicyAsync(Req(), Token);
        var page = await service.GetPoliciesAsync(new RefundPolicyQueryRequest(), Token);
        Assert.Equal(3, page.Items.Single(x => x.PolicyId == id).RulesCount);
    }

    [AdminPostgresFact]
    public async Task C06_C07_DisplayStatus_ScheduledVsActive()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var scheduled = await service.CreatePolicyAsync(Req("Future", from: Future), Token);
        var active = await service.CreatePolicyAsync(Req("Now", from: Past), Token);
        Assert.Equal("Scheduled", (await service.GetPolicyAsync(scheduled, Token)).DisplayStatus);
        Assert.Equal("Active", (await service.GetPolicyAsync(active, Token)).DisplayStatus);
    }

    [AdminPostgresFact]
    public async Task C09_Staff_CannotCreate()
    {
        var (db, context, _) = await StartAsync();
        await using var _db = db; await using var __ = context;
        var staff = Service(db, context, "Staff");
        await Assert.ThrowsAsync<ForbiddenException>(() => staff.CreatePolicyAsync(Req(), Token));
    }

    [AdminPostgresFact]
    public async Task C10_Create_WritesAuditLog()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var id = await service.CreatePolicyAsync(Req(), Token);
        Assert.True(await context.AuditLogs.AnyAsync(x => x.EntityType == "RefundPolicy" && x.EntityId == id && x.Action == "Create"));
    }

    // ---------- Edit ----------

    [AdminPostgresFact]
    public async Task E01_Edit_UnusedDraft_ReplacesFieldsAndRules()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var id = await service.CreatePolicyAsync(Req("Cũ", draft: true), Token);
        await service.UpdatePolicyAsync(id, Req("Mới", draft: true, description: "mô tả mới", until: Future), Token);
        await service.SetRulesAsync(id, new RefundPolicyRulesRequest { Rules = [new() { MinHoursBefore = 48, RefundPercentage = 70 }] }, Token);
        var detail = await service.GetPolicyAsync(id, Token);
        Assert.Equal("Mới", detail.Name);
        Assert.Equal("mô tả mới", detail.Description);
        Assert.Equal(Future, detail.EffectiveUntil);
        var rule = Assert.Single(detail.Rules);
        Assert.Equal(48, rule.MinHoursBefore);
        Assert.Equal(70, rule.RefundPercentage);
    }

    [AdminPostgresFact]
    public async Task E02_E03_Edit_PolicyInUse_Returns409()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await ThrowsAppAsync<ConflictException>(() => service.UpdatePolicyAsync(db.PolicyId, Req("Đổi tên"), Token), 409, "POLICY_ALREADY_IN_USE");
        await ThrowsAppAsync<ConflictException>(() => service.SetRulesAsync(db.PolicyId, new RefundPolicyRulesRequest { Rules = Rules3() }, Token), 409, "POLICY_ALREADY_IN_USE");
    }

    [AdminPostgresFact]
    public async Task E04_PolicyType_IsImmutable()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var id = await service.CreatePolicyAsync(Req(type: "Tour", draft: true), Token);
        try { await service.UpdatePolicyAsync(id, Req(type: "Rental", draft: true), Token); }
        catch (AppException) { /* từ chối cũng chấp nhận */ }
        Assert.Equal("Tour", (await service.GetPolicyAsync(id, Token)).PolicyType);
    }

    [AdminPostgresFact]
    public async Task E05_Edit_UnusedActive_IsAllowed()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var id = await service.CreatePolicyAsync(Req(), Token);
        await service.SetRulesAsync(id, new RefundPolicyRulesRequest { Rules = [new() { MinHoursBefore = 10, RefundPercentage = 10 }] }, Token);
        Assert.Single((await service.GetPolicyAsync(id, Token)).Rules);
    }

    [AdminPostgresFact]
    public async Task E06_E07_Edit_MissingOrDeleted_Returns404()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdatePolicyAsync(Guid.NewGuid(), Req(), Token));
        var id = await service.CreatePolicyAsync(Req(draft: true), Token);
        await service.DeletePolicyAsync(id, Token);
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdatePolicyAsync(id, Req(draft: true), Token));
    }

    // ---------- Delete ----------

    [AdminPostgresFact]
    public async Task D01_Delete_UnusedDraft_SoftDeletesPolicyAndRules()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var id = await service.CreatePolicyAsync(Req(draft: true), Token);
        await service.DeletePolicyAsync(id, Token);
        Assert.True((await context.RefundPolicies.IgnoreQueryFilters().SingleAsync(x => x.RefundPolicyId == id)).IsDeleted);
        var rules = await context.RefundPolicyRules.IgnoreQueryFilters().Where(x => x.RefundPolicyId == id).ToListAsync();
        Assert.Equal(3, rules.Count);
        Assert.All(rules, r => Assert.True(r.IsDeleted));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetPolicyAsync(id, Token));
        Assert.DoesNotContain((await service.GetPoliciesAsync(new RefundPolicyQueryRequest { LatestOnly = false }, Token)).Items, x => x.PolicyId == id);
    }

    [AdminPostgresFact]
    public async Task D02_Delete_PolicyInUse_Returns409_AndKeepsData()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await ThrowsAppAsync<ConflictException>(() => service.DeletePolicyAsync(db.PolicyId, Token), 409, "POLICY_ALREADY_IN_USE");
        Assert.False((await context.RefundPolicies.SingleAsync(x => x.RefundPolicyId == db.PolicyId)).IsDeleted);
    }

    [AdminPostgresFact]
    public async Task D03_Delete_LastEffectiveActive_Returns409()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        // Archive seed policy nhường chỗ cho policy mới chưa dùng là Tour hiệu lực duy nhất sau đó.
        var other = await service.CreatePolicyAsync(Req("Duy nhất", from: Past), Token);
        await service.ArchiveAsync(db.PolicyId, Token);
        await ThrowsAppAsync<ConflictException>(() => service.DeletePolicyAsync(other, Token), 409, "REFUND_POLICY_LAST_ACTIVE");
    }

    [AdminPostgresFact]
    public async Task D04_Delete_Missing_Returns404()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeletePolicyAsync(Guid.NewGuid(), Token));
    }

    [AdminPostgresFact]
    public async Task D05_Delete_OneVersion_LeavesOthers()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v2 = await service.CloneVersionAsync(db.PolicyId, Token);
        var v3 = await service.CloneVersionAsync(db.PolicyId, Token);
        await service.DeletePolicyAsync(v3, Token);
        Assert.NotNull(await service.GetPolicyAsync(v2, Token));
        Assert.NotNull(await service.GetPolicyAsync(db.PolicyId, Token));
    }

    [AdminPostgresFact]
    public async Task D06_D07_Staff_CannotDelete_AndAdminDeleteIsAudited()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var id = await service.CreatePolicyAsync(Req(draft: true), Token);
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(db, context, "Staff").DeletePolicyAsync(id, Token));
        await service.DeletePolicyAsync(id, Token);
        Assert.True(await context.AuditLogs.AnyAsync(x => x.EntityId == id && x.Action == "Delete"));
    }

    // ---------- Archive / Activate ----------

    [AdminPostgresFact]
    public async Task A01_Archive_Active_WhenAnotherEffectiveExists()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var other = await service.CreatePolicyAsync(Req("Khác"), Token);
        await service.ArchiveAsync(other, Token);
        var detail = await service.GetPolicyAsync(other, Token);
        Assert.Equal("Archived", detail.Status);
        Assert.Equal("Archived", detail.DisplayStatus);
        Assert.True(await context.AuditLogs.AnyAsync(x => x.EntityId == other && x.Action == "Archive"));
    }

    [AdminPostgresFact]
    public async Task A02_A03_Archive_OnlyFromActive()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var draft = await service.CreatePolicyAsync(Req(draft: true), Token);
        await ThrowsClientErrorAsync(() => service.ArchiveAsync(draft, Token));
        Assert.Equal("Draft", (await service.GetPolicyAsync(draft, Token)).Status);
        var active = await service.CreatePolicyAsync(Req("A"), Token);
        await service.ArchiveAsync(active, Token);
        await ThrowsClientErrorAsync(() => service.ArchiveAsync(active, Token));
    }

    [AdminPostgresFact]
    public async Task A04_A05_Activate_Draft_NeedsRules()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var draft = await service.CreatePolicyAsync(Req(draft: true), Token);
        await service.ActivateAsync(draft, Token);
        Assert.Equal("Active", (await service.GetPolicyAsync(draft, Token)).Status);

        // Draft không rule: tạo bằng clone từ seed (seed không có rule) rồi activate
        var empty = await service.CloneVersionAsync(db.PolicyId, Token);
        Assert.Empty((await service.GetPolicyAsync(empty, Token)).Rules);
        await ThrowsClientErrorAsync(() => service.ActivateAsync(empty, Token));
        Assert.Equal("Draft", (await service.GetPolicyAsync(empty, Token)).Status);
    }

    [AdminPostgresFact]
    public async Task A06_A07_Activate_Archived_OnlyIfLatestInFamily()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v1 = await service.CreatePolicyAsync(Req("Family"), Token);
        var v2 = await service.CloneVersionAsync(v1, Token);
        await service.ArchiveAsync(v1, Token);     // còn seed là Tour hiệu lực nên được phép
        await ThrowsAppAsync<ConflictException>(() => service.ActivateAsync(v1, Token), 409, "POLICY_NOT_LATEST_VERSION");
        await service.DeletePolicyAsync(v2, Token); // v1 lại là bản mới nhất
        await service.ActivateAsync(v1, Token);
        Assert.Equal("Active", (await service.GetPolicyAsync(v1, Token)).Status);
    }

    [AdminPostgresFact]
    public async Task A08_Activate_AlreadyActive_IsRejected()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await ThrowsClientErrorAsync(() => service.ActivateAsync(db.PolicyId, Token));
    }

    [AdminPostgresFact]
    public async Task A09_Archive_InUsePolicy_IsAllowedWhenAnotherEffectiveExists()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await service.CreatePolicyAsync(Req("Thay thế"), Token);
        await service.ArchiveAsync(db.PolicyId, Token);
        Assert.Equal("Archived", (await service.GetPolicyAsync(db.PolicyId, Token)).Status);
    }

    [AdminPostgresFact]
    public async Task A10_Staff_CannotArchiveOrActivate()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var staff = Service(db, context, "Staff");
        var draft = await service.CreatePolicyAsync(Req(draft: true), Token);
        await Assert.ThrowsAsync<ForbiddenException>(() => staff.ActivateAsync(draft, Token));
        await Assert.ThrowsAsync<ForbiddenException>(() => staff.ArchiveAsync(db.PolicyId, Token));
    }

    // ---------- Last-active guard ----------

    [AdminPostgresFact]
    public async Task LA01_Archive_OnlyEffectiveTour_Returns409()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await ThrowsAppAsync<ConflictException>(() => service.ArchiveAsync(db.PolicyId, Token), 409, "REFUND_POLICY_LAST_ACTIVE");
        Assert.Equal("Active", (await service.GetPolicyAsync(db.PolicyId, Token)).Status);
    }

    [AdminPostgresFact]
    public async Task LA02_Archive_WhenAnotherEffectiveTourExists_IsAllowed()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await service.CreatePolicyAsync(Req("Khác"), Token);
        await service.ArchiveAsync(db.PolicyId, Token);
        Assert.Equal("Archived", (await service.GetPolicyAsync(db.PolicyId, Token)).Status);
    }

    [AdminPostgresFact]
    public async Task LA03_ScheduledTour_DoesNotCountAsEffective()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await service.CreatePolicyAsync(Req("Sắp có", from: Future), Token);
        await ThrowsAppAsync<ConflictException>(() => service.ArchiveAsync(db.PolicyId, Token), 409, "REFUND_POLICY_LAST_ACTIVE");
    }

    [AdminPostgresFact]
    public async Task LA04_GuardIsPerType()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await service.CreatePolicyAsync(Req("Rental", type: "Rental"), Token);
        await ThrowsAppAsync<ConflictException>(() => service.ArchiveAsync(db.PolicyId, Token), 409, "REFUND_POLICY_LAST_ACTIVE");
    }

    [AdminPostgresFact]
    public async Task LA05_ExpiredActive_CanBeArchived()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var expired = await service.CreatePolicyAsync(Req("Hết hạn", from: Past, until: new DateOnly(2026, 2, 1)), Token);
        Assert.Equal("Expired", (await service.GetPolicyAsync(expired, Token)).DisplayStatus);
        await service.ArchiveAsync(expired, Token);
        Assert.Equal("Archived", (await service.GetPolicyAsync(expired, Token)).Status);
    }

    [AdminPostgresFact]
    public async Task LA06_AfterActivatingNewVersion_OldCanBeArchived()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v2 = await service.CloneVersionAsync(db.PolicyId, Token);
        await service.SetRulesAsync(v2, new RefundPolicyRulesRequest { Rules = Rules3() }, Token);
        await service.ActivateAsync(v2, Token);
        await service.ArchiveAsync(db.PolicyId, Token);
        Assert.Equal("Archived", (await service.GetPolicyAsync(db.PolicyId, Token)).Status);
    }

    // ---------- Clone / Family ----------

    [AdminPostgresFact]
    public async Task CL01_CL02_Clone_CreatesDraftNextVersion_AndLeavesSourceUntouched()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var sourceId = await service.CreatePolicyAsync(Req("Gốc"), Token);
        var before = await service.GetPolicyAsync(sourceId, Token);
        var cloneId = await service.CloneVersionAsync(sourceId, Token);
        var clone = await service.GetPolicyAsync(cloneId, Token);
        Assert.NotEqual(sourceId, cloneId);
        Assert.Equal("Draft", clone.Status);
        Assert.Equal(before.Version + 1, clone.Version);
        Assert.Equal(before.PolicyFamilyId, clone.PolicyFamilyId);
        Assert.Equal(before.Rules.Count, clone.Rules.Count);
        var after = await service.GetPolicyAsync(sourceId, Token);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.EffectiveUntil, after.EffectiveUntil);
        Assert.Equal(before.Rules.Count, after.Rules.Count);
    }

    [AdminPostgresFact]
    public async Task CL03_ActivatingNewVersion_ClosesOlderVersion()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v2 = await service.CloneVersionAsync(db.PolicyId, Token);
        await service.SetRulesAsync(v2, new RefundPolicyRulesRequest { Rules = Rules3() }, Token);
        await service.ActivateAsync(v2, Token);
        var v1 = await service.GetPolicyAsync(db.PolicyId, Token);
        var v2Detail = await service.GetPolicyAsync(v2, Token);
        Assert.Equal("Active", v2Detail.Status);
        Assert.Equal(v2Detail.EffectiveFrom, v1.EffectiveUntil);
        Assert.Equal("Expired", v1.DisplayStatus);
    }

    [AdminPostgresFact]
    public async Task CL04_CloneTwice_GivesVersion2Then3()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v2 = await service.CloneVersionAsync(db.PolicyId, Token);
        var v3 = await service.CloneVersionAsync(db.PolicyId, Token);
        Assert.Equal(2, (await service.GetPolicyAsync(v2, Token)).Version);
        Assert.Equal(3, (await service.GetPolicyAsync(v3, Token)).Version);
    }

    [AdminPostgresFact]
    public async Task CL05_EditClonedDraft_DoesNotTouchSource()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v2 = await service.CloneVersionAsync(db.PolicyId, Token);
        await service.SetRulesAsync(v2, new RefundPolicyRulesRequest { Rules = [new() { MinHoursBefore = 5, RefundPercentage = 5 }] }, Token);
        Assert.Single((await service.GetPolicyAsync(v2, Token)).Rules);
        Assert.Empty((await service.GetPolicyAsync(db.PolicyId, Token)).Rules);
    }

    [AdminPostgresFact]
    public async Task CL06_ActivatingRentalFamily_ClosesOtherRentalFamilies()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var a = await service.CreatePolicyAsync(Req("Rental A", type: "Rental"), Token);
        var b = await service.CreatePolicyAsync(Req("Rental B", type: "Rental", draft: true), Token);
        await service.ActivateAsync(b, Token);
        Assert.NotNull((await service.GetPolicyAsync(a, Token)).EffectiveUntil);
        Assert.Equal("Active", (await service.GetPolicyAsync(b, Token)).DisplayStatus);
    }

    [AdminPostgresFact]
    public async Task CL07_ActivatingTourFamily_KeepsOtherTourFamilies()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var a = await service.CreatePolicyAsync(Req("Tour A"), Token);
        var b = await service.CreatePolicyAsync(Req("Tour B", draft: true), Token);
        await service.ActivateAsync(b, Token);
        Assert.Null((await service.GetPolicyAsync(a, Token)).EffectiveUntil);
        Assert.Null((await service.GetPolicyAsync(db.PolicyId, Token)).EffectiveUntil);
    }

    [AdminPostgresFact]
    public async Task F01_F02_F03_FamilyVersions_AndLatestOnly()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v2 = await service.CloneVersionAsync(db.PolicyId, Token);
        var v3 = await service.CloneVersionAsync(db.PolicyId, Token);
        var detail = await service.GetPolicyAsync(v3, Token);
        Assert.Equal(3, detail.Versions.Count);

        var latest = await service.GetPoliciesAsync(new RefundPolicyQueryRequest(), Token);
        var row = Assert.Single(latest.Items, x => x.PolicyFamilyId == detail.PolicyFamilyId);
        Assert.Equal(v3, row.PolicyId);
        Assert.Equal(3, row.VersionsCount);

        var all = await service.GetPoliciesAsync(new RefundPolicyQueryRequest { LatestOnly = false }, Token);
        Assert.Equal(3, all.Items.Count(x => x.PolicyFamilyId == detail.PolicyFamilyId));
        Assert.Contains(all.Items, x => x.PolicyId == v2);
    }

    [AdminPostgresFact]
    public async Task F04_Clone_ArchivedSource_IsAllowed()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var a = await service.CreatePolicyAsync(Req("Cũ"), Token);
        await service.ArchiveAsync(a, Token);
        var clone = await service.CloneVersionAsync(a, Token);
        Assert.Equal("Draft", (await service.GetPolicyAsync(clone, Token)).Status);
    }

    // ---------- Status / flags ----------

    [AdminPostgresFact]
    public async Task S01_Draft_Flags()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var d = await service.GetPolicyAsync(await service.CreatePolicyAsync(Req(draft: true), Token), Token);
        Assert.True(d.CanActivate); Assert.False(d.CanArchive); Assert.True(d.CanEdit); Assert.True(d.CanDelete); Assert.False(d.IsLocked);
    }

    [AdminPostgresFact]
    public async Task S02_ActiveUnused_Flags()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var d = await service.GetPolicyAsync(await service.CreatePolicyAsync(Req("Active"), Token), Token);
        Assert.Equal("Active", d.DisplayStatus);
        Assert.True(d.CanArchive); Assert.True(d.CanEdit); Assert.False(d.CanActivate); Assert.True(d.CanClone);
    }

    [AdminPostgresFact]
    public async Task S04_S07_Expired_WhenUntilIsPastOrToday()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var past = await service.CreatePolicyAsync(Req("P", until: new DateOnly(2026, 2, 1)), Token);
        var today = await service.CreatePolicyAsync(Req("T", until: DateOnly.FromDateTime(db.Now.UtcDateTime)), Token);
        Assert.Equal("Expired", (await service.GetPolicyAsync(past, Token)).DisplayStatus);
        Assert.Equal("Expired", (await service.GetPolicyAsync(today, Token)).DisplayStatus);
    }

    [AdminPostgresFact]
    public async Task S05_Archived_CanActivateOnlyWhenLatest()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var v1 = await service.CreatePolicyAsync(Req("Fam"), Token);
        await service.ArchiveAsync(v1, Token);
        Assert.True((await service.GetPolicyAsync(v1, Token)).CanActivate);
        await service.CloneVersionAsync(v1, Token);
        Assert.False((await service.GetPolicyAsync(v1, Token)).CanActivate);
    }

    [AdminPostgresFact]
    public async Task S06_S08_InUse_IsLocked_AndCountsUsage()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var d = await service.GetPolicyAsync(db.PolicyId, Token);
        Assert.True(d.IsLocked); Assert.False(d.CanEdit); Assert.False(d.CanDelete); Assert.True(d.CanClone);
        var row = (await service.GetPoliciesAsync(new RefundPolicyQueryRequest(), Token)).Items.Single(x => x.PolicyId == db.PolicyId);
        Assert.Equal(1, row.TripsCount);
        Assert.True(row.OrdersCount >= 1);
        Assert.Equal(row.TripsCount + row.OrdersCount, row.UsageCount);
    }

    // ---------- Query ----------

    private static async Task SeedManyAsync(IAdminRefundPolicyService service)
    {
        await service.CreatePolicyAsync(Req("Alpha Tết", draft: true), Token);
        await service.CreatePolicyAsync(Req("Beta Hè"), Token);
        await service.CreatePolicyAsync(Req("Gamma Thuê", type: "Rental"), Token);
        await service.CreatePolicyAsync(Req("Delta Sắp", from: Future), Token);
    }

    [AdminPostgresFact]
    public async Task Q01_Paging()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await SeedManyAsync(service); // + seed = 5 family
        var p1 = await service.GetPoliciesAsync(new RefundPolicyQueryRequest { PageNumber = 1, PageSize = 2 }, Token);
        var p3 = await service.GetPoliciesAsync(new RefundPolicyQueryRequest { PageNumber = 3, PageSize = 2 }, Token);
        Assert.Equal(2, p1.Items.Count);
        Assert.Equal(5, p1.TotalItems);
        Assert.Equal(3, p1.TotalPages);
        Assert.Single(p3.Items);
    }

    [AdminPostgresFact]
    public async Task Q02_Q03_Q04_Filters()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await SeedManyAsync(service);
        var tour = await service.GetPoliciesAsync(new RefundPolicyQueryRequest { PolicyType = "Tour" }, Token);
        Assert.All(tour.Items, x => Assert.Equal("Tour", x.PolicyType));
        Assert.Equal(4, tour.TotalItems);
        var draft = await service.GetPoliciesAsync(new RefundPolicyQueryRequest { Status = "Draft" }, Token);
        Assert.Equal("Alpha Tết", Assert.Single(draft.Items).Name);
        var scheduled = await service.GetPoliciesAsync(new RefundPolicyQueryRequest { Status = "Scheduled" }, Token);
        Assert.Equal("Delta Sắp", Assert.Single(scheduled.Items).Name);
        var active = await service.GetPoliciesAsync(new RefundPolicyQueryRequest { Status = "Active" }, Token);
        Assert.Equal(3, active.TotalItems);
        Assert.Empty((await service.GetPoliciesAsync(new RefundPolicyQueryRequest { Status = "Archived" }, Token)).Items);
        Assert.Empty((await service.GetPoliciesAsync(new RefundPolicyQueryRequest { Status = "Expired" }, Token)).Items);
    }

    [AdminPostgresFact]
    public async Task Q05_Search_IsCaseInsensitive()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await SeedManyAsync(service);
        var result = await service.GetPoliciesAsync(new RefundPolicyQueryRequest { Search = "tẾt" }, Token);
        Assert.Equal("Alpha Tết", Assert.Single(result.Items).Name);
    }

    [AdminPostgresFact]
    public async Task Q06_Q07_Sort()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        await SeedManyAsync(service);
        var asc = (await service.GetPoliciesAsync(new RefundPolicyQueryRequest { SortBy = "name", SortOrder = "asc" }, Token)).Items.Select(x => x.Name).ToList();
        var desc = (await service.GetPoliciesAsync(new RefundPolicyQueryRequest { SortBy = "name", SortOrder = "desc" }, Token)).Items.Select(x => x.Name).ToList();
        Assert.Equal(asc.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(), asc);
        Assert.Equal(asc.AsEnumerable().Reverse().ToList(), desc);
        var byFrom = (await service.GetPoliciesAsync(new RefundPolicyQueryRequest { SortBy = "effectiveFrom", SortOrder = "desc" }, Token)).Items;
        Assert.Equal("Delta Sắp", byFrom[0].Name);
    }

    [AdminPostgresFact]
    public async Task Q11_Items_HaveCreatedByName()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var id = await service.CreatePolicyAsync(Req("Có người tạo"), Token);
        var row = (await service.GetPoliciesAsync(new RefundPolicyQueryRequest(), Token)).Items.Single(x => x.PolicyId == id);
        Assert.Equal("Admin Test", row.CreatedByName);
    }

    [AdminPostgresFact]
    public async Task G07_SeededTourPolicy_MatchesTourFilter()
    {
        var (db, context, service) = await StartAsync();
        await using var _ = db; await using var __ = context;
        var tour = await service.GetPoliciesAsync(new RefundPolicyQueryRequest { PolicyType = "Tour" }, Token);
        Assert.Contains(tour.Items, x => x.PolicyId == db.PolicyId);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private sealed class TestActor(Guid userId, string role) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public Guid RequiredUserId => userId;
        public string? Jti => null;
        public string? Role => role;
        public bool IsAuthenticated => true;
    }
}
